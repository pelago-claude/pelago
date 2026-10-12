using DPOper.Models;

namespace DPOper.Services;

/// <summary>
/// Una singola cessione (vendita, esercizio di warrant, ecc.) con il relativo utile/perdita
/// realizzato, calcolato a costo medio ponderato.
/// </summary>
public sealed record RealizedEvent(
    DateTime Date,
    string Name,
    string Symbol,
    string AssetClass,
    string Type,
    string Description,
    decimal SharesDisposed,
    decimal Proceeds,
    decimal CostBasis)
{
    public decimal Gain => Proceeds - CostBasis;
}

/// <summary>Quantità ancora posseduta di un asset e costo totale ancora "a carico" (costo medio ponderato).</summary>
public sealed record OpenPosition(decimal Shares, decimal Cost)
{
    public decimal AverageCost => Shares > 0m ? Cost / Shares : 0m;
}

/// <summary>
/// Calcola le plusvalenze/minusvalenze realizzate su ogni cessione, con il metodo del costo medio
/// ponderato (non FIFO): ogni acquisizione di quantità (BUY, ricevute gratuite, ecc.) aumenta la
/// posizione e il suo costo medio; ogni cessione (SELL, esercizio di warrant, ecc.) scarica quote
/// al costo medio corrente e ne registra l'utile/perdita.
///
/// Gli eventi societari che riducono le quote senza un incasso registrato nell'export (es. un
/// warrant esercitato/scaduto) vengono trattati come una cessione a ricavo zero: il costo residuo
/// diventa una minusvalenza realizzata. È una semplificazione dichiarata, non un calcolo fiscale.
///
/// Lo stesso scorrimento fornisce, per ogni posizione ancora aperta, la quantità e il costo residuo
/// (<see cref="ComputeOpenPositions"/>), da cui si ricava la plus/minusvalenza "latente".
/// </summary>
public static class RealizedGainsCalculator
{
    public static List<RealizedEvent> Compute(IEnumerable<Operation> operations)
    {
        var events = new List<RealizedEvent>();
        foreach (var group in GroupByPosition(operations))
        {
            Walk(group, events);
        }
        return events.OrderByDescending(e => e.Date).ToList();
    }

    /// <summary>Per ogni asset: quantità posseduta oggi e costo medio ponderato di quanto resta (solo posizioni con quantità &gt; 0).</summary>
    public static Dictionary<string, OpenPosition> ComputeOpenPositions(IEnumerable<Operation> operations)
    {
        var result = new Dictionary<string, OpenPosition>();
        foreach (var group in GroupByPosition(operations))
        {
            var position = Walk(group, null);
            if (position.Shares > 0m)
            {
                result[group.Key] = position;
            }
        }
        return result;
    }

    private static IEnumerable<IGrouping<string, Operation>> GroupByPosition(IEnumerable<Operation> operations) => OperationCleanup.WithoutReversals(operations)
        .Where(o => !string.IsNullOrWhiteSpace(o.PositionKey) && !string.IsNullOrWhiteSpace(o.AssetClass))
        .GroupBy(o => o.PositionKey);

    private static OpenPosition Walk(IEnumerable<Operation> group, List<RealizedEvent>? events)
    {
        var shares = 0m;
        var cost = 0m;

        foreach (var op in group.OrderBy(o => o.DateTime))
        {
            var delta = op.Shares ?? 0m;
            if (delta == 0m)
            {
                continue;
            }

            if (delta > 0m)
            {
                var acquisitionCost = op.NetCashImpact < 0m ? -op.NetCashImpact : 0m;
                shares += delta;
                cost += acquisitionCost;
                continue;
            }

            var sharesSold = -delta;
            var avgCost = shares > 0m ? cost / shares : 0m;
            var costBasis = Math.Min(cost, avgCost * sharesSold);
            var proceeds = op.NetCashImpact;

            events?.Add(new RealizedEvent(op.DateTime, op.Name, op.Symbol, op.AssetClass, op.Type,
                op.Description, sharesSold, proceeds, costBasis));

            shares = Math.Max(0m, shares - sharesSold);
            cost = Math.Max(0m, cost - costBasis);
        }

        return new OpenPosition(shares, cost);
    }
}
