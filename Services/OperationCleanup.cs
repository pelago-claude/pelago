using DPOper.Models;

namespace DPOper.Services;

/// <summary>
/// Toglie dai movimenti le righe che si annullano a vicenda, così i calcoli per titolo (cicli, costo fiscale, plusvalenze) non vedono vendite
/// o acquisti mai avvenuti:
/// <list type="bullet">
/// <item><c>MERGER</c> e <c>MERGER_CANCELLED</c> (fusione di un fondo e relativo storno): spostano le quote da un codice all'altro senza cambiare il costo
/// e, nel file, si compensano; se restassero verrebbero lette come una vendita a ricavo zero e un acquisto gratuito.</item>
/// <item><c>SELL_CANCELLED</c>: storno di una vendita (di solito riproposta poco dopo). Si tolgono lo storno e la vendita che annulla, riconosciuta dallo stesso titolo,
/// dalle stesse quote e dallo stesso importo.</item>
/// </list>
/// La liquidità e le quote possedute non cambiano: le due righe di ogni coppia si compensano.
/// </summary>
public static class OperationCleanup
{
    private const decimal ShareTolerance = 0.000000001m;
    private const decimal AmountTolerance = 0.005m;

    public static List<Operation> WithoutReversals(IEnumerable<Operation> operations)
    {
        var all = operations as List<Operation> ?? operations.ToList();
        if (!all.Any(o => o.Type is "MERGER" or "MERGER_CANCELLED" or "SELL_CANCELLED"))
        {
            return all;
        }

        var drop = new HashSet<Operation>(ReferenceEqualityComparer.Instance);
        foreach (var op in all.Where(o => o.Type is "MERGER" or "MERGER_CANCELLED"))
        {
            drop.Add(op);
        }

        foreach (var cancel in all.Where(o => o.Type == "SELL_CANCELLED").OrderBy(o => o.DateTime))
        {
            drop.Add(cancel);
            var original = all
                .Where(o => o.Type == "SELL" && !drop.Contains(o) && o.PositionKey == cancel.PositionKey && o.DateTime <= cancel.DateTime
                    && Math.Abs((o.Shares ?? 0m) + (cancel.Shares ?? 0m)) <= ShareTolerance
                    && Math.Abs((o.Amount ?? 0m) + (cancel.Amount ?? 0m)) <= AmountTolerance)
                .OrderByDescending(o => o.DateTime)
                .FirstOrDefault();
            if (original is not null)
            {
                drop.Add(original);
            }
        }

        return all.Where(o => !drop.Contains(o)).ToList();
    }
}
