using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class GovBondsTests
{
    [Fact]
    public void Splits_csv_lines_respecting_quotes()
    {
        var cells = GovBonds.SplitCsvLine("a,\"b, with comma\",\"say \"\"hi\"\"\",d", ',');

        Assert.Equal(new[] { "a", "b, with comma", "say \"hi\"", "d" }, cells);
    }

    [Fact]
    public void Parses_the_ecb_monthly_csv_even_with_commas_inside_descriptions()
    {
        const string csv = "KEY,FREQ,REF_AREA,TIME_PERIOD,OBS_VALUE,TITLE\n" +
                           "IRS.M.IT.L.L40,M,IT,2026-07,3.881,\"Italy, 10-year\"\n" +
                           "IRS.M.IT.L.L40,M,IT,2026-06,3.734,\"Italy, 10-year\"\n";

        var points = GovBonds.ParseEcbMonthly(csv);

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), points[0].Date);   // in ordine di data
        Assert.Equal(3.881m, points[1].Value);
    }

    [Fact]
    public void Parses_the_bundesbank_csv_into_maturities()
    {
        const string csv = "﻿\"\",BBSIS.D.I.ZAR.ZI.EUR.S1311.B.A604.R02XX.R.A.A._Z._Z.A,BBSIS.D.I.ZAR.ZI.EUR.S1311.B.A604.R02XX.R.A.A._Z._Z.A_FLAGS,BBSIS.D.I.ZAR.ZI.EUR.S1311.B.A604.R10XX.R.A.A._Z._Z.A,BBSIS.D.I.ZAR.ZI.EUR.S1311.B.A604.R10XX.R.A.A._Z._Z.A_FLAGS\n" +
                           "unit multiplier,One,,One,\n" +
                           "last update,2026-10-09 12:07:30,,2026-10-09 12:07:30,\n" +
                           "2026-10-08,2.9,,3.54,\n" +
                           "2026-10-09,2.92,,3.50,\n";

        var rows = GovBonds.ParseBundesbank(csv);

        Assert.Equal(4, rows.Count);
        Assert.Contains(("10", "2026-10-09", 3.50m), rows);
        Assert.Contains(("2", "2026-10-08", 2.9m), rows);
    }

    [Fact]
    public void Parses_the_treasury_curve_and_maps_column_names_to_years()
    {
        const string csv = "Date,\"1 Mo\",\"3 Mo\",\"2 Yr\",\"10 Yr\",\"30 Yr\"\n" +
                           "10/08/2026,4.14,4.23,4.75,5.22,5.60\n" +
                           "10/07/2026,4.07,4.22,4.77,5.28,5.67\n";

        var rows = GovBonds.ParseTreasuryCsv(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 10, 8), rows[0].Date);
        Assert.Equal(5.22m, rows[0].Values["10 Yr"]);
        Assert.Equal(0.25m, GovBonds.TreasuryYears("3 Mo"));
        Assert.Equal(10m, GovBonds.TreasuryYears("10 Yr"));
        Assert.Null(GovBonds.TreasuryYears("Nonsense"));
    }

    [Fact]
    public void Parses_the_treasury_auctions_and_skips_bills()
    {
        const string json = """
            [
              {"securityType":"Bond","securityTerm":"29-Year 10-Month","issueDate":"2026-10-15T00:00:00","maturityDate":"2056-08-15T00:00:00","interestRate":"5.125000","highYield":"5.6180","pricePer100":"92.889131","bidToCoverRatio":"2.540000"},
              {"securityType":"Bill","securityTerm":"4-Week","issueDate":"2026-10-13T00:00:00","maturityDate":"2026-11-10T00:00:00","interestRate":"","highYield":"","pricePer100":"99.690444","bidToCoverRatio":"2.4"},
              {"securityType":"Note","securityTerm":"10-Year","issueDate":"2026-09-15T00:00:00","maturityDate":"2036-08-15T00:00:00","interestRate":"4.625000","highYield":"","pricePer100":"","bidToCoverRatio":""}
            ]
            """;

        var auctions = GovBonds.ParseAuctions(json);

        Assert.Equal(2, auctions.Count);
        Assert.Equal("Bond", auctions[0].Type);                     // la più recente per prima
        Assert.Equal(5.125m, auctions[0].Coupon);
        Assert.Equal(5.6180m, auctions[0].Yield);
        Assert.Null(auctions[1].Yield);                              // campi vuoti: nessun valore, non zero
        Assert.Null(auctions[1].Price);
    }

    [Fact]
    public void Curve_falls_back_to_the_last_day_on_or_before_the_target()
    {
        var byDate = new SortedDictionary<DateOnly, List<CurvePoint>>
        {
            [new DateOnly(2026, 9, 7)] = new() { new CurvePoint(10m, 3m) },
            [new DateOnly(2026, 10, 9)] = new() { new CurvePoint(10m, 3.5m) },
        };

        var curve = GovBonds.BuildCurve(byDate, new DateOnly(2026, 9, 9), "test");

        Assert.Equal(new DateOnly(2026, 9, 7), curve!.Date);
        Assert.Null(GovBonds.BuildCurve(byDate, new DateOnly(2020, 1, 1), "test"));
    }

    [Fact]
    public void Spread_is_the_difference_in_basis_points_on_common_months()
    {
        var italy = new[] { new YieldPoint(new DateOnly(2026, 7, 1), 3.881m), new YieldPoint(new DateOnly(2026, 8, 1), 3.986m), new YieldPoint(new DateOnly(2026, 9, 1), 4m) };
        var germany = new[] { new YieldPoint(new DateOnly(2026, 7, 1), 3.07m), new YieldPoint(new DateOnly(2026, 8, 1), 3.185m) };

        var spread = GovBonds.SpreadBasisPoints(italy, germany);

        Assert.Equal(2, spread.Count);                // settembre manca per la Germania
        Assert.Equal(81.1m, spread[0].Value);
        Assert.Equal(80.1m, spread[1].Value);
    }

    [Fact]
    public void Every_country_and_currency_has_a_flag_file()
    {
        var folder = Path.Combine(I18nSource.Root, "wwwroot", "img", "flags");
        var wanted = GovBonds.Countries.Select(c => c.Flag)
            .Concat(FxBoard.Currencies.Select(c => FxBoard.FlagOf(c.Code) ?? "(manca la valuta " + c.Code + ")"))
            .Append(FxBoard.FlagOf("EUR")!)
            .Distinct();

        var missing = wanted.Where(f => !File.Exists(Path.Combine(folder, f + ".svg"))).ToList();

        Assert.True(missing.Count == 0, "bandiere mancanti: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_country_has_a_unique_code_and_the_featured_ones_exist()
    {
        Assert.Equal(GovBonds.Countries.Count, GovBonds.Countries.Select(c => c.Code).Distinct().Count());
        Assert.All(GovBonds.Featured, code => Assert.NotNull(GovBonds.Find(code)));
        Assert.Equal("", GovBonds.Find("EA")!.IsinPrefix);
        Assert.Equal("DE", GovBonds.Find("de")!.IsinPrefix);
    }
}
