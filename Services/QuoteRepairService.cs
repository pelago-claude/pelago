using DPOper.Models;
using static DPOper.Services.Loc;

namespace DPOper.Services;

/// <summary>
/// Ripara le quotazioni salvate in passato con una valuta diversa dall'euro e usate come se fossero euro
/// (esempio: un ETF quotato a Londra in pence, 4.433 GBp, letto come 4.433 €). Converte ogni prezzo con il cambio del
/// giorno della quotazione (Frankfurter) e conserva il prezzo originale. Se il cambio non è raggiungibile non tocca nulla
/// e riprova al prossimo avvio.
/// </summary>
public static class QuoteRepairService
{
    public static async Task<int> RepairAsync(IEnumerable<AssetCatalogEntry> catalog, Action<string>? log, CancellationToken cancellationToken)
    {
        var repaired = 0;
        using var fx = new ExchangeRateProvider();

        foreach (var entry in catalog)
        {
            var quotes = QuoteHistoryStore.Load(entry);
            var foreign = quotes.Where(q => !CurrencyConverter.IsEur(q.Currency)).ToList();
            if (foreign.Count == 0)
            {
                continue;
            }

            var changed = 0;
            foreach (var quote in foreign)
            {
                try
                {
                    var (price, currency) = CurrencyConverter.NormalizeMinorUnit(quote.Price, quote.Currency);
                    var rate = await fx.GetRateAtAsync(currency, "EUR", DateOnly.FromDateTime(quote.Timestamp), cancellationToken);
                    quote.OriginalPrice = quote.Price;
                    quote.OriginalCurrency = quote.Currency;
                    quote.Price = Math.Round(price * rate, 6);
                    quote.Currency = "EUR";
                    changed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log?.Invoke(T("Prezzo di {0} non riparato ({1}): {2}", entry.Name, quote.Currency, ex.Message));
                }
            }

            if (changed > 0)
            {
                QuoteHistoryStore.Save(entry, quotes);
                repaired += changed;
                log?.Invoke(T("Corretto {0}: {1} prezzi convertiti in euro.", entry.Name, changed));
            }
        }

        return repaired;
    }
}
