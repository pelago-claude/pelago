using Pelago.Models;
using static Pelago.Services.Loc;

namespace Pelago.Services;

public enum FindingLevel { Ok, Info, Warning }

/// <summary>
/// Un esito dei controlli di coerenza: cosa è stato verificato, com'è andata e (se serve) dove andare a sistemare.
/// I testi sono già nella lingua corrente: se la lingua cambia, <see cref="AppState"/> rifà i controlli.
/// </summary>
public sealed record Finding(string Code, FindingLevel Level, string Title, string Detail, string? Route = null);

/// <summary>
/// Controlli sui dati, per accorgersi subito di numeri che non possono essere giusti (un prezzo in sterline letto come euro,
/// una vendita senza il relativo acquisto, un file esportato mesi fa...). Ogni controllo produce sempre un esito: «ok» se è
/// tutto a posto, così la pagina mostra anche cosa è stato verificato.
/// </summary>
public static class ConsistencyChecker
{
    public const int StaleQuoteDays = 3;
    private const double PriceJumpHigh = 1.5;
    private const double PriceJumpLow = 1.0 / 1.5;

    public static List<Finding> Run(
        IReadOnlyList<Operation> operations,
        IReadOnlyList<DailySnapshot> snapshots,
        IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> quotes,
        IReadOnlyList<AssetCatalogEntry> catalog,
        IReadOnlyList<Holding> holdings,
        Func<AssetCatalogEntry, List<QuoteEntry>> loadQuotes,
        Func<string, string> nameOf,
        DateTime now)
    {
        var findings = new List<Finding>();
        var open = holdings.Where(h => h.Shares > 0.000001m && h.AssetClass != "FX").ToList();

        // 1. Liquidità negativa: sintomo tipico di operazioni mancanti nel file.
        var negativeDays = snapshots.Where(s => s.Cash < -0.5m).OrderBy(s => s.Date).ToList();
        findings.Add(negativeDays.Count == 0
            ? new Finding("cash", FindingLevel.Ok, T("Liquidità mai negativa"), T("In nessun giorno il saldo in cassa scende sotto zero."))
            : new Finding("cash", FindingLevel.Warning, T("Liquidità negativa in alcuni giorni"),
                T("{0} giorni con la cassa sotto zero (il primo: {1:dd/MM/yyyy}, {2}). Di solito significa che nel file mancano dei versamenti o altre operazioni.",
                    negativeDays.Count, negativeDays[0].Date, Fmt.Money(negativeDays[0].Cash)), "daily"));

        // 2. Quantità negative: una vendita senza l'acquisto corrispondente.
        var negative = holdings.Where(h => h.Shares < -0.000001m).ToList();
        findings.Add(negative.Count == 0
            ? new Finding("negshares", FindingLevel.Ok, T("Nessuna quantità negativa"), T("Ogni vendita ha il suo acquisto."))
            : new Finding("negshares", FindingLevel.Warning, T("Quantità negative"),
                T("{0} asset risultano venduti più di quanto acquistato: {1}. Può mancare una parte della storia.", negative.Count, Names(negative.Select(h => nameOf(h.Key)))), "securities"));

        // 3. Posizioni aperte senza quotazione scaricata (valutate all'ultimo prezzo di operazione).
        var priced = open.Where(h => h.AssetClass is "STOCK" or "FUND" or "CRYPTO").ToList();
        var noQuote = priced.Where(h => !quotes.ContainsKey(h.Key)).ToList();
        findings.Add(noQuote.Count == 0
            ? new Finding("noquote", FindingLevel.Ok, T("Tutte le posizioni hanno una quotazione"), T("Azioni, fondi e crypto aperti sono valutati con un prezzo scaricato."))
            : new Finding("noquote", FindingLevel.Warning, T("Posizioni senza quotazione scaricata"),
                T("{0} posizioni aperte sono valutate all'ultimo prezzo di una tua operazione: {1}. Aggiorna le quotazioni.", noQuote.Count, Names(noQuote.Select(h => nameOf(h.Key)))), "quotes"));

        // 4. Quotazioni vecchie.
        var stale = priced.Where(h => h.LastPriceTimestamp is { } t && quotes.ContainsKey(h.Key) && (now - t).TotalDays > StaleQuoteDays).ToList();
        findings.Add(stale.Count == 0
            ? new Finding("stale", FindingLevel.Ok, T("Prezzi aggiornati"), T("Nessuna quotazione ha più di {0} giorni.", StaleQuoteDays))
            : new Finding("stale", FindingLevel.Warning, T("Prezzi vecchi"),
                T("{0} posizioni hanno una quotazione di oltre {1} giorni fa: {2}.", stale.Count, StaleQuoteDays, Names(stale.Select(h => nameOf(h.Key)))), "quotes"));

        // 5. Prezzi sospetti: lontanissimi dall'ultimo prezzo di operazione (valuta sbagliata, ticker sbagliato...).
        var suspicious = new List<string>();
        foreach (var h in priced.Concat(open.Where(h => h.AssetClass == "BOND")))
        {
            if (!quotes.TryGetValue(h.Key, out var points) || points.Count == 0)
            {
                continue;
            }
            var quote = points.OrderByDescending(p => p.Timestamp).First();
            var trade = operations.Where(o => o.PositionKey == h.Key && o.Category == "TRADING" && o.Type is "BUY" or "SELL" && o.Price is > 0m)
                .OrderByDescending(o => o.DateTime).FirstOrDefault();
            if (trade is null || (quote.Timestamp - trade.DateTime).TotalDays > 90 || trade.Price is not { } tradePrice || quote.Price <= 0m)
            {
                continue;
            }
            var ratio = (double)(quote.Price / tradePrice);
            if (ratio > PriceJumpHigh || ratio < PriceJumpLow)
            {
                suspicious.Add(T("{0} (quotazione {1} contro {2} dell'ultima operazione)", nameOf(h.Key), Fmt.Money4(quote.Price), Fmt.Money4(tradePrice)));
            }
        }
        findings.Add(suspicious.Count == 0
            ? new Finding("pricejump", FindingLevel.Ok, T("Prezzi coerenti con le operazioni"), T("Nessuna quotazione si scosta di oltre il 50% dall'ultima operazione recente."))
            : new Finding("pricejump", FindingLevel.Warning, T("Prezzi molto diversi dall'ultima operazione"),
                string.Join("; ", suspicious.Take(4)) + (suspicious.Count > 4 ? T("; e altri {0}", suspicious.Count - 4) : "") +
                T(". Può essere un cambio di valuta o un ticker sbagliato."), "securities"));

        // 6. Prezzi in valuta estera non ancora convertiti.
        var foreignQuotes = 0;
        var foreignAssets = new List<string>();
        foreach (var entry in catalog)
        {
            var count = loadQuotes(entry).Count(q => !CurrencyConverter.IsEur(q.Currency));
            if (count > 0)
            {
                foreignQuotes += count;
                foreignAssets.Add(entry.Name);
            }
        }
        findings.Add(foreignQuotes == 0
            ? new Finding("currency", FindingLevel.Ok, T("Prezzi tutti in euro"), T("Nessuna quotazione è rimasta in una valuta estera."))
            : new Finding("currency", FindingLevel.Warning, T("Prezzi in valuta estera da convertire"),
                T("{0} quotazioni non sono in euro ({1}) e vengono ignorate finché non sono convertite: serve la connessione internet per il cambio.", foreignQuotes, Names(foreignAssets)), "quotes"));

        // 7. Titoli per cui non è mai stato trovato un simbolo su Yahoo.
        var unresolved = priced.Where(h => h.AssetClass != "CRYPTO").Where(h =>
            catalog.FirstOrDefault(c => c.Key == h.Key) is { } c && !c.ProviderMappings.ContainsKey("yahoo") && !quotes.ContainsKey(h.Key)).ToList();
        if (unresolved.Count > 0)
        {
            findings.Add(new Finding("mapping", FindingLevel.Info, T("Titoli non trovati su Yahoo Finance"),
                T("{0} asset non hanno un simbolo di borsa: {1}. Potrebbero essere ritirati dal mercato.", unresolved.Count, Names(unresolved.Select(h => nameOf(h.Key)))), "quotes"));
        }

        // 8. Operazioni duplicate.
        var duplicates = operations.Where(o => !string.IsNullOrWhiteSpace(o.TransactionId)).GroupBy(o => o.TransactionId).Count(g => g.Count() > 1);
        findings.Add(duplicates == 0
            ? new Finding("duplicates", FindingLevel.Ok, T("Nessuna operazione duplicata"), T("Ogni identificativo di transazione compare una sola volta."))
            : new Finding("duplicates", FindingLevel.Warning, T("Operazioni duplicate"),
                T("{0} identificativi compaiono più volte: se hai unito due esportazioni, alcune operazioni sono contate due volte.", duplicates)));

        // 9. Quanto è recente il file.
        if (operations.Count > 0)
        {
            var last = operations.Max(o => o.Date);
            var days = (DateOnly.FromDateTime(now).DayNumber - last.DayNumber);
            findings.Add(days > 30
                ? new Finding("freshness", FindingLevel.Info, T("File non recente"), T("L'ultima operazione è del {0:dd/MM/yyyy} ({1} giorni fa): se nel frattempo hai operato, esporta un file nuovo.", last, days))
                : new Finding("freshness", FindingLevel.Ok, T("File recente"), T("L'ultima operazione è del {0:dd/MM/yyyy}.", last)));
        }

        return findings;
    }

    private static string Names(IEnumerable<string> names)
    {
        var list = names.ToList();
        var shown = string.Join(", ", list.Take(3));
        return list.Count > 3 ? T("{0} e altri {1}", shown, list.Count - 3) : shown;
    }
}
