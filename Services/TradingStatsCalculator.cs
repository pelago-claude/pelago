namespace Pelago.Services;

/// <summary>Un'operazione completa (un ciclo di acquisto e vendita) con il titolo a cui appartiene.</summary>
public sealed record TradeRef(string Key, string Name, string Underlying, string AssetClass, PositionCycle Cycle)
{
    public decimal Result => Cycle.Result;

    /// <summary>Giorno di chiusura, nell'ora locale.</summary>
    public DateOnly ClosedOn => DateOnly.FromDateTime(Cycle.Closed.ToLocalTime());
}

/// <summary>Un gruppo di operazioni (un giorno della settimana, un mese, un sottostante) con i suoi numeri.</summary>
public sealed record BucketStat(string Label, int Trades, int Winners, decimal Result, decimal SumWins = 0m, decimal SumLosses = 0m)
{
    public double? WinRate => Trades == 0 ? null : (double)Winners / Trades;
}

public sealed record UnderlyingStat(string Name, int Trades, int Winners, decimal Result, decimal Fees)
{
    public double? WinRate => Trades == 0 ? null : (double)Winners / Trades;
}

/// <summary>I numeri di sintesi di chi fa molte operazioni: quanto spesso vince, quanto vince e perde in media, quanto dura una posizione, quando guadagna.</summary>
public sealed record TradingStats(
    int Trades,
    int Winners,
    int Losers,
    decimal TotalResult,
    decimal GrossResult,
    decimal Fees,
    decimal Taxes,
    decimal SumWins,
    decimal SumLosses,
    TradeRef? Best,
    TradeRef? Worst,
    double AverageDaysHeld,
    int IntradayTrades,
    int LongestWinStreak,
    int LongestLossStreak,
    IReadOnlyList<BucketStat> ByWeekday,
    IReadOnlyList<BucketStat> ByMonth,
    IReadOnlyList<(DateOnly Date, decimal Result)> Daily,
    IReadOnlyList<(DateOnly Date, decimal Cumulative)> Cumulative,
    IReadOnlyList<UnderlyingStat> ByUnderlying)
{
    public double? WinRate => Trades == 0 ? null : (double)Winners / Trades;

    /// <summary>Guadagno medio delle operazioni in guadagno.</summary>
    public decimal AverageWin => Winners == 0 ? 0m : SumWins / Winners;

    /// <summary>Perdita media delle operazioni in perdita (negativa).</summary>
    public decimal AverageLoss => Losers == 0 ? 0m : SumLosses / Losers;

    /// <summary>Quanto vale in media un guadagno rispetto a una perdita (guadagno medio diviso perdita media).</summary>
    public double? Payoff => AverageLoss == 0m ? null : (double)(AverageWin / -AverageLoss);

    /// <summary>Somma dei guadagni divisa per la somma delle perdite: sopra 1 si guadagna in tutto.</summary>
    public double? ProfitFactor => SumLosses == 0m ? null : (double)(SumWins / -SumLosses);

    /// <summary>Risultato medio di una singola operazione.</summary>
    public decimal Expectancy => Trades == 0 ? 0m : TotalResult / Trades;
}

/// <summary>
/// Statistiche di trading sulle operazioni chiuse. Ogni «operazione» è un ciclo completo di una posizione (la quantità sale da zero e torna a zero):
/// stessa unità di <see cref="ClosedPositionCalculator"/>, quindi compresi i cicli di titoli oggi riaperti e i turbo azzerati senza vendita registrata.
/// </summary>
public static class TradingStatsCalculator
{
    private static readonly string[] WeekdayNames = { "Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato", "Domenica" };

    public static IEnumerable<TradeRef> Trades(IEnumerable<ClosedPosition> positions) => positions
        .SelectMany(p => p.Cycles.Select(c => new TradeRef(p.Key, p.Name, ClosedPositionCalculator.Underlying(p.Name), p.AssetClass, c)));

