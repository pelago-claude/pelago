using System.Net;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class VontobelQuoteProviderTests
{
    private static string Page(string priceJson) =>
        "<html><head></head><body><script id=\"__NEXT_DATA__\" type=\"application/json\">" +
        "{\"props\":{\"pageProps\":{\"additionalData\":{\"price\":" + priceJson + ",\"priceFactor\":1}}}}" +
        "</script></body></html>";

    private const string GoodPrice =
        "{\"bid\":0.33,\"bidSize\":95000,\"ask\":0.34,\"askSize\":95000,\"latest\":0.33,\"latestTimestamp\":\"2026-10-12T08:30:07.907+00:00\",\"currency\":\"EUR\"}";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Html(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html") };

    [Theory]
    [InlineData("DE000VAAA001", true)]
    [InlineData("de000vaad005", true)]
    [InlineData("DE000BDAA002", true)]
    [InlineData("DE000FAAC000", false)]          // di un altro emittente (Société Générale)
    [InlineData("DE000SAAA007", false)]
    [InlineData("IE00TEST0001", false)]
    [InlineData("DE000V", false)]
    [InlineData(null, false)]
    public void Only_vontobel_isins_are_covered(string? isin, bool expected) =>
        Assert.Equal(expected, VontobelQuoteProvider.Covers(isin));

    [Fact]
    public void The_page_address_carries_the_isin_in_capitals()
    {
        Assert.Equal("https://markets.vontobel.com/de-de/produkte/hebel/turbo-optionsscheine-open-end/DE000VAAA001", VontobelQuoteProvider.UrlFor("  de000vaaa001 "));
    }

    [Fact]
    public void The_bid_is_the_valuation_price_and_the_other_fields_are_read()
    {
        var quote = VontobelQuoteProvider.ParsePage(Page(GoodPrice));

        Assert.NotNull(quote);
        Assert.Equal(0.33m, quote!.Price);
        Assert.Equal(0.33m, quote.Bid);
        Assert.Equal(0.34m, quote.Ask);
        Assert.Equal("EUR", quote.Currency);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 30, 7, 907, DateTimeKind.Utc), quote.LatestUtc);
    }

    [Fact]
    public void The_product_name_is_made_from_the_short_name_of_the_underlying()
    {
        string WithData(string data) =>
            "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"additionalData\":{\"data\":" + data + ",\"price\":" + GoodPrice + "}}}}</script>";

        Assert.Equal("Turbo su Acme Energy",
            VontobelQuoteProvider.ParsePage(WithData("{\"name\":\"Acme Energy Corp.\",\"shortName\":\"Acme Energy\"}"))!.ProductName);
        Assert.Equal("Turbo su Acme Energy Corp.",
            VontobelQuoteProvider.ParsePage(WithData("{\"name\":\"Acme Energy Corp.\"}"))!.ProductName);
        Assert.Null(VontobelQuoteProvider.ParsePage(WithData("{\"isin\":\"DE000VAAA001\"}"))!.ProductName);
        Assert.Null(VontobelQuoteProvider.ParsePage(Page(GoodPrice))!.ProductName);        // senza il blocco «data» il prezzo si legge lo stesso
    }

    [Fact]
    public void Without_a_bid_the_last_price_is_used_and_with_nothing_there_is_no_quote()
    {
        var onlyLatest = VontobelQuoteProvider.ParsePage(Page("{\"latest\":1.25,\"currency\":\"EUR\"}"));
        Assert.Equal(1.25m, onlyLatest!.Price);
        Assert.Null(onlyLatest.Bid);

        Assert.Null(VontobelQuoteProvider.ParsePage(Page("{\"currency\":\"EUR\"}")));
        Assert.Null(VontobelQuoteProvider.ParsePage(Page("{\"bid\":null,\"ask\":null,\"latest\":null}")));
    }

    [Fact]
    public void A_worthless_product_has_a_price_of_zero_not_no_price()
    {
        var quote = VontobelQuoteProvider.ParsePage(Page("{\"bid\":0,\"ask\":0.001,\"latest\":0.001,\"currency\":\"EUR\"}"));

        Assert.NotNull(quote);
        Assert.Equal(0m, quote!.Price);
    }

    [Fact]
    public void A_page_without_the_data_block_or_with_broken_data_gives_no_quote()
    {
        Assert.Null(VontobelQuoteProvider.ParsePage("<html>niente</html>"));
        Assert.Null(VontobelQuoteProvider.ParsePage("<script id=\"__NEXT_DATA__\">{ rotto</script>"));
        Assert.Null(VontobelQuoteProvider.ParsePage("<script id=\"__NEXT_DATA__\">{\"props\":{}}</script>"));
    }

    [Fact]
    public async Task The_request_goes_to_the_product_page_and_declares_itself()
    {
        var handler = new FakeHandler(_ => Html(Page(GoodPrice)));
        using var provider = new VontobelQuoteProvider(handler);

        var result = await provider.GetQuoteAsync("DE000VAAA001", CancellationToken.None);

        Assert.Equal(DerivativeLookupStatus.Ok, result.Status);
        Assert.Equal(0.33m, result.Quote!.Price);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/DE000VAAA001", request.RequestUri!.AbsoluteUri);
        Assert.Contains("Pelago", request.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, DerivativeLookupStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, DerivativeLookupStatus.Blocked)]
    [InlineData((HttpStatusCode)429, DerivativeLookupStatus.Blocked)]
    [InlineData(HttpStatusCode.InternalServerError, DerivativeLookupStatus.Blocked)]
    public async Task Http_errors_are_told_apart(HttpStatusCode code, DerivativeLookupStatus expected)
    {
        using var provider = new VontobelQuoteProvider(new FakeHandler(_ => Html("errore", code)));

        var result = await provider.GetQuoteAsync("DE000VAAA001", CancellationToken.None);

        Assert.Equal(expected, result.Status);
        Assert.Equal((int)code, result.HttpStatus);
        Assert.Null(result.Quote);
    }

    [Fact]
    public async Task A_page_that_exists_but_shows_no_price_is_reported_as_such()
    {
        using var provider = new VontobelQuoteProvider(new FakeHandler(_ => Html("<html>prodotto scaduto</html>")));

        var result = await provider.GetQuoteAsync("DE000VAAD005", CancellationToken.None);

        Assert.Equal(DerivativeLookupStatus.NoPrice, result.Status);
    }

    // ---------------------------------------------------------------- quali derivati si leggono

    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";

    private static string Trade(string date, string type, string cls, string symbol, string shares, string amount, string id) =>
        $"\"{date}T10:00:00.000Z\",\"{date}\",\"DEFAULT\",\"TRADING\",\"{type}\",\"{cls}\",\"Prodotto {symbol}\",\"{symbol}\",\"{shares}\",\"1\",\"{amount}\",\"\",\"\",\"EUR\",\"{type} {symbol}\",\"{id}\"";

    [Fact]
    public async Task Only_open_vontobel_derivatives_are_selected_and_the_feature_is_off_by_default()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pelago-vontobel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var previous = Environment.GetEnvironmentVariable("DPOPER_TEST");
        Environment.SetEnvironmentVariable("DPOPER_TEST", "1");
        try
        {
            var path = Path.Combine(folder, "d.csv");
            File.WriteAllLines(path, new[]
            {
                Header,
                Trade("2026-09-01", "BUY", "DERIVATIVE", "DE000VAAA001", "100", "-50", "t1"),      // Vontobel, aperto
                Trade("2026-09-01", "BUY", "DERIVATIVE", "DE000VAAD005", "100", "-50", "t2"),      // Vontobel, poi chiuso
                Trade("2026-09-05", "SELL", "DERIVATIVE", "DE000VAAD005", "-100", "60", "t3"),
                Trade("2026-09-01", "BUY", "DERIVATIVE", "DE000FAAC000", "100", "-50", "t4"),      // altro emittente
                Trade("2026-09-01", "BUY", "FUND", "IE00TEST0001", "10", "-500", "t5"),            // non è un derivato
            });
            using var state = new AppState(new UiPrefs());
            Assert.True(await state.LoadFileAsync(path));

            Assert.False(state.VontobelEnabled);
            var open = state.OpenVontobelDerivatives();
            Assert.Equal(new[] { "DE000VAAA001" }, open.ToArray());

            state.VontobelEnabled = true;
            Assert.True(state.VontobelEnabled);
            state.VontobelEnabled = false;
        }
        finally
        {
            Environment.SetEnvironmentVariable("DPOPER_TEST", previous);
            Directory.Delete(folder, recursive: true);
        }
    }
}
