using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class TaxReconciliationTests
{
    private static Operation Op(string date, string type, decimal shares, decimal amount, decimal tax = 0m, string symbol = "AAA", string cls = "STOCK", string name = "Alfa", string category = "TRADING") => new()
    {
        DateTime = DateTime.Parse(date + "T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
        Date = DateOnly.Parse(date),
        Category = category,
        Type = type,
        AssetClass = cls,
        Name = name,
        Symbol = symbol,
        Shares = shares == 0m ? null : shares,
        Amount = amount,
        Tax = tax == 0m ? null : tax,
    };

    private static TaxReconciliation Run(IReadOnlyList<Operation> ops, Func<string, bool>? chosen = null)
    {
        var report = TaxReportCalculator.Compute(ops);
        return TaxReconciliationCalculator.Compute(ops, report, ClosedPositionCalculator.Compute(ops, _ => 0m), chosen ?? (_ => false));
    }

    private static TaxCheck Check(TaxReconciliation r, string code) => r.Checks.Single(c => c.Code == code);

    [Fact]
    public void A_clean_account_has_only_ok_results()
    {
        var ops = new[]
        {
            Op("2025-01-10", "BUY", 10m, -1000m),
            Op("2025-03-10", "SELL", -10m, 1200m, tax: -20m),
        };

        var result = Run(ops);

        Assert.All(result.Checks, c => Assert.Equal(FindingLevel.Ok, c.Level));
        Assert.Equal(0, result.ToCheck);
        var year = Assert.Single(result.Years);
        Assert.Equal(20m, year.OnSales);
        Assert.Equal(0m, year.Difference);
    }

    [Fact]
    public void Sales_with_no_purchase_in_the_file_are_a_warning_naming_the_security()
    {
        var ops = new[]
        {
            Op("2025-01-10", "BUY", 4m, -400m),
            Op("2025-03-10", "SELL", -10m, 1000m),
        };

        var check = Check(Run(ops), "missingcost");

        Assert.Equal(FindingLevel.Warning, check.Level);
        Assert.Contains("Alfa", check.Detail);
    }

    [Fact]
    public void Tax_on_a_sale_the_report_cannot_attribute_is_flagged_by_year()
    {
        var ops = new[]
        {
            Op("2025-01-10", "BUY", 10m, -1000m),
            Op("2025-03-10", "SELL", -10m, 1200m, tax: -20m),
            Op("2025-04-10", "SELL", -3m, 300m, tax: -5m, symbol: "", cls: "", name: ""),     // senza titolo: il prospetto non la vede
        };

        var result = Run(ops);

        Assert.Equal(FindingLevel.Warning, Check(result, "taxdiff").Level);
        Assert.Equal(5m, result.Years.Single().Difference);
    }

    [Fact]
    public void Year_rows_split_the_withheld_tax_by_type()
    {
        var ops = new[]
        {
            Op("2025-01-10", "BUY", 10m, -1000m),
            Op("2025-03-10", "SELL", -10m, 1200m, tax: -20m),
            Op("2025-04-01", "INTEREST_PAYMENT", 0m, 10m, tax: -2.6m, category: "CASH", symbol: "", cls: "", name: ""),
            Op("2025-12-31", "TAX", 0m, 0m, tax: -3m, category: "CASH", symbol: "", cls: "", name: ""),
        };

        var year = Assert.Single(Run(ops).Years);

        Assert.Equal(20m, year.OnSales);
        Assert.Equal(2.6m, year.OnInterest);
        Assert.Equal(3m, year.StampAndRecalculation);
        Assert.Equal(25.6m, year.Total);
    }

    [Fact]
    public void A_knocked_out_turbo_without_a_sale_is_a_warning_with_its_loss()
    {
        var ops = new[] { Op("2025-01-10", "BUY", 1000m, -800m, symbol: "DE000X", cls: "DERIVATIVE", name: "Turbo X") };

        var check = Check(Run(ops), "knockout");

        Assert.Equal(FindingLevel.Warning, check.Level);
        Assert.Contains("800", check.Detail);
    }

    [Fact]
    public void Mergers_cancelled_sales_unchosen_funds_and_free_crypto_are_reported_as_information()
    {
        var ops = new[]
        {
            Op("2025-01-10", "BUY", 10m, -1000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Op("2025-02-10", "MERGER", -10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Op("2025-02-10", "MERGER", 10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Op("2025-03-10", "SELL", -10m, 1200m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Op("2025-04-10", "FREE_RECEIPT", 0.5m, 0m, symbol: "SOL", cls: "CRYPTO", name: "Solana"),
        };

        var result = Run(ops);

        Assert.Equal(FindingLevel.Info, Check(result, "reversals").Level);
        Assert.Equal(FindingLevel.Info, Check(result, "funds").Level);
        Assert.Equal(FindingLevel.Info, Check(result, "rewards").Level);

        var chosen = Run(ops, _ => true);
        Assert.DoesNotContain(chosen.Checks, c => c.Code == "funds");
    }
}