    /// <param name="positions">Le posizioni con i loro cicli (vedi <see cref="ClosedPositionCalculator.Compute"/>), anche quelle oggi riaperte.</param>
    /// <param name="year">Se indicato, solo le operazioni chiuse in quell'anno.</param>
    public static TradingStats Compute(IEnumerable<ClosedPosition> positions, int? year = null)
    {
        var trades = Trades(positions)
            .Where(t => year is null || t.ClosedOn.Year == year)
            .OrderBy(t => t.Cycle.Closed)
            .ToList();

        var winners = trades.Where(t => t.Result > 0m).ToList();
        var losers = trades.Where(t => t.Result < 0m).ToList();

        var (longestWin, longestLoss) = Streaks(trades);

        // giorni della settimana: lunedì per primo; solo quelli in cui c'è almeno un'operazione
        var byWeekday = trades.GroupBy(t => ((int)t.ClosedOn.DayOfWeek + 6) % 7).OrderBy(g => g.Key)
            .Select(g => new BucketStat(WeekdayNames[g.Key], g.Count(), g.Count(t => t.Result > 0m), g.Sum(t => t.Result))).ToList();

        var byMonth = trades.GroupBy(t => (t.ClosedOn.Year, t.ClosedOn.Month)).OrderBy(g => g.Key)
            .Select(g => new BucketStat($"{g.Key.Year:0000}-{g.Key.Month:00}", g.Count(), g.Count(t => t.Result > 0m), g.Sum(t => t.Result),
                g.Where(t => t.Result > 0m).Sum(t => t.Result), g.Where(t => t.Result < 0m).Sum(t => t.Result))).ToList();

        var daily = trades.GroupBy(t => t.ClosedOn).OrderBy(g => g.Key).Select(g => (g.Key, g.Sum(t => t.Result))).ToList();

        var running = 0m;
        var cumulative = daily.Select(d => (d.Key, running += d.Item2)).ToList();

        var byUnderlying = trades.GroupBy(t => t.Underlying)
            .Select(g => new UnderlyingStat(g.Key, g.Count(), g.Count(t => t.Result > 0m), g.Sum(t => t.Result), g.Sum(t => t.Cycle.Fees)))
            .OrderByDescending(u => u.Result).ToList();

        return new TradingStats(
            trades.Count,
            winners.Count,
            losers.Count,
            trades.Sum(t => t.Result),
            trades.Sum(t => t.Cycle.GrossResult),
            trades.Sum(t => t.Cycle.Fees),
            trades.Sum(t => t.Cycle.TaxesPaid),
            winners.Sum(t => t.Result),
            losers.Sum(t => t.Result),
            trades.Count == 0 ? null : trades.MaxBy(t => t.Result),
            trades.Count == 0 ? null : trades.MinBy(t => t.Result),
            trades.Count == 0 ? 0d : trades.Average(t => (double)t.Cycle.DaysHeld),
            trades.Count(t => t.Cycle.DaysHeld == 0),
            longestWin,
            longestLoss,
            byWeekday,
            byMonth,
            daily,
            cumulative,
            byUnderlying);
    }

    /// <summary>Le serie più lunghe di operazioni di fila in guadagno e in perdita, in ordine di chiusura (un pareggio interrompe entrambe).</summary>
    private static (int Win, int Loss) Streaks(IEnumerable<TradeRef> orderedTrades)
    {
        int win = 0, loss = 0, bestWin = 0, bestLoss = 0;
        foreach (var t in orderedTrades)
        {
            if (t.Result > 0m)
            {
                win++;
                loss = 0;
            }
            else if (t.Result < 0m)
            {
                loss++;
                win = 0;
            }
            else
            {
                win = loss = 0;
            }
            bestWin = Math.Max(bestWin, win);
            bestLoss = Math.Max(bestLoss, loss);
        }
        return (bestWin, bestLoss);
    }
}
