using System.IO.Compression;
using DPOper.Models;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class ConsistencyCheckerTests
{
    private static readonly DateTime Now = Ops.At("2026-10-09", "12:00:00");

    private static List<Finding> Run(List<Operation> ops, Dictionary<string, List<(DateTime, decimal)>> quotes, List<AssetCatalogEntry>? catalog = null)
    {
        var snapshots = PortfolioCalculator.Compute(ops, quotes);
        var holdings = PortfolioCalculator.ComputeHoldings(ops, quotes);
        return ConsistencyChecker.Run(ops, snapshots, quotes, catalog ?? new List<AssetCatalogEntry>(), holdings, _ => new List<QuoteEntry>(), k => k, Now);
    }

    private static Finding Get(List<Finding> findings, string code) => findings.Single(f => f.Code == code);

    [Fact]
    public void A_clean_account_has_no_warnings()
    {
        var ops = new List<Operation> { Ops.Transfer("2026-10-01", 1000m), Ops.Buy("2026-10-02", "AAA", "STOCK", 10, 10m) };
        var quotes = new Dictionary<string, List<(DateTime, decimal)>> { ["AAA"] = new() { (Ops.At("2026-10-08"), 10.5m) } };

        var findings = Run(ops, quotes);

        Assert.DoesNotContain(findings, f => f.Level == FindingLevel.Warning);
    }

    [Fact]
    public void A_price_far_from_the_last_trade_is_flagged()
    {
        var ops = new List<Operation> { Ops.Transfer("2026-10-01", 1000m), Ops.Buy("2026-10-02", "AAA", "FUND", 10, 52m) };
        var quotes = new Dictionary<string, List<(DateTime, decimal)>> { ["AAA"] = new() { (Ops.At("2026-10-08"), 4433m) } };

        Assert.Equal(FindingLevel.Warning, Get(Run(ops, quotes), "pricejump").Level);
    }

    [Fact]
    public void Negative_cash_is_flagged()
    {
        var ops = new List<Operation> { Ops.Buy("2026-10-02", "AAA", "STOCK", 10, 10m) };   // nessun versamento prima

        Assert.Equal(FindingLevel.Warning, Get(Run(ops, new()), "cash").Level);
    }

    [Fact]
    public void An_open_position_without_a_downloaded_quote_is_flagged()
    {
        var ops = new List<Operation> { Ops.Transfer("2026-10-01", 1000m), Ops.Buy("2026-10-02", "AAA", "STOCK", 10, 10m) };

        Assert.Equal(FindingLevel.Warning, Get(Run(ops, new()), "noquote").Level);
    }

    [Fact]
    public void An_old_quote_is_flagged_as_stale()
    {
        var ops = new List<Operation> { Ops.Transfer("2026-09-01", 1000m), Ops.Buy("2026-09-02", "AAA", "STOCK", 10, 10m) };
        var quotes = new Dictionary<string, List<(DateTime, decimal)>> { ["AAA"] = new() { (Ops.At("2026-09-10"), 10m) } };

        Assert.Equal(FindingLevel.Warning, Get(Run(ops, quotes), "stale").Level);
    }

    [Fact]
    public void Duplicated_transaction_ids_are_flagged()
    {
        var a = Ops.Transfer("2026-10-01", 100m);
        var ops = new List<Operation> { new() { DateTime = a.DateTime, Date = a.Date, Category = a.Category, Type = a.Type, Amount = 100m, TransactionId = "X" },
                                        new() { DateTime = a.DateTime, Date = a.Date, Category = a.Category, Type = a.Type, Amount = 100m, TransactionId = "X" } };

        Assert.Equal(FindingLevel.Warning, Get(Run(ops, new()), "duplicates").Level);
    }
}

