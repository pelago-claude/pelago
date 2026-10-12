using DPOper.Components;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class HeatMapTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pelago-heat-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------- i colori

    [Fact]
    public void Without_a_cost_or_with_no_gain_the_colour_is_the_neutral_grey()
    {
        Assert.Equal(HeatColor.NeutralHex, HeatColor.For(null));
        Assert.Equal(HeatColor.NeutralHex, HeatColor.For(0.0));
        Assert.Equal(HeatColor.NeutralHex, HeatColor.For(double.NaN));
    }

    [Fact]
    public void The_colour_grows_with_the_gain_or_the_loss_and_stops_at_fifty_percent()
    {
        Assert.Equal(HeatColor.GainHex, HeatColor.For(0.5));
        Assert.Equal(HeatColor.GainHex, HeatColor.For(3.0));          // un turbo a +300%: non diventa più verde di così
        Assert.Equal(HeatColor.LossHex, HeatColor.For(-0.5));
        Assert.Equal(HeatColor.LossHex, HeatColor.For(-1.0));

        // a metà strada: il colore sta a metà tra il grigio e il verde
        var half = HeatColor.For(0.25);
        Assert.NotEqual(HeatColor.NeutralHex, half);
        Assert.NotEqual(HeatColor.GainHex, half);
        Assert.Equal("#46827c", half);                                // a metà tra il grigio (6f7b8a) e il verde (1e8a6e)

        Assert.NotEqual(HeatColor.For(0.1), HeatColor.For(0.2));
        Assert.NotEqual(HeatColor.For(0.1), HeatColor.For(-0.1));
    }

    // ---------------------------------------------------------------- la composizione a una data

    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";

    private static string Row(string date, string type, string cls, string symbol, string shares, string price, string amount, string id) =>
        $"\"{date}T10:00:00.000Z\",\"{date}\",\"DEFAULT\",\"TRADING\",\"{type}\",\"{cls}\",\"{symbol}\",\"{symbol}\",\"{shares}\",\"{price}\",\"{amount}\",\"\",\"\",\"EUR\",\"{type} {symbol}\",\"{id}\"";

    private async Task<(AppState State, string Folder)> LoadAsync(params string[] rows)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "h.csv");
        File.WriteAllLines(path, new[] { Header }.Concat(rows));
        Environment.SetEnvironmentVariable("DPOPER_TEST", "1");
        var state = new AppState(new UiPrefs());
        Assert.True(await state.LoadFileAsync(path));
        return (state, _dir);
    }

    [Fact]
    public async Task Value_cost_and_gain_are_those_of_the_chosen_day()
    {
        var (state, _) = await LoadAsync(
            Row("2026-01-05", "BUY", "STOCK", "AAA", "10", "10", "-100", "t1"),
            Row("2026-02-05", "BUY", "STOCK", "AAA", "10", "20", "-200", "t2"),
            Row("2026-01-05", "BUY", "STOCK", "BBB", "5", "40", "-200", "t3"),
            Row("2026-02-01", "SELL", "STOCK", "BBB", "-5", "40", "200", "t4"));
        using var _ = state;

        var january = HeatMapCalculator.Compute(state.Operations!, new DateOnly(2026, 1, 10), state.Quotes);
        Assert.Equal(new[] { "BBB", "AAA" }, january.Items.Select(i => i.Key).ToArray());        // dal più grande al più piccolo
        var aaa1 = january.Items.Single(i => i.Key == "AAA");
        Assert.Equal(100m, aaa1.Value);
        Assert.Equal(100m, aaa1.Cost);
        Assert.Equal(0m, aaa1.Pnl);

        var february = HeatMapCalculator.Compute(state.Operations!, new DateOnly(2026, 2, 10), state.Quotes);
        var aaa2 = Assert.Single(february.Items);                                                  // BBB è stato venduto
        Assert.Equal("AAA", aaa2.Key);
        Assert.Equal(20m, aaa2.Shares);
        Assert.Equal(400m, aaa2.Value);
        Assert.Equal(300m, aaa2.Cost);                                                             // costo medio: 10 quote a 10 e 10 a 20
        Assert.Equal(100m, aaa2.Pnl);
        Assert.Equal(1.0 / 3.0, aaa2.PnlPct!.Value, 6);
        Assert.Equal(100m, february.Pnl);
        Assert.Equal(300m, february.KnownCost);
        Assert.Equal(400m, february.ItemsValue);
    }

    [Fact]
    public async Task Titles_worth_nothing_are_counted_but_not_drawn_and_weights_use_the_whole_account()
    {
        var (state, _) = await LoadAsync(
            Row("2026-01-02", "CUSTOMER_INBOUND", "CASH", "", "", "", "1000", "d1"),
            Row("2026-01-05", "BUY", "STOCK", "AAA", "10", "10", "-100", "t1"),
            Row("2026-01-05", "BUY", "DERIVATIVE", "ZZZ", "100", "0", "0", "t2"));
        using var _ = state;

        var map = HeatMapCalculator.Compute(state.Operations!, new DateOnly(2026, 1, 10), state.Quotes);

        var shown = Assert.Single(map.Items);
        Assert.Equal("AAA", shown.Key);
        Assert.Equal(1, map.Hidden);
        Assert.True(map.Total > map.ItemsValue);                       // c'è anche la liquidità del versamento
        Assert.Equal((double)(shown.Value / map.Total), shown.Weight, 9);
    }

    [Fact]
    public async Task A_title_without_a_known_cost_has_no_gain_and_gets_the_neutral_colour()
    {
        var (state, _) = await LoadAsync(Row("2026-01-05", "BUY", "STOCK", "AAA", "10", "10", "-100", "t1"));
        using var _ = state;

        var map = HeatMapCalculator.Compute(state.Operations!, new DateOnly(2026, 1, 10), state.Quotes);
        var item = Assert.Single(map.Items) with { Cost = null };

        Assert.Null(item.Pnl);
        Assert.Null(item.PnlPct);
        Assert.Equal(HeatColor.NeutralHex, HeatColor.For(item.PnlPct));
    }

    [Fact]
    public async Task The_tree_for_the_chart_groups_by_category_and_carries_what_the_page_needs()
    {
        var (state, _) = await LoadAsync(
            Row("2026-01-05", "BUY", "STOCK", "AAA", "10", "10", "-100", "t1"),
            Row("2026-01-05", "BUY", "FUND", "FFF", "2", "50", "-100", "t2"));
        using var _ = state;

        var map = HeatMapCalculator.Compute(state.Operations!, new DateOnly(2026, 1, 10), state.Quotes);
        var json = System.Text.Json.JsonSerializer.Serialize(ChartData.HeatMapTree(map, i => "Nome " + i.Key));

        Assert.Contains("\"name\":\"Nome AAA\"", json);
        Assert.Contains("\"key\":\"FFF\"", json);
        Assert.Contains("\"groups\":[", json);
        Assert.Contains("\"color\":\"" + HeatColor.For(0.0) + "\"", json);
    }

    // ---------------------------------------------------------------- lo slider del tempo

    [Fact]
    public async Task The_chosen_day_is_shared_stays_within_the_data_and_defaults_to_today()
    {
        var (state, _) = await LoadAsync(Row("2026-01-05", "BUY", "STOCK", "AAA", "10", "10", "-100", "t1"));
        using var _ = state;

        Assert.Equal(state.MaxDate, state.ViewDate);

        state.ViewDate = new DateOnly(2026, 1, 20);
        Assert.Equal(new DateOnly(2026, 1, 20), state.ViewDate);

        state.ViewDate = new DateOnly(2020, 1, 1);
        Assert.Equal(state.MinDate, state.ViewDate);

        state.ViewDate = new DateOnly(2999, 1, 1);
        Assert.Equal(state.MaxDate, state.ViewDate);
    }

    [Theory]
    [InlineData(580, 8)]             // un anno e mezzo di dati: otto giorni per passo, una settantina di passi in tutto
    [InlineData(80, 1)]
    [InlineData(30, 1)]              // pochi giorni: uno alla volta
    [InlineData(0, 1)]
    public void The_playback_moves_a_few_days_per_step(int totalDays, int expected)
    {
        var min = new DateOnly(2025, 1, 1);

        Assert.Equal(expected, DateSlider.DaysPerStep(min, min.AddDays(totalDays)));
    }

    [Fact]
    public void The_playback_gives_the_map_time_to_be_drawn_and_goes_faster_at_higher_speed()
    {
        Assert.Equal(1000, DateSlider.FrameMilliseconds(1));        // a ×1 un secondo per passo: il tempo di vedere la mappa
        Assert.Equal(500, DateSlider.FrameMilliseconds(2));
        Assert.Equal(250, DateSlider.FrameMilliseconds(4));
        Assert.Equal(1000, DateSlider.FrameMilliseconds(7));        // un valore strano vale come la velocità normale
    }
}
