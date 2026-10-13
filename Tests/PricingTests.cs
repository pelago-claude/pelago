using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class CurrencyConverterTests
{
    [Theory]
    [InlineData(4433, "GBp", 44.33, "GBP")]   // l'ETF londinese che valeva 4.433 "euro"
    [InlineData(4433, "GBX", 44.33, "GBP")]
    [InlineData(100, "USD", 100, "USD")]
    [InlineData(51.5, "EUR", 51.5, "EUR")]
    public void Minor_units_are_brought_back_to_the_main_unit(decimal price, string currency, decimal expectedPrice, string expectedCurrency)
    {
        var (normalized, normalizedCurrency) = CurrencyConverter.NormalizeMinorUnit(price, currency);

        Assert.Equal(expectedPrice, normalized);
        Assert.Equal(expectedCurrency, normalizedCurrency);
    }

    [Theory]
    [InlineData("EUR", true)]
    [InlineData("eur", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("USD", false)]
    [InlineData("GBp", false)]
    public void Only_euro_counts_as_euro(string? currency, bool expected) => Assert.Equal(expected, CurrencyConverter.IsEur(currency));
}

public class AssetNameResolverTests
{
    private static readonly string[] FundDescriptions =
    {
        "Savings plan execution LU0000000019 Acme - Acme MSCI Latin America UCITS ETF 1C, quantity: 0.77",
    };

    [Fact]
    public void Fund_gets_its_full_product_name_without_the_issuer_prefix()
    {
        var name = AssetNameResolver.ResolveFullName("Acme EM Latin America Swap USD (Acc)", "FUND", FundDescriptions);

        Assert.Equal("Acme MSCI Latin America UCITS ETF 1C", name);
    }

    [Fact]
    public void Derivative_keeps_the_short_label_that_carries_the_strike()
    {
        var name = AssetNameResolver.ResolveFullName("Long 61,49 $", "DERIVATIVE",
            new[] { "Buy trade DE000XX TURBO AUF ACME TECH. DL-,00001, quantity: 1000" });

        Assert.Equal("TURBO AUF ACME TECH. DL-,00001 (Long 61,49 $)", name);
    }

    [Fact]
    public void Stock_and_crypto_keep_the_short_name()
    {
        Assert.Equal("AMD", AssetNameResolver.ResolveFullName("AMD", "STOCK", new[] { "Buy trade US0000000019 ADVANCED MIC.DEV. DL-,01, quantity: 3" }));
        Assert.Equal("Bitcoin", AssetNameResolver.ResolveFullName("Bitcoin", "CRYPTO", Array.Empty<string>()));
    }

    [Fact]
    public void Falls_back_to_the_short_name_when_no_description_matches()
    {
        Assert.Equal("Fondo", AssetNameResolver.ResolveFullName("Fondo", "FUND", new[] { "Interest payment" }));
    }
}

public class PortfolioCalculatorTests
{
    [Fact]
    public void Cash_includes_fees_and_taxes()
    {
        var ops = new[]
        {
            Ops.Transfer("2026-01-02", 1000m),
            Ops.Buy("2026-01-03", "AAA", "STOCK", 10, 10m, fee: 1m),     // -101
        };

        var last = PortfolioCalculator.Compute(ops).Last();

        Assert.Equal(899m, last.Cash);
        Assert.Equal(1000m, last.Contributions);
        Assert.Equal(100m, last.Securities);   // 10 x ultimo prezzo noto (10)
    }

    [Fact]
    public void Interest_is_not_a_contribution()
    {
        var ops = new[] { Ops.Transfer("2026-01-02", 1000m), Ops.Interest("2026-02-01", 10m, 2.6m) };

        var last = PortfolioCalculator.Compute(ops).Last();

        Assert.Equal(1000m, last.Contributions);
        Assert.Equal(1007.4m, last.Cash);
    }

    [Fact]
    public void External_quote_wins_over_the_last_trade_price()
    {
        var ops = new[] { Ops.Transfer("2026-01-02", 1000m), Ops.Buy("2026-01-03", "AAA", "STOCK", 10, 10m) };
        var quotes = new Dictionary<string, List<(DateTime, decimal)>> { ["AAA"] = new() { (Ops.At("2026-02-01"), 12m) } };

        var holding = PortfolioCalculator.ComputeHoldings(ops, quotes).Single();

        Assert.Equal(12m, holding.LastPrice);
        Assert.Equal(120m, holding.Value);
    }
}

public class BondTests
{
    [Fact]
    public void Yield_at_par_equals_the_coupon_rate()
    {
        var settlement = new DateOnly(2026, 10, 12);
        var maturity = new DateOnly(2030, 3, 1);

        var yield = BondMath.Yield(settlement, maturity, 0.035, 100, 100);

        Assert.Equal(0.035, yield, 4);
    }

    [Fact]
    public void Below_par_yields_more_than_the_coupon()
    {
        var yield = BondMath.Yield(new DateOnly(2026, 10, 12), new DateOnly(2030, 3, 1), 0.035, 95, 100);

        Assert.True(yield > 0.035);
    }

    [Fact]
    public void Buying_at_100_on_a_coupon_date_has_no_surplus_and_no_accrual()
    {
        var bond = new BtpBond("IT000TEST", "BTP Test", 0.04m, new DateOnly(2030, 3, 1), 100m);
        var parameters = new BtpParams(Capital: 10000m, Commission: 0m, TaxRate: 0.125m, Lot: 1000m, Settlement: new DateOnly(2026, 3, 1));

        var context = BtpCalculator.CreateContext(bond, parameters)!;
        var scenario = BtpCalculator.Compute(context, 100m);

        Assert.Equal(10, scenario.Lots);
        Assert.Equal(10000m, scenario.Nominal);
        Assert.Equal(0m, scenario.Surplus);
        Assert.Equal(400m, scenario.AnnualCoupon);
        Assert.Equal(10000m, scenario.TotalOutlay);
    }
}