public class CouponScheduleTests
{
    [Fact]
    public void Lists_remaining_coupons_and_the_redemption()
    {
        var bond = new BtpBond("IT000TEST", "BTP Test 3% Mar30", 0.03m, new DateOnly(2030, 3, 1), 100m);

        var plan = CouponSchedule.Build(new[] { ("IT000TEST", "BTP Test", 10000m) }, new[] { bond }, 0.125m, new DateOnly(2026, 10, 9));

        var coupons = plan.Events.Where(e => e.Kind == "Cedola").ToList();
        Assert.Equal(7, coupons.Count);                                   // 1/3/27, 1/9/27 ... 1/3/30
        Assert.Equal(new DateOnly(2027, 3, 1), coupons[0].Date);
        Assert.Equal(150m, coupons[0].Gross);                             // 10.000 x 3% / 2
        Assert.Equal(131.25m, coupons[0].Net);                            // meno 12,5%
        var redemption = Assert.Single(plan.Events, e => e.Kind == "Rimborso");
        Assert.Equal(10000m, redemption.Gross);
        Assert.Equal(new DateOnly(2030, 3, 1), redemption.Date);
        Assert.Empty(plan.Unknown);
    }

    [Fact]
    public void Unknown_bonds_are_reported_not_invented()
    {
        var plan = CouponSchedule.Build(new[] { ("IT0000X", "Sconosciuto", 1000m) }, Array.Empty<BtpBond>(), 0.125m, new DateOnly(2026, 10, 9));

        Assert.Empty(plan.Events);
        Assert.Equal(new[] { "Sconosciuto" }, plan.Unknown);
    }

    [Fact]
    public void Expired_bonds_have_no_events()
    {
        var bond = new BtpBond("IT000OLD", "Scaduto", 0.03m, new DateOnly(2026, 3, 1), 100m);

        Assert.Empty(CouponSchedule.Build(new[] { ("IT000OLD", "Scaduto", 1000m) }, new[] { bond }, 0.125m, new DateOnly(2026, 10, 9)).Events);
    }
}

public class XlsxWriterTests
{
    [Fact]
    public void Writes_a_valid_workbook_with_typed_cells()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pelago-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            XlsxWriter.Write(path, "Prova", new[] { "Nome", "Importo", "Data" }, new[] { ColumnKind.Text, ColumnKind.Money, ColumnKind.Date },
                new[] { new object?[] { "Mondo & Co <ETF>", 1234.5m, new DateTime(2026, 10, 9, 14, 30, 0) } });

            using var zip = ZipFile.OpenRead(path);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("xl/workbook.xml", names);
            Assert.Contains("xl/worksheets/sheet1.xml", names);
            Assert.Contains("xl/styles.xml", names);

            using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var sheet = reader.ReadToEnd();
            Assert.Contains("Mondo &amp; Co &lt;ETF&gt;", sheet);            // testo con caratteri speciali, in XML valido
            Assert.Contains("<v>1234.5</v>", sheet);                          // importo come numero, non come testo
            var serial = System.Text.RegularExpressions.Regex.Match(sheet, "<c r=\"C2\"[^>]*><v>([^<]+)</v>").Groups[1].Value;
            Assert.Equal(46304.6041667, double.Parse(serial, System.Globalization.CultureInfo.InvariantCulture), 6);   // data come numero seriale di Excel
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Columns_are_wide_enough_for_long_names_and_dates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pelago-test-{Guid.NewGuid():N}.xlsx");
        try
        {
            XlsxWriter.Write(path, "Prova", new[] { "Titolo", "Data" }, new[] { ColumnKind.Text, ColumnKind.Date },
                new[] { new object?[] { "Acme EM Latin America Swap USD (Acc)", new DateTime(2026, 10, 9) } });

            using var zip = ZipFile.OpenRead(path);
            using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var sheet = reader.ReadToEnd();
            double Width(int col) => double.Parse(System.Text.RegularExpressions.Regex.Match(sheet, $"<col min=\"{col}\"[^>]*width=\"([^\"]+)\"").Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(Width(1) >= 37);   // il nome lungo non viene tagliato
            Assert.True(Width(2) >= 18);   // data e ora entrano senza "###"
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Cell_references_follow_the_excel_column_letters()
    {
        Assert.Equal("A1", XlsxWriter.Ref(0, 1));
        Assert.Equal("Z3", XlsxWriter.Ref(25, 3));
        Assert.Equal("AA1", XlsxWriter.Ref(26, 1));
        Assert.Equal("AB7", XlsxWriter.Ref(27, 7));
    }
}
