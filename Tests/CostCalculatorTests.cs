using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class CostCalculatorTests
{
    [Fact]
    public void Costs_are_split_by_kind_and_reported_as_paid()
    {
        var ops = new List<Operation>
        {
            Ops.Buy("2026-01-10", "AAA", "STOCK", 10, 10m, fee: 1m),
            Ops.Sell("2026-02-10", "AAA", "STOCK", 10, 12m, fee: 1m, tax: 5m),
            Ops.Interest("2026-03-01", 10m, 2.6m),
            new() { DateTime = Ops.At("2026-04-01"), Date = DateOnly.Parse("2026-04-01"), Category = "CASH", Type = "TAX", Tax = -3m },
            new() { DateTime = Ops.At("2026-05-01"), Date = DateOnly.Parse("2026-05-01"), Category = "CASH", Type = "TAX_OPTIMIZATION", Tax = 4m },
        };

        var total = CostCalculator.Total(ops);

        Assert.Equal(2m, total.Fees);
        Assert.Equal(5m, total.TradeTax);
        Assert.Equal(2.6m, total.InterestTax);
        Assert.Equal(3m, total.StampAndRecalculation);
        Assert.Equal(-4m, total.TaxOptimization);       // un rimborso e' un costo negativo
        Assert.Equal(6.6m, total.NetTax);
        Assert.Equal(8.6m, total.Total);
    }

    [Fact]
    public void ByYear_groups_by_calendar_year()
    {
        var ops = new List<Operation>
        {
            Ops.Buy("2025-12-30", "AAA", "STOCK", 1, 10m, fee: 1m),
            Ops.Buy("2026-01-02", "AAA", "STOCK", 1, 10m, fee: 2m),
        };

        var rows = CostCalculator.ByYear(ops);

        Assert.Equal(new[] { "2025", "2026" }, rows.Select(r => r.Period));
        Assert.Equal(1m, rows[0].Fees);
        Assert.Equal(2m, rows[1].Fees);
    }
}
