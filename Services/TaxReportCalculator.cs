using Pelago.Models;

namespace Pelago.Services;

/// <summary>Il trattamento fiscale indicativo di uno strumento (in italiano la chiave: sono i nomi mostrati nel prospetto).</summary>
public enum TaxCategory
{
    Azioni,
    EtcEtn,
    EtfOicr,
    Obbligazioni,
    Derivati,
    Cripto,
}

public static class TaxCategories
{
    public static string Label(TaxCategory category) => category switch
    {
        TaxCategory.Azioni => "Azioni",
        TaxCategory.EtcEtn => "ETC / ETN",
        TaxCategory.EtfOicr => "ETF e fondi (OICR)",
        TaxCategory.Obbligazioni => "Obbligazioni",
        TaxCategory.Derivati => "Derivati e certificati (turbo)",
        _ => "Cripto-attività",
    };

    /// <summary>
    /// Euristica: un «fondo» è un ETF/OICR (le plusvalenze sono redditi di capitale, non compensabili con le minusvalenze) tranne ETC ed ETN
    /// (redditi diversi, compensabili). Il file non lo dice: si deduce dal nome e dal codice. L'utente può correggerlo strumento per strumento.
    /// </summary>
    public static TaxCategory ForFund(string name, string key)
    {
        var isEtc = EtcHint.IsMatch(name) || key.StartsWith("JE", StringComparison.OrdinalIgnoreCase);
        return isEtc ? TaxCategory.EtcEtn : TaxCategory.EtfOicr;
    }

