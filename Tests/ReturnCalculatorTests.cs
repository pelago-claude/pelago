using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class ReturnCalculatorTests
{
    private static DailySnapshot Snap(string date, decimal total, decimal contributions) =>
        new(DateOnly.Parse(date), total, 0m, 0m, 0m, contributions);

    private static readonly DateOnly YearStart = DateOnly.Parse("2025-12-31");
    private static readonly DateOnly YearEnd = DateOnly.Parse("2026-12-31");

    [Fact]
    public void Twr_ignores_deposits()
    {
        // 1000 versati, +10%; poi altri 1000 versati (nessun rendimento quel giorno); poi +10% su 2100 -> 2310.
        var snaps = new[]
        {
            Snap("2026-01-01", 1000m, 1000m),
            Snap("2026-02-01", 1100m, 1000m),
            Snap("2026-03-01", 2100m, 2000m),
            Snap("2026-04-01", 2310m, 2000m),
        };

        var twr = ReturnCalculator.Twr(snaps, YearStart, YearEnd);

        Assert.NotNull(twr);
        Assert.Equal(0.21, twr!.Value, 6); // 1,10 x 1,10 - 1
    }

    [Fact]
    public void Huge_deposit_into_a_small_account_never_gives_an_impossible_return()
    {
        // 20.000 nel conto, poi 296.000 versati nello stesso giorno e investiti in titoli che a fine giornata valgono meno:
        // un rendimento ponderato nel tempo non puo' scendere sotto -100%, e qui la perdita del giorno e' relativa al capitale versato.
        var snaps = new[]
        {
            Snap("2026-01-06", 20000m, 20000m),
            Snap("2026-01-07", 270000m, 316000m),
        };

        var twr = ReturnCalculator.Twr(snaps, YearStart, YearEnd)!.Value;

        Assert.InRange(twr, -1.0, 0.0);
        Assert.Equal(-46000.0 / (20000.0 + 148000.0), twr, 6);   // (270.000 - 20.000 - 296.000) / (20.000 + meta' di 296.000)
    }

    [Fact]
    public void Twr_is_zero_when_value_only_grows_by_deposits()
    {
        var snaps = new[] { Snap("2026-01-01", 1000m, 1000m), Snap("2026-02-01", 2000m, 2000m) };

        Assert.Equal(0.0, ReturnCalculator.Twr(snaps, YearStart, YearEnd)!.Value, 9);
    }

    [Fact]
    public void Twr_is_null_without_capital()
    {
        var snaps = new[] { Snap("2026-01-01", 0m, 0m), Snap("2026-02-01", 0m, 0m) };

        Assert.Null(ReturnCalculator.Twr(snaps, YearStart, YearEnd));
    }

    [Fact]
    public void Xirr_of_one_year_ten_percent_is_ten_percent()
    {
        var flows = new[] { (DateOnly.Parse("2025-01-01"), -1000m), (DateOnly.Parse("2026-01-01"), 1100m) };

        var rate = ReturnCalculator.Xirr(flows);

        Assert.NotNull(rate);
        Assert.Equal(0.10, rate!.Value, 5);
    }

    [Fact]
    public void Xirr_weights_by_date_unlike_twr()
    {
        // Lo stesso guadagno in meno tempo equivale a un tasso annuo piu' alto.
        var early = ReturnCalculator.Xirr(new[] { (DateOnly.Parse("2025-01-01"), -1000m), (DateOnly.Parse("2026-01-01"), 1050m) });
        var late = ReturnCalculator.Xirr(new[] { (DateOnly.Parse("2025-07-01"), -1000m), (DateOnly.Parse("2026-01-01"), 1050m) });

        Assert.True(late > early);
    }

    [Fact]
    public void Xirr_is_null_without_sign_change()
    {
        Assert.Null(ReturnCalculator.Xirr(new[] { (DateOnly.Parse("2026-01-01"), -1000m), (DateOnly.Parse("2026-06-01"), -50m) }));
    }

    [Fact]
    public void Period_reports_gain_as_value_minus_deposits()
    {
        var snaps = new[] { Snap("2026-01-02", 1000m, 1000m), Snap("2026-06-30", 1500m, 1200m) };
        var ops = new[] { Ops.Transfer("2026-01-02", 1000m), Ops.Transfer("2026-03-01", 200m) };

        var period = ReturnCalculator.Period("2026", snaps, ops, YearStart, YearEnd);

        Assert.Equal(1200m, period.NetDeposits);
        Assert.Equal(1500m, period.EndValue);
        Assert.Equal(300m, period.Gain);
    }

    [Fact]
    public void Category_return_uses_purchases_as_flows()
    {
        // Titoli: acquisto di 500 (e' un flusso, non un rendimento), poi il valore sale a 550: +10%.
        var snaps = new[]
        {
            new DailySnapshot(DateOnly.Parse("2026-01-02"), 500m, 500m, 0m, 0m, 1000m),
            new DailySnapshot(DateOnly.Parse("2026-02-02"), 450m, 550m, 0m, 0m, 1000m),
        };
        var ops = new[] { Ops.Buy("2026-01-02", "AAA", "STOCK", 50, 10m) };

        var securities = ReturnCalculator.ByCategory(snaps, ops).First();

        Assert.Equal(0.10, securities.Twr!.Value, 6);
    }
}
