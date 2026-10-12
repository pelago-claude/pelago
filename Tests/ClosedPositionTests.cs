using DPOper.Models;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class ClosedPositionTests
{
    private static Operation Trade(string when, string type, decimal shares, decimal amount, decimal fee = 0m, decimal tax = 0m, string symbol = "AAA", string cls = "STOCK", string name = "Alfa", string description = "") => new()
    {
        DateTime = DateTime.Parse(when, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
        Date = DateOnly.Parse(when[..10]),
        Category = "TRADING",
        Type = type,
        AssetClass = cls,
        Name = name,
        Symbol = symbol,
        Shares = shares,
        Amount = amount,
        Fee = fee == 0m ? null : fee,
        Tax = tax == 0m ? null : tax,
        Description = description,
    };

    [Fact]
    public void A_simple_round_trip_is_one_closed_cycle_with_net_result()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m, fee: -1m),
            Trade("2026-03-10T10:00:00", "SELL", -10m, 1200m, fee: -1m, tax: -20m),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.True(position.IsClosed);
        var cycle = Assert.Single(position.Cycles);
        Assert.Equal(1001m, cycle.Invested);          // 1000 + commissione
        Assert.Equal(1179m, cycle.Proceeds);          // 1200 - commissione - imposta trattenuta
        Assert.Equal(178m, cycle.Result);
        Assert.Equal(2m, cycle.Fees);
        Assert.Equal(20m, cycle.TaxesPaid);           // imposte già pagate al broker
        Assert.Equal(200m, cycle.GrossResult);        // prima di commissioni e imposte
        Assert.Equal(64, cycle.DaysHeld);
        Assert.Equal(2, cycle.Trades);
    }

    [Fact]
    public void A_title_bought_again_and_still_held_keeps_its_closed_cycle_but_is_not_closed()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m),
            Trade("2026-02-05T10:00:00", "SELL", -10m, 1100m),       // +100
            Trade("2026-04-01T10:00:00", "BUY", 5m, -600m),          // ancora in portafoglio
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.False(position.IsClosed);
        var cycle = Assert.Single(position.Cycles);
        Assert.Equal(100m, cycle.Result);
        Assert.Equal(100m, position.Result);                          // conta solo il ciclo concluso
    }

    [Fact]
    public void A_merger_and_its_reversal_leave_one_clean_cycle()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2026-03-10T10:00:00", "MERGER", -10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2026-03-10T10:00:00", "MERGER", 10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2026-03-13T10:00:00", "MERGER_CANCELLED", 10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2026-03-13T10:00:00", "MERGER_CANCELLED", -10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2026-06-10T10:00:00", "SELL", -10m, 1200m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        var cycle = Assert.Single(position.Cycles);
        Assert.Equal(1000m, cycle.Invested);
        Assert.Equal(1200m, cycle.Proceeds);
        Assert.Equal(2, cycle.Trades);
    }

    [Fact]
    public void A_cancelled_sale_does_not_close_the_position_twice()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m),
            Trade("2026-02-01T10:00:00", "SELL", -10m, 1300m),
            Trade("2026-02-04T10:00:00", "SELL_CANCELLED", 10m, -1300m),
            Trade("2026-02-04T11:00:00", "SELL", -10m, 1310m),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        var cycle = Assert.Single(position.Cycles);
        Assert.Equal(310m, cycle.Result);
    }

    [Fact]
    public void A_warrant_settlement_is_counted_in_one_cycle_only()
    {
        const string isin = "DE000TURBO01";
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 100m, -300m, symbol: isin, cls: "DERIVATIVE", name: "Turbo X"),
            Trade("2026-02-19T10:00:00", "WARRANT_EXERCISE", -100m, 0m, symbol: isin, cls: "DERIVATIVE", name: "Turbo X"),
            Trade("2026-02-20T10:00:00", "TILG", 0m, 50m, symbol: "", cls: "DERIVATIVE", name: "Turbo X", description: "Auszahlung " + isin),
            Trade("2026-02-25T10:00:00", "BUY", 100m, -200m, symbol: isin, cls: "DERIVATIVE", name: "Turbo X"),
            Trade("2026-03-19T10:00:00", "WARRANT_EXERCISE", -100m, 0m, symbol: isin, cls: "DERIVATIVE", name: "Turbo X"),
            Trade("2026-03-20T10:00:00", "TILG", 0m, 70m, symbol: "", cls: "DERIVATIVE", name: "Turbo X", description: "Auszahlung " + isin),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.Equal(new[] { 50m, 70m }, position.Cycles.Select(c => c.Proceeds));   // ognuno al suo ciclo, nessun doppio conteggio
    }

    [Fact]
    public void A_position_still_open_is_not_closed_and_has_no_cycles()
    {
        var ops = new[] { Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m) };

        Assert.Empty(ClosedPositionCalculator.Compute(ops));
    }

    [Fact]
    public void Buying_again_after_closing_starts_a_second_cycle_and_totals_add_up()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m),
            Trade("2026-02-05T10:00:00", "SELL", -10m, 1100m),       // +100
            Trade("2026-04-01T10:00:00", "BUY", 5m, -600m),
            Trade("2026-05-01T10:00:00", "SELL", -5m, 550m),         // -50
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.Equal(2, position.Cycles.Count);
        Assert.Equal(new[] { 100m, -50m }, position.Cycles.Select(c => c.Result));
        Assert.Equal(50m, position.Result);                          // quanto hai fatto in tutto su quel titolo
        Assert.Equal(new[] { 1, 2 }, position.Cycles.Select(c => c.Number));
    }

    [Fact]
    public void Partial_sales_stay_in_the_same_cycle_until_the_quantity_is_zero()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m),
            Trade("2026-02-05T10:00:00", "SELL", -4m, 480m),
            Trade("2026-03-05T10:00:00", "SELL", -6m, 720m),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        var cycle = Assert.Single(position.Cycles);
        Assert.Equal(200m, cycle.Result);
        Assert.Equal(3, cycle.Trades);
    }

    [Fact]
    public void An_open_cycle_after_a_closed_one_is_kept_but_the_position_is_not_closed()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "BUY", 10m, -1000m),
            Trade("2026-02-05T10:00:00", "SELL", -10m, 1100m),
            Trade("2026-04-01T10:00:00", "BUY", 5m, -600m),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.False(position.IsClosed);
        Assert.Single(position.Cycles);
        Assert.Equal(100m, position.Result);
    }

    [Fact]
    public void A_worthless_open_derivative_counts_as_knocked_out_at_a_loss()
    {
        var ops = new[] { Trade("2026-06-01T10:00:00", "BUY", 1000m, -800m, symbol: "DE000X", cls: "DERIVATIVE", name: "Long 100 $") };

        var closed = ClosedPositionCalculator.Compute(ops, key => key == "DE000X" ? 0m : null);

        var position = Assert.Single(closed);
        Assert.True(position.IsClosed);
        Assert.True(position.KnockedOut);
        Assert.Equal(-800m, position.Result);
    }

    [Fact]
    public void A_derivative_with_value_is_not_knocked_out()
    {
        var ops = new[] { Trade("2026-06-01T10:00:00", "BUY", 1000m, -800m, symbol: "DE000X", cls: "DERIVATIVE") };

        Assert.Empty(ClosedPositionCalculator.Compute(ops, _ => 0.55m));
    }

    [Fact]
    public void A_warrant_exercise_with_cash_settlement_adds_the_proceeds_to_the_cycle()
    {
        var ops = new List<Operation>
        {
            Trade("2026-06-01T10:00:00", "BUY", 1000m, -800m, symbol: "DE000X", cls: "DERIVATIVE"),
            Trade("2026-06-20T10:00:00", "WARRANT_EXERCISE", -1000m, 0m, symbol: "DE000X", cls: "DERIVATIVE"),
            new Operation { DateTime = DateTime.Parse("2026-06-21T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal), Date = new DateOnly(2026, 6, 21), Category = "CASH", Type = "TILG", AssetClass = "DERIVATIVE", Name = "Long 100 $", Amount = 40m, Description = "Warrant Exercise for ISIN DE000XAB1234" },
        };
        // l'ISIN della descrizione deve coincidere con la chiave del titolo
        ops[0] = Trade("2026-06-01T10:00:00", "BUY", 1000m, -800m, symbol: "DE000XAB1234", cls: "DERIVATIVE");
        ops[1] = Trade("2026-06-20T10:00:00", "WARRANT_EXERCISE", -1000m, 0m, symbol: "DE000XAB1234", cls: "DERIVATIVE");

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.Equal(-760m, position.Result);
    }

    [Fact]
    public void Free_receipts_have_no_cost_so_the_return_is_not_a_percentage()
    {
        var ops = new[]
        {
            Trade("2026-01-05T10:00:00", "FREE_RECEIPT", 2m, 0m, symbol: "SOL", cls: "CRYPTO", name: "Solana"),
            Trade("2026-02-05T10:00:00", "SELL", -2m, 150m, symbol: "SOL", cls: "CRYPTO", name: "Solana"),
        };

        var position = Assert.Single(ClosedPositionCalculator.Compute(ops));

        Assert.Equal(150m, position.Result);
        Assert.Null(position.ReturnPct);
    }

    [Theory]
    [InlineData("Best Turbo auf ACME INC. (Long 99,8419 $)", "Best Turbo auf ACME INC.")]
    [InlineData("Turbo auf ADVANCED MIC.DEV.  DL-,01 (Short 100,5 $)", "Turbo auf ADVANCED MIC.DEV.  DL-,01")]
    [InlineData("VanEck Semiconductor UCITS ETF", "VanEck Semiconductor UCITS ETF")]
    public void The_underlying_is_the_product_name_without_the_parenthesis(string name, string expected) =>
        Assert.Equal(expected, ClosedPositionCalculator.Underlying(name));
}
