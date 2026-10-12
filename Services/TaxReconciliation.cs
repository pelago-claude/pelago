using DPOper.Models;
using static DPOper.Services.Loc;

namespace DPOper.Services;

/// <summary>Un riscontro sul prospetto fiscale: cosa è stato controllato, com'è andata e (se serve) cosa guardare. I testi sono già nella lingua corrente.</summary>
public sealed record TaxCheck(string Code, FindingLevel Level, string Title, string Detail);

/// <summary>Le imposte trattenute in un anno, divise per tipo come nel file, e quanto le imposte sulle vendite coincidono con quelle del prospetto.</summary>
public sealed record YearTaxRow(
    int Year,
    decimal OnSales,
    decimal OnInterest,
    decimal StampAndRecalculation,
    decimal Refunds,
    decimal Other,
    decimal Total,
    decimal SalesInReport,
    decimal Difference);

public sealed record TaxReconciliation(IReadOnlyList<TaxCheck> Checks, IReadOnlyList<YearTaxRow> Years)
{
    /// <summary>Quanti riscontri chiedono un'occhiata (avvisi e informazioni).</summary>
    public int ToCheck => Checks.Count(c => c.Level != FindingLevel.Ok);
}

/// <summary>
/// Riscontri sul prospetto fiscale: mettono a confronto le imposte nel file con quelle del prospetto e segnalano ciò che rende i numeri meno
/// affidabili (costi mancanti, turbo azzerati senza vendita, fondi classificati dal nome, fusioni e storni ignorati, cripto ricevute gratis).
/// Non è un calcolo fiscale: serve a sapere quanto fidarsi del prospetto.
/// </summary>
public static class TaxReconciliationCalculator
{
    private const decimal Tolerance = 0.01m;

    /// <param name="operations">Tutti i movimenti, come nel file.</param>
    /// <param name="report">Il prospetto già calcolato da <see cref="TaxReportCalculator"/>.</param>
    /// <param name="positions">Le posizioni con i cicli (per riconoscere i turbo azzerati senza vendita).</param>
    /// <param name="fundChoiceMade">Se per un fondo l'utente ha scelto a mano ETF o ETC.</param>
    /// <param name="nameOf">Nome da mostrare di un titolo (chiave, nome del file).</param>
    public static TaxReconciliation Compute(IReadOnlyList<Operation> operations, TaxReport report, IEnumerable<ClosedPosition> positions,
        Func<string, bool> fundChoiceMade, Func<string, string, string>? nameOf = null)
    {
        nameOf ??= (_, fallback) => fallback;

        // imposte per anno: quelle sulle vendite nel file contro quelle sulle righe di vendita del prospetto
        var expectedOnSales = operations.Where(o => o.Type is "SELL" or "SELL_CANCELLED" or "WARRANT_EXERCISE" or "TILG")
            .GroupBy(o => o.Date.Year).ToDictionary(g => g.Key, g => -g.Sum(o => o.Tax ?? 0m));
        var inReport = report.Sales.GroupBy(s => s.Year).ToDictionary(g => g.Key, g => g.Sum(s => s.TaxWithheld));

        var years = CostCalculator.ByYear(operations).Select(c =>
        {
            var year = int.Parse(c.Period);
            var reported = inReport.GetValueOrDefault(year);
            return new YearTaxRow(year, c.TradeTax, c.InterestTax, c.StampAndRecalculation, c.TaxOptimization, c.OtherTax, c.NetTax,
                reported, expectedOnSales.GetValueOrDefault(year) - reported);
        }).OrderByDescending(r => r.Year).ToList();

        var checks = new List<TaxCheck>();

        var missing = report.Sales.Where(s => s.HasMissingCost).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.GroupBy(s => s.Key).Select(g => nameOf(g.Key, g.First().Name)).Take(3));
            checks.Add(new TaxCheck("missingcost", FindingLevel.Warning, T("Costo mancante su alcune vendite"),
                T("{0} vendite hanno quote senza acquisto nel file (per esempio: {1}). Il costo mancante conta come zero e il guadagno può risultare troppo alto.", missing.Count, names)));
        }
        else
        {
            checks.Add(new TaxCheck("missingcost", FindingLevel.Ok, T("Costo delle vendite"), T("Ogni vendita ha i suoi acquisti nel file.")));
        }

        var off = years.Where(r => Math.Abs(r.Difference) > Tolerance).ToList();
        if (off.Count > 0)
        {
            checks.Add(new TaxCheck("taxdiff", FindingLevel.Warning, T("Imposte sulle vendite che non tornano"),
                T("Nel file le imposte sulle vendite di {0} non coincidono con quelle del prospetto (differenza {1}): alcune vendite non sono attribuite a un titolo.",
                    string.Join(", ", off.OrderBy(r => r.Year).Select(r => r.Year)), Fmt.Money(off.Sum(r => r.Difference)))));
        }
        else
        {
            checks.Add(new TaxCheck("taxdiff", FindingLevel.Ok, T("Imposte sulle vendite"), T("Le imposte trattenute sulle vendite nel file coincidono con quelle del prospetto, anno per anno.")));
        }

        var knockedOut = positions.SelectMany(p => p.Cycles.Where(c => c.KnockedOut)).ToList();
        if (knockedOut.Count > 0)
        {
            checks.Add(new TaxCheck("knockout", FindingLevel.Warning, T("Turbo azzerati senza una vendita"),
                T("{0} turbo risultano azzerati ma senza vendita nel file, per circa {1} di perdita. Non sono nel prospetto: se sono scaduti, la perdita si realizza quando il broker li chiude.",
                    knockedOut.Count, Fmt.Money(Math.Abs(knockedOut.Sum(c => c.Result))))));
        }
        else
        {
            checks.Add(new TaxCheck("knockout", FindingLevel.Ok, T("Turbo azzerati"), T("Nessun turbo risulta azzerato senza una vendita registrata.")));
        }

        var reversals = operations.Count(o => o.Type is "MERGER" or "MERGER_CANCELLED" or "SELL_CANCELLED");
        if (reversals > 0)
        {
            checks.Add(new TaxCheck("reversals", FindingLevel.Info, T("Fusioni e vendite annullate"),
                T("{0} righe di fusioni di fondi o di vendite annullate si compensano e non sono contate come vendite.", reversals)));
        }

        var unchosen = report.Sales.Where(s => s.AssetClass == "FUND").Select(s => s.Key).Distinct().Count(k => !fundChoiceMade(k));
        if (unchosen > 0)
        {
            checks.Add(new TaxCheck("funds", FindingLevel.Info, T("Fondi classificati dal nome"),
                T("{0} fondi sono classificati dal nome (ETF o ETC): controlla il trattamento nella sezione dei fondi.", unchosen)));
        }

        var rewards = operations.Count(o => o.Type == "FREE_RECEIPT" && o.AssetClass == "CRYPTO" && (o.Shares ?? 0m) > 0m);
        if (rewards > 0)
        {
            checks.Add(new TaxCheck("rewards", FindingLevel.Info, T("Cripto ricevute gratis"),
                T("{0} accrediti gratuiti di cripto (premi) hanno costo zero e potrebbero essere un reddito da dichiarare: chiedi al commercialista.", rewards)));
        }

        return new TaxReconciliation(checks.OrderByDescending(c => c.Level).ToList(), years);
    }
}
