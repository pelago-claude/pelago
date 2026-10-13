using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class TradingStatsTests
{
    // mezzogiorno UTC: lo stesso giorno in tutti i fusi orari dell'Europa
    private static DateTime Noon(string date) => DateTime.Parse(date + "T12:00:00", null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    private static PositionCycle Cycle(string closed, decimal invested, decimal proceeds, decimal fees = 1m, string? opened = null) =>
        new(1, Noon(opened ?? closed), Noon(closed), 2, invested, proceeds, fees, 0m, false);

    private static ClosedPosition Position(string name, params PositionCycle[] cycles) =>
        new("KEY-" + name, name, "DERIVATIVE", cycles, true);

    [Fact]
    public void Basic_numbers_win_rate_payoff_profit_factor_and_expectancy()
    {
        var stats = TradingStatsCalculator.Compute(new[]
        {
            Position("A", Cycle("2026-03-02", 100m, 200m), Cycle("2026-03-03", 100m, 150m)),     // +100, +50
            Position("B", Cycle("2026-03-04", 100m, 70m), Cycle("2026-03-05", 100m, 80m)),       // -30, -20
        });

        Assert.Equal(4, stats.Trades);
        Assert.Equal(2, stats.Winners);
        Assert.Equal(2, stats.Losers);
        Assert.Equal(0.5, stats.WinRate);
        Assert.Equal(100m, stats.TotalResult);
        Assert.Equal(75m, stats.AverageWin);
        Assert.Equal(-25m, stats.AverageLoss);
        Assert.Equal(3.0, stats.Payoff);                    // 75 / 25
        Assert.Equal(3.0, stats.ProfitFactor);              // 150 / 50
        Assert.Equal(25m, stats.Expectancy);
        Assert.Equal(104m, stats.GrossResult);              // risultato più le commissioni (4 x 1)
        Assert.Equal(4m, stats.Fees);
    }

    [Fact]
    public void No_trades_gives_zeros_without_dividing_by_zero()
    {
        var stats = TradingStatsCalculator.Compute(Array.Empty<ClosedPosition>());

        Assert.Equal(0, stats.Trades);
        Assert.Null(stats.WinRate);
        Assert.Null(stats.Payoff);
        Assert.Null(stats.ProfitFactor);
        Assert.Equal(0m, stats.Expectancy);
        Assert.Null(stats.Best);
        Assert.Empty(stats.ByMonth);
    }

    [Fact]
    public void Only_winners_have_no_payoff_or_profit_factor()
    {
        var stats = TradingStatsCalculator.Compute(new[] { Position("A", Cycle("2026-03-02", 100m, 150m)) });

        Assert.Equal(1.0, stats.WinRate);
        Assert.Null(stats.Payoff);
        Assert.Null(stats.ProfitFactor);
    }

    [Fact]
    public void Streaks_follow_closing_order_and_a_tie_breaks_both()
    {
        var stats = TradingStatsCalculator.Compute(new[]
        {
            Position("A",
                Cycle("2026-03-02", 100m, 110m), Cycle("2026-03-03", 100m, 110m), Cycle("2026-03-04", 100m, 110m),   // 3 vinte
                Cycle("2026-03-05", 100m, 100m),                                                                   // pareggio
                Cycle("2026-03-06", 100m, 90m), Cycle("2026-03-09", 100m, 90m)),                                   // 2 perse
        });

        Assert.Equal(3, stats.LongestWinStreak);
        Assert.Equal(2, stats.LongestLossStreak);
    }

    [Fact]
    public void Results_are_grouped_by_weekday_and_month_and_accumulate_over_time()
    {
        var stats = TradingStatsCalculator.Compute(new[]
        {
            Position("A", Cycle("2026-03-02", 100m, 150m), Cycle("2026-03-09", 100m, 80m), Cycle("2026-04-01", 100m, 120m)),  // lunedì, lunedì, mercoledì
        });

        var monday = stats.ByWeekday.First(b => b.Label == "Lunedì");
        Assert.Equal(2, monday.Trades);
        Assert.Equal(30m, monday.Result);
        Assert.Equal(new[] { "2026-03", "2026-04" }, stats.ByMonth.Select(b => b.Label));
        Assert.Equal(new[] { 50m, 30m, 50m }, stats.Cumulative.Select(c => c.Cumulative));
    }

    [Fact]
    public void The_year_filter_keeps_only_trades_closed_in_that_year()
    {
        var all = new[] { Position("A", Cycle("2025-12-30", 100m, 150m), Cycle("2026-01-05", 100m, 80m)) };

        Assert.Equal(1, TradingStatsCalculator.Compute(all, 2025).Trades);
        Assert.Equal(-20m, TradingStatsCalculator.Compute(all, 2026).TotalResult);
        Assert.Equal(2, TradingStatsCalculator.Compute(all).Trades);
    }

    [Fact]
    public void Products_on_the_same_underlying_are_merged_and_duration_counts_intraday()
    {
        var stats = TradingStatsCalculator.Compute(new[]
        {
            Position("Turbo auf ORACLE (Long 120 $)", Cycle("2026-03-02", 100m, 150m)),
            Position("Turbo auf ORACLE (Long 130 $)", Cycle("2026-03-03", 100m, 60m, opened: "2026-03-01")),
            Position("Turbo auf INTEL (Long 20 $)", Cycle("2026-03-04", 100m, 120m)),
        });

        var oracle = stats.ByUnderlying.Single(u => u.Name == "Turbo auf ORACLE");
        Assert.Equal(2, oracle.Trades);
        Assert.Equal(10m, oracle.Result);
        Assert.Equal("Turbo auf INTEL", stats.ByUnderlying[0].Name);        // ordinati per risultato
        Assert.Equal(2, stats.IntradayTrades);                              // due su tre aperte e chiuse nello stesso giorno
        Assert.Equal(2d / 3d, stats.AverageDaysHeld, 6);                    // (0 + 2 + 0) / 3
    }
}
