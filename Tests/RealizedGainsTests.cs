using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class RealizedGainsTests
{
    private static Pelago.Models.Operation[] Scenario() => new[]
    {
        Ops.Buy("2026-01-10", "AAA", "STOCK", 10, 10m, fee: 1m),   // costo 101
        Ops.Buy("2026-02-10", "AAA", "STOCK", 10, 20m, fee: 1m),   // costo 201: in tutto 302 per 20 quote = 15,10 l'una
        Ops.Sell("2026-03-10", "AAA", "STOCK", 10, 30m, fee: 1m),  // incasso 299
    };

    [Fact]
    public void Sale_uses_weighted_average_cost_including_fees()
    {
        var sale = Assert.Single(RealizedGainsCalculator.Compute(Scenario()));

        Assert.Equal(151m, sale.CostBasis);          // 15,10 x 10
        Assert.Equal(299m, sale.Proceeds);
        Assert.Equal(148m, sale.Gain);
    }

    [Fact]
    public void Open_position_keeps_remaining_shares_and_cost()
    {
        var position = Assert.Single(RealizedGainsCalculator.ComputeOpenPositions(Scenario())).Value;

        Assert.Equal(10m, position.Shares);
        Assert.Equal(151m, position.Cost);
        Assert.Equal(15.1m, position.AverageCost);
    }

    [Fact]
    public void Closed_position_is_not_listed_as_open()
    {
        var ops = new[]
        {
            Ops.Buy("2026-01-10", "AAA", "STOCK", 5, 10m),
            Ops.Sell("2026-02-10", "AAA", "STOCK", 5, 12m),
        };

        Assert.Empty(RealizedGainsCalculator.ComputeOpenPositions(ops));
    }

    [Fact]
    public void Free_shares_dilute_the_average_cost()
    {
        var gift = new Pelago.Models.Operation
        {
            DateTime = Ops.At("2026-02-01"), Date = DateOnly.Parse("2026-02-01"), Category = "DELIVERY", Type = "FREE_RECEIPT",
            AssetClass = "CRYPTO", Symbol = "BTC", Name = "Bitcoin", Shares = 1m, Amount = 0m,
        };
        var ops = new[] { Ops.Buy("2026-01-10", "BTC", "CRYPTO", 1, 100m), gift };

        var position = Assert.Single(RealizedGainsCalculator.ComputeOpenPositions(ops)).Value;

        Assert.Equal(2m, position.Shares);
        Assert.Equal(100m, position.Cost);
        Assert.Equal(50m, position.AverageCost);
    }
}
