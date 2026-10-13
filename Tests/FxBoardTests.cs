using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class FxBoardTests
{
    private static readonly FxCurrencyInfo Usd = new("USD", "Dollaro statunitense");
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static List<(DateOnly, decimal)> Ecb(params (int DaysAgo, decimal Rate)[] points) =>
        points.Select(p => (Today.AddDays(-p.DaysAgo), p.Rate)).ToList();

    [Fact]
    public void Parses_the_frankfurter_time_series_by_currency()
    {
        const string json = """{"amount":1.0,"base":"EUR","rates":{"2026-10-07":{"USD":1.12,"GBP":0.85},"2026-10-08":{"USD":1.1186,"GBP":0.84698}}}""";

        var series = FxBoard.ParseFrankfurterSeries(json);

        Assert.Equal(2, series["USD"].Count);
        Assert.Equal(1.1186m, series["USD"][1].Rate);
        Assert.Equal(new DateOnly(2026, 10, 8), series["GBP"][1].Date);
    }

    [Fact]
    public void Parses_the_yahoo_chart_meta()
    {
        const string json = """{"chart":{"result":[{"meta":{"currency":"USD","regularMarketPrice":1.122,"regularMarketTime":1791541008,"chartPreviousClose":1.1216}}]}}""";

        var quote = FxBoard.ParseYahooMeta(json);

        Assert.NotNull(quote);
        Assert.Equal(1.122m, quote!.Price);
        Assert.Equal(1.1216m, quote.PreviousClose);
        Assert.Equal(DateTimeKind.Utc, quote.Time.Kind);
    }

    [Fact]
    public void A_yahoo_answer_without_a_price_is_ignored()
    {
        Assert.Null(FxBoard.ParseYahooMeta("""{"chart":{"result":null,"error":{"code":"Not Found"}}}"""));
    }

    [Fact]
    public void Live_rate_wins_and_extends_the_sparkline()
    {
        var ecb = Ecb((40, 1.10m), (30, 1.11m), (1, 1.1186m));
        var live = new FxBoard.YahooQuote(1.122m, 1.1216m, new DateTime(2026, 10, 9, 10, 16, 0, DateTimeKind.Utc));

        var row = FxBoard.BuildRow(Usd, ecb, live, Today)!;

        Assert.Equal(1.122m, row.Rate);
        Assert.True(row.IsLive);
        Assert.Equal(1.122m / 1.1216m - 1m, row.Change1d);
        Assert.Equal(1.122m / 1.11m - 1m, row.Change30d);   // riferimento: il cambio BCE di 30 giorni fa
        Assert.Equal(1.122m, row.Spark[^1]);
        Assert.Equal(3, row.Spark.Count);                    // i due punti BCE dentro i 30 giorni + il valore di adesso
    }

    [Fact]
    public void An_absurd_live_rate_falls_back_to_the_ecb()
    {
        var ecb = Ecb((30, 1.11m), (2, 1.12m), (1, 1.1186m));
        var live = new FxBoard.YahooQuote(11.2m, 1.12m, DateTime.UtcNow);   // errore di virgola

        var row = FxBoard.BuildRow(Usd, ecb, live, Today)!;

        Assert.Equal(1.1186m, row.Rate);
        Assert.Equal(FxBoard.EcbSource, row.Source);
        Assert.False(row.IsLive);
        Assert.Equal(1.1186m / 1.12m - 1m, row.Change1d);
    }

    [Fact]
    public void Without_any_source_there_is_no_row()
    {
        Assert.Null(FxBoard.BuildRow(Usd, new(), null, Today));
    }

    [Fact]
    public void Only_yahoo_still_gives_a_rate()
    {
        var row = FxBoard.BuildRow(Usd, new(), new FxBoard.YahooQuote(1.12m, null, DateTime.UtcNow), Today)!;

        Assert.Equal(1.12m, row.Rate);
        Assert.Null(row.Change1d);
        Assert.Null(row.Change30d);
    }

    [Fact]
    public void Converts_through_the_euro()
    {
        var snapshot = new FxSnapshot
        {
            Rows = new()
            {
                new FxRow { Code = "USD", Rate = 1.25m },
                new FxRow { Code = "GBP", Rate = 0.80m },
            },
        };

        Assert.Equal(125m, FxBoard.Convert(100m, "EUR", "USD", snapshot));
        Assert.Equal(80m, FxBoard.Convert(100m, "USD", "EUR", snapshot));
        Assert.Equal(64m, FxBoard.Convert(100m, "USD", "GBP", snapshot));
        Assert.Null(FxBoard.Convert(100m, "USD", "XXX", snapshot));
    }

    [Fact]
    public void Exposure_groups_by_quote_currency_and_weights_the_total()
    {
        var rows = FxExposure.Compute(new (decimal, string?)[]
        {
            (6000m, null), (2000m, "USD"), (1500m, "usd"), (500m, "GBP"), (0m, "JPY"),
        });

        Assert.Equal(new[] { "EUR", "USD", "GBP" }, rows.Select(r => r.Currency));
        Assert.Equal(3500m, rows[1].Value);
        Assert.Equal(0.6m, rows[0].Share);
        Assert.Equal(1m, rows.Sum(r => r.Share));
    }
}
