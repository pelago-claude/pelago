using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class TaxReportTests
{
    private static Operation Trade(string date, string type, decimal shares, decimal amount, decimal fee = 0m, decimal tax = 0m, string symbol = "AAA", string cls = "STOCK", string name = "Alfa") => new()
    {
        DateTime = DateTime.Parse(date + "T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
        Date = DateOnly.Parse(date),
        Category = "TRADING",
        Type = type,
        AssetClass = cls,
        Name = name,
        Symbol = symbol,
        Shares = shares,
        Amount = amount,
        Fee = fee == 0m ? null : fee,
        Tax = tax == 0m ? null : tax,
    };

    [Fact]
    public void Stocks_use_lifo_so_the_last_bought_are_sold_first()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m),     // 100 l'una
            Trade("2025-02-10", "BUY", 10m, -2000m),     // 200 l'una
            Trade("2025-03-10", "SELL", -10m, 1500m),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.Equal(2000m, sale.CostBasis);             // LIFO: si vendono i 10 acquistati per ultimi
        Assert.Equal(-500m, sale.Result);
    }

    [Fact]
    public void Etf_and_crypto_use_the_weighted_average_cost()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-02-10", "BUY", 10m, -2000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-03-10", "SELL", -10m, 1500m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.Equal(TaxCategory.EtfOicr, sale.Category);
        Assert.Equal(1500m, sale.CostBasis);             // media di 150 l'una
        Assert.Equal(0m, sale.Result);
    }

    [Fact]
    public void Fees_change_cost_and_proceeds_but_withheld_tax_is_reported_apart()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m, fee: -1m),
            Trade("2025-03-10", "SELL", -10m, 1200m, fee: -1m, tax: -20m),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.Equal(1199m, sale.Proceeds);              // 1200 meno la commissione (l'imposta non si toglie)
        Assert.Equal(1001m, sale.CostBasis);             // 1000 più la commissione
        Assert.Equal(198m, sale.Result);
        Assert.Equal(20m, sale.TaxWithheld);             // già pagata al broker
    }

    [Fact]
    public void A_loss_goes_into_the_backpack_and_offsets_later_gains_oldest_first()
    {
        var backpack = TaxReportCalculator.RunBackpack(2023, 2026, y => y switch { 2023 => -1000m, 2024 => 400m, 2025 => 1000m, _ => 0m });

        var rows = backpack.Years.ToDictionary(r => r.Year);
        Assert.Equal(1000m, rows[2023].NewLoss);
        Assert.Equal(400m, rows[2024].Used);
        Assert.Equal(0m, rows[2024].Taxable);            // tutto compensato
        Assert.Equal(600m, rows[2024].ClosingAvailable);
        Assert.Equal(600m, rows[2025].Used);
        Assert.Equal(400m, rows[2025].Taxable);          // 1000 di guadagno, 600 compensati
        Assert.Equal(0m, rows[2025].ClosingAvailable);
        Assert.Empty(backpack.Lots);
    }

    [Fact]
    public void Losses_expire_after_the_fourth_following_year()
    {
        var backpack = TaxReportCalculator.RunBackpack(2020, 2026, y => y == 2020 ? -1000m : y == 2025 ? 500m : 0m);

        var rows = backpack.Years.ToDictionary(r => r.Year);
        Assert.Equal(1000m, rows[2024].ClosingAvailable);    // ancora valida nel 2024 (4° anno dopo il 2020)
        Assert.Equal(1000m, rows[2025].Expired);             // nel 2025 è scaduta
        Assert.Equal(0m, rows[2025].Used);
        Assert.Equal(500m, rows[2025].Taxable);              // il guadagno resta tutto da tassare
    }

    [Fact]
    public void Etf_gains_do_not_enter_the_offsettable_bucket_but_etf_losses_do()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-02-10", "SELL", -10m, 1500m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),      // +500: reddito di capitale
            Trade("2025-03-10", "BUY", 10m, -1000m),
            Trade("2025-04-10", "SELL", -10m, 700m),                                                               // -300: reddito diverso
        };

        var report = TaxReportCalculator.Compute(ops);

        var row = report.Diversi.Years.Single(y => y.Year == 2025);
        Assert.Equal(-300m, row.Net);                    // il guadagno dell'ETF non compensa la perdita
        Assert.Equal(300m, row.NewLoss);
    }

    [Fact]
    public void Crypto_has_its_own_backpack()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 1m, -1000m, symbol: "BTC", cls: "CRYPTO", name: "Bitcoin"),
            Trade("2025-02-10", "SELL", -1m, 600m, symbol: "BTC", cls: "CRYPTO", name: "Bitcoin"),
            Trade("2025-03-10", "BUY", 10m, -1000m),
            Trade("2025-04-10", "SELL", -10m, 1400m),
        };

        var report = TaxReportCalculator.Compute(ops);

        Assert.Equal(-400m, report.Cripto.Years.Single(y => y.Year == 2025).Net);
        Assert.Equal(400m, report.Diversi.Years.Single(y => y.Year == 2025).Net);
        Assert.Equal(400m, report.Diversi.Years.Single(y => y.Year == 2025).Taxable);   // la perdita cripto non compensa i titoli
    }

    [Fact]
    public void A_warrant_cash_settlement_counts_as_a_derivative_proceed_and_interest_is_listed()
    {
        var ops = new List<Operation>
        {
            Trade("2025-06-01", "BUY", 1000m, -800m, symbol: "DE000X", cls: "DERIVATIVE", name: "Long 100 $"),
            Trade("2025-06-20", "WARRANT_EXERCISE", -1000m, 0m, symbol: "DE000X", cls: "DERIVATIVE", name: "Long 100 $"),
            new() { DateTime = DateTime.Parse("2025-06-21T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal), Date = new DateOnly(2025, 6, 21), Category = "CASH", Type = "TILG", AssetClass = "DERIVATIVE", Name = "Long 100 $", Amount = 40m },
            new() { DateTime = DateTime.Parse("2025-07-01T10:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal), Date = new DateOnly(2025, 7, 1), Category = "CASH", Type = "INTEREST_PAYMENT", Amount = 10m, Tax = -2.6m },
        };

        var report = TaxReportCalculator.Compute(ops);

        var derivatives = report.Sales.Where(s => s.Category == TaxCategory.Derivati).Sum(s => s.Result);
        Assert.Equal(-760m, derivatives);                // -800 all'esercizio + 40 incassati
        var interest = Assert.Single(report.Income);
        Assert.Equal(10m, interest.Gross);
        Assert.Equal(2.6m, interest.TaxWithheld);
    }

    [Fact]
    public void A_merger_and_its_reversal_do_not_create_a_phantom_loss()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-03-10", "MERGER", -10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-03-10", "MERGER", 10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-03-13", "MERGER_CANCELLED", 10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-03-13", "MERGER_CANCELLED", -10m, 0m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
            Trade("2025-06-10", "SELL", -10m, 1200m, symbol: "IE00ETF", cls: "FUND", name: "Mondo UCITS ETF"),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.Equal(1000m, sale.CostBasis);
        Assert.Equal(200m, sale.Result);
    }

    [Fact]
    public void A_cancelled_sale_is_removed_together_with_the_sale_it_cancels()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 10m, -1000m),
            Trade("2025-02-01", "SELL", -10m, 1300m),
            Trade("2025-02-04", "SELL_CANCELLED", 10m, -1300m),
            Trade("2025-02-04", "SELL", -10m, 1310m),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.Equal(new DateOnly(2025, 2, 4), sale.Date);
        Assert.Equal(1000m, sale.CostBasis);
        Assert.Equal(310m, sale.Result);
    }

    [Fact]
    public void Shares_sold_without_a_matching_purchase_are_reported_as_missing_cost()
    {
        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 4m, -400m),
            Trade("2025-03-10", "SELL", -10m, 1000m),
        };

        var sale = Assert.Single(TaxReportCalculator.Compute(ops).Sales);

        Assert.True(sale.HasMissingCost);
        Assert.Equal(6m, sale.Unmatched);
        Assert.Equal(400m, sale.CostBasis);
    }

    [Theory]
    [InlineData("Mondo UCITS ETF", "IE00TEST0001", TaxCategory.EtfOicr)]
    [InlineData("Physical Gold ETC", "IE00B579F325", TaxCategory.EtcEtn)]
    [InlineData("Qualcosa", "JE00B1VS3770", TaxCategory.EtcEtn)]
    [InlineData("WITR MU.AS.I.SILV.ETP2062", "XS0000000001", TaxCategory.EtcEtn)]
    [InlineData("WITR COM.SEC.Z08/UN.IDX", "XS0000000002", TaxCategory.EtcEtn)]
    [InlineData("WisdomTree NASDAQ 100 3x Daily Leveraged", "XS0000000003", TaxCategory.EtcEtn)]
    [InlineData("WisdomTree Gold 3x Daily Short", "XS0000000004", TaxCategory.EtcEtn)]
    [InlineData("iShares MSCI China Tech UCITS ETF USD (Acc)", "IE00BMW3QX54", TaxCategory.EtfOicr)]
    [InlineData("Vanguard FTSE All-World UCITS ETF", "IE00BK5BQT80", TaxCategory.EtfOicr)]
    public void Funds_are_classified_by_name_and_code_and_the_user_can_override(string name, string key, TaxCategory expected)
    {
        Assert.Equal(expected, TaxCategories.ForFund(name, key));

        var ops = new[]
        {
            Trade("2025-01-10", "BUY", 1m, -100m, symbol: key, cls: "FUND", name: name),
            Trade("2025-02-10", "SELL", -1m, 150m, symbol: key, cls: "FUND", name: name),
        };
        var overridden = TaxReportCalculator.Compute(ops, k => k == key ? TaxCategory.Azioni : null);
        Assert.Equal(TaxCategory.Azioni, Assert.Single(overridden.Sales).Category);
    }
}