    // ETC/ETN/ETP nel nome (anche attaccati a un numero, come «ETP2062»), materie prime fisiche, prodotti a leva (un UCITS non può averla) e la sigla WITR di WisdomTree
    private static readonly System.Text.RegularExpressions.Regex EtcHint = new(
        @"(?<![A-Z])(ETC|ETN|ETP)|PHYSICAL|XETRA-GOLD|COM\.SEC|MU\.AS\.I|LEVERAGED|DAILY SHORT|\b\d+X\b|^WITR\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}

/// <summary>Una cessione con il suo costo, calcolato come previsto per la dichiarazione dei redditi (LIFO; costo medio per ETF/OICR e cripto).</summary>
public sealed record SaleLine(
    DateOnly Date,
    string Key,
    string Name,
    string AssetClass,
    TaxCategory Category,
    string Type,
    decimal Shares,
    decimal Proceeds,
    decimal CostBasis,
    decimal TaxWithheld,
    decimal Unmatched = 0m)
{
    public int Year => Date.Year;
    public decimal Result => Proceeds - CostBasis;

    /// <summary>Quote vendute per cui nel file non c'è un acquisto: il loro costo manca e qui conta come zero.</summary>
    public bool HasMissingCost => Unmatched > 0.000000001m;
}

/// <summary>Reddito che non nasce da una vendita: interessi sulla liquidità o cedole, con l'imposta già trattenuta.</summary>
public sealed record IncomeLine(int Year, string Kind, decimal Gross, decimal TaxWithheld);

public sealed record BackpackYear(int Year, decimal Net, decimal OpeningAvailable, decimal Used, decimal NewLoss, decimal Expired, decimal ClosingAvailable, decimal Taxable);

/// <summary>Una minusvalenza ancora utilizzabile: l'anno in cui è nata, quanta ne resta, fino a quando vale.</summary>
public sealed record BackpackLot(int LossYear, decimal Original, decimal Remaining)
{
    public int LastUsableYear => LossYear + TaxReportCalculator.CarryForwardYears;
}

public sealed record Backpack(IReadOnlyList<BackpackYear> Years, IReadOnlyList<BackpackLot> Lots);

public sealed class TaxReport
{
    public List<SaleLine> Sales { get; init; } = new();
    public List<IncomeLine> Income { get; init; } = new();
    public Backpack Diversi { get; init; } = new(Array.Empty<BackpackYear>(), Array.Empty<BackpackLot>());
    public Backpack Cripto { get; init; } = new(Array.Empty<BackpackYear>(), Array.Empty<BackpackLot>());

    public IEnumerable<int> Years => Sales.Select(s => s.Year).Concat(Income.Select(i => i.Year)).Distinct().OrderBy(y => y);
}

/// <summary>
/// Prospetto fiscale <b>indicativo</b>: per ogni anno, risultati delle cessioni divisi per tipo di reddito, interessi e cedole, imposte già trattenute dal broker
/// e «zainetto» delle minusvalenze (quelle non compensate si riportano nei quattro anni successivi). Non è una consulenza né un modello da riportare:
/// le regole cambiano e hanno eccezioni, e il file del broker non dice tutto (per esempio se un fondo è un ETF o un ETC).
///
/// Semplificazioni dichiarate:
/// <list type="bullet">
/// <item>Costo calcolato a LIFO (ultimi acquistati, primi venduti) per azioni, obbligazioni, derivati ed ETC; a costo medio ponderato per ETF/OICR e cripto.</item>
/// <item>Le commissioni si sommano al costo e si tolgono dal ricavo; le imposte trattenute dal broker non riducono il risultato e si mostrano a parte.</item>
/// <item>Le plusvalenze degli ETF/OICR sono «redditi di capitale» (non compensabili); le loro minusvalenze finiscono tra i «redditi diversi».</item>
/// <item>Le cripto-attività hanno uno zainetto a parte (si ipotizza che compensino solo se stesse).</item>
/// <item>I turbo andati a zero senza una vendita registrata non sono inclusi: la minusvalenza si realizza quando il broker li chiude.</item>
/// </list>
/// </summary>
public static class TaxReportCalculator
{
    /// <summary>Le minusvalenze non compensate si possono usare nei quattro anni successivi.</summary>
    public const int CarryForwardYears = 4;

    private const decimal Epsilon = 0.000000001m;

    /// <param name="fundOverride">Trattamento scelto dall'utente per un fondo (chiave del titolo → categoria), se c'è.</param>
    public static TaxReport Compute(IEnumerable<Operation> operations, Func<string, TaxCategory?>? fundOverride = null)
    {
        var ops = OperationCleanup.WithoutReversals(operations);
        var sales = new List<SaleLine>();

        foreach (var group in ops.Where(o => !string.IsNullOrWhiteSpace(o.PositionKey) && !string.IsNullOrWhiteSpace(o.AssetClass)).GroupBy(o => o.PositionKey))
        {
            var first = group.First();
            var name = AssetNameResolver.ResolveDisplayName(first.Name, first.AssetClass, group.Select(o => o.Description));
            var category = CategoryOf(first.AssetClass, name, group.Key, fundOverride);
            var average = category is TaxCategory.EtfOicr or TaxCategory.Cripto;

            var lots = new List<(decimal Shares, decimal Cost)>();
            foreach (var op in group.OrderBy(o => o.DateTime))
            {
                var delta = op.Shares ?? 0m;
                if (delta == 0m)
                {
                    continue;
                }

                // costo/ricavo per la dichiarazione: l'importo con le commissioni, senza le imposte trattenute
                var gross = (op.Amount ?? 0m) + (op.Fee ?? 0m);
                if (delta > 0m)
                {
                    lots.Add((delta, gross < 0m ? -gross : 0m));
                    continue;
                }

                var sold = -delta;
                var (costBasis, unmatched) = TakeCost(lots, sold, average);
                sales.Add(new SaleLine(op.Date, group.Key, name, first.AssetClass, category, op.Type, sold, gross, costBasis, -(op.Tax ?? 0m), unmatched));
            }
        }

        // incassi in contanti dell'esercizio dei warrant (righe TILG senza quantità): ricavo senza costo, sul titolo indicato nella descrizione
        foreach (var op in ops.Where(o => o.Type == "TILG" && (o.Amount ?? 0m) != 0m))
        {
            sales.Add(new SaleLine(op.Date, op.Symbol, op.Name, "DERIVATIVE", TaxCategory.Derivati, "TILG", 0m, (op.Amount ?? 0m) + (op.Fee ?? 0m), 0m, -(op.Tax ?? 0m)));
        }

        var income = ops.Where(o => o.Type == "INTEREST_PAYMENT" && o.Category is "CASH" or "INTEREST")
            .GroupBy(o => (o.Date.Year, Kind: o.AssetClass == "BOND" ? "Cedole e interessi delle obbligazioni" : "Interessi sulla liquidità"))
            .Select(g => new IncomeLine(g.Key.Year, g.Key.Kind, g.Sum(o => o.Amount ?? 0m), -g.Sum(o => o.Tax ?? 0m)))
            .OrderBy(i => i.Year).ThenBy(i => i.Kind)
            .ToList();

        var ordered = sales.OrderBy(s => s.Date).ToList();
        var years = ordered.Select(s => s.Year).Concat(income.Select(i => i.Year)).Distinct().OrderBy(y => y).ToList();
        var firstYear = years.Count == 0 ? DateTime.Today.Year : years[0];
        var lastYear = Math.Max(years.Count == 0 ? firstYear : years[^1], DateTime.Today.Year);

        decimal NetDiversi(int year) => ordered.Where(s => s.Year == year).Where(s => s.Category != TaxCategory.Cripto)
            .Sum(s => s.Category == TaxCategory.EtfOicr ? Math.Min(0m, s.Result) : s.Result);
        decimal NetCripto(int year) => ordered.Where(s => s.Year == year && s.Category == TaxCategory.Cripto).Sum(s => s.Result);

        return new TaxReport
        {
            Sales = ordered,
            Income = income,
            Diversi = RunBackpack(firstYear, lastYear, NetDiversi),
            Cripto = RunBackpack(firstYear, lastYear, NetCripto),
        };
    }

    private static TaxCategory CategoryOf(string assetClass, string name, string key, Func<string, TaxCategory?>? fundOverride) => assetClass switch
    {
        "STOCK" => TaxCategory.Azioni,
        "BOND" => TaxCategory.Obbligazioni,
        "CRYPTO" => TaxCategory.Cripto,
        "DERIVATIVE" => TaxCategory.Derivati,
        "FUND" => fundOverride?.Invoke(key) ?? TaxCategories.ForFund(name, key),
        _ => TaxCategory.Azioni,
    };

    /// <summary>
    /// Toglie <paramref name="sold"/> quote dai lotti e restituisce il costo di quelle quote (LIFO, oppure costo medio ponderato) e le quote
    /// vendute che nei lotti non c'erano (costo mancante, contato come zero).
    /// </summary>
    private static (decimal Cost, decimal Unmatched) TakeCost(List<(decimal Shares, decimal Cost)> lots, decimal sold, bool average)
    {
        var cost = 0m;
        if (average)
        {
            var totalShares = lots.Sum(l => l.Shares);
            var totalCost = lots.Sum(l => l.Cost);
            if (totalShares <= Epsilon)
            {
                return (0m, sold);
            }

            var take = Math.Min(sold, totalShares);
            cost = totalCost * take / totalShares;
            var remaining = totalShares - take;
            lots.Clear();
            if (remaining > Epsilon)
            {
                lots.Add((remaining, totalCost - cost));
            }
            return (cost, sold - take);
        }

        var left = sold;
        for (var i = lots.Count - 1; i >= 0 && left > Epsilon; i--)
        {
            var (shares, lotCost) = lots[i];
            var take = Math.Min(shares, left);
            var part = lotCost * take / shares;
            cost += part;
            left -= take;
            if (shares - take <= Epsilon)
            {
                lots.RemoveAt(i);
            }
            else
            {
                lots[i] = (shares - take, lotCost - part);
            }
        }
        return (cost, left > Epsilon ? left : 0m);
    }

    /// <summary>
    /// Lo zainetto anno per anno: se il risultato netto dell'anno è una perdita entra nello zainetto; se è un guadagno si compensa con le minusvalenze
    /// ancora valide, dalla più vecchia. Una minusvalenza nata nell'anno Y si può usare fino al quarto anno successivo.
    /// </summary>
    internal static Backpack RunBackpack(int firstYear, int lastYear, Func<int, decimal> netOf)
    {
        var lots = new List<BackpackLot>();
        var rows = new List<BackpackYear>();

        for (var year = firstYear; year <= lastYear; year++)
        {
            var expired = lots.Where(l => l.LastUsableYear < year).Sum(l => l.Remaining);
            lots.RemoveAll(l => l.LastUsableYear < year);
            var opening = lots.Sum(l => l.Remaining);
            var net = netOf(year);
            decimal used = 0m, newLoss = 0m;

            if (net < 0m)
            {
                newLoss = -net;
                lots.Add(new BackpackLot(year, newLoss, newLoss));
            }
            else if (net > 0m)
            {
                var toCover = net;
                for (var i = 0; i < lots.Count && toCover > 0m; i++)
                {
                    var take = Math.Min(lots[i].Remaining, toCover);
                    lots[i] = lots[i] with { Remaining = lots[i].Remaining - take };
                    toCover -= take;
                    used += take;
                }
            }

            rows.Add(new BackpackYear(year, net, opening, used, newLoss, expired, lots.Sum(l => l.Remaining), net > 0m ? net - used : 0m));
        }

        return new Backpack(rows, lots.Where(l => l.Remaining > 0m).ToList());
    }
}
