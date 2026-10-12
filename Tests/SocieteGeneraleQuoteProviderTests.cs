using System.Net;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class SocieteGeneraleQuoteProviderTests
{
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

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private const string Product = "{\"Id\":1234567,\"Isin\":\"DE000FAAD001\",\"Currency\":\"EUR\",\"Name\":\"Turbo Open-End Long su Oro\",\"Status\":65}";

    private const string Ticks =
        "[{\"Ask\":0.096,\"Bid\":0.086,\"Date\":\"2026-10-12T10:57:09.56+02:00\",\"UnderlyingPrice\":0}," +
        "{\"Ask\":0.097,\"Bid\":0.087,\"Date\":\"2026-10-12T10:58:01.578+02:00\",\"UnderlyingPrice\":0}]";

    private static HttpResponseMessage Route(HttpRequestMessage request, string live, string intraday = "[]")
    {
        var path = request.RequestUri!.AbsolutePath;
        return path.EndsWith("/Products/DE000FAAD001") ? Json(Product)
            : path.EndsWith("/Prices/Live") ? Json(live)
            : path.EndsWith("/Prices/Intraday") ? Json(intraday)
            : Json("{}", HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("DE000FAAD001", true)]
    [InlineData("de000faaa006", true)]
    [InlineData("DE000SAAA007", true)]
    [InlineData("DE000VAAA001", false)]          // di Vontobel
    [InlineData("DE000BDAA002", false)]
    [InlineData("IE00TEST0001", false)]
    [InlineData(null, false)]
    public void Only_societe_generale_isins_are_covered(string? isin, bool expected) =>
        Assert.Equal(expected, SocieteGeneraleQuoteProvider.Covers(isin));

    [Fact]
    public void Each_issuer_has_its_own_reader_for_the_update()
    {
        using var vontobel = AssetUpdateService.DefaultDerivativeProvider(Issuer.Vontobel);
        using var societeGenerale = AssetUpdateService.DefaultDerivativeProvider(Issuer.SocieteGenerale);

        Assert.IsType<VontobelQuoteProvider>(vontobel);
        Assert.IsType<SocieteGeneraleQuoteProvider>(societeGenerale);
        Assert.Equal("societegenerale", societeGenerale!.Source);
        Assert.Equal("Société Générale", societeGenerale.IssuerName);
    }

    [Fact]
    public void The_addresses_carry_the_isin_in_capitals_and_the_internal_number()
    {
        Assert.Equal("https://prodotti.societegenerale.it/EmcWebApi/api/Products/DE000FAAD001", SocieteGeneraleQuoteProvider.ProductUrl("  de000faad001 "));
        Assert.Equal("https://prodotti.societegenerale.it/EmcWebApi/api/Prices/Live?productId=1234567", SocieteGeneraleQuoteProvider.LiveUrl(1234567));
        Assert.Equal("https://prodotti.societegenerale.it/EmcWebApi/api/Prices/Intraday?productId=1234567", SocieteGeneraleQuoteProvider.IntradayUrl(1234567));
    }

    [Fact]
    public void The_product_card_gives_number_name_and_currency()
    {
        var product = SocieteGeneraleQuoteProvider.ParseProduct(Product);

        Assert.NotNull(product);
        Assert.Equal(1234567, product!.Id);
        Assert.Equal("Turbo Open-End Long su Oro", product.Name);
        Assert.Equal("EUR", product.Currency);

        // senza valuta si presume l'euro; senza numero, o se non è una scheda, non c'è nulla da leggere
        Assert.Equal("EUR", SocieteGeneraleQuoteProvider.ParseProduct("{\"Id\":5}")!.Currency);
        Assert.Null(SocieteGeneraleQuoteProvider.ParseProduct("{\"Name\":\"x\"}"));
        Assert.Null(SocieteGeneraleQuoteProvider.ParseProduct("[]"));
        Assert.Null(SocieteGeneraleQuoteProvider.ParseProduct("<html>"));
    }

    [Fact]
    public void The_latest_tick_with_a_price_wins_whatever_the_order_and_the_date_format()
    {
        var tick = SocieteGeneraleQuoteProvider.LastTick(
            "[{\"Bid\":1,\"Ask\":1.1,\"Date\":\"2026-10-12T08:00:00Z\"}," +
            "{\"Bid\":3,\"Ask\":3.1,\"Date\":\"2026-10-12T10:30:00+02:00\"}," +          // 08:30 UTC: la più recente
            "{\"Bid\":2,\"Ask\":2.1,\"Date\":\"2026-10-12T08:10:00Z\"}," +
            "{\"Bid\":null,\"Ask\":null,\"Date\":\"2026-10-12T23:00:00Z\"}]");           // senza prezzi: si salta

        Assert.NotNull(tick);
        Assert.Equal(3m, tick!.Bid);
        Assert.Equal(3.1m, tick.Ask);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc), tick.DateUtc);
    }

    [Fact]
    public void An_empty_or_unreadable_list_has_no_tick()
    {
        Assert.Null(SocieteGeneraleQuoteProvider.LastTick("[]"));
        Assert.Null(SocieteGeneraleQuoteProvider.LastTick("{\"Bid\":1}"));
        Assert.Null(SocieteGeneraleQuoteProvider.LastTick("<html>"));
        Assert.Null(SocieteGeneraleQuoteProvider.LastTick("[{\"Date\":\"2026-10-12T08:00:00Z\"}]"));
    }

    [Theory]
    [InlineData("Turbo Open-End Long su Oro", "Turbo Open-End su Oro")]
    [InlineData("Turbo Open-End Short su EUR/USD", "Turbo Open-End su EUR/USD")]
    [InlineData("turbo open-end long su Oracle Corp", "turbo open-end su Oracle Corp")]
    [InlineData("Mini Future su Oro", "Mini Future su Oro")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void The_direction_is_taken_out_of_the_product_name(string? siteName, string? expected) =>
        Assert.Equal(expected, SocieteGeneraleQuoteProvider.ProductNameFrom(siteName));

    [Fact]
    public async Task The_bid_is_the_price_and_the_time_is_that_of_the_last_tick()
    {
        var handler = new FakeHandler(r => Route(r, Ticks));
        using var provider = new SocieteGeneraleQuoteProvider(handler);

        var result = await provider.GetQuoteAsync("DE000FAAD001", CancellationToken.None);

        Assert.Equal(DerivativeLookupStatus.Ok, result.Status);
        var quote = result.Quote!;
        Assert.Equal(0.087m, quote.Price);
        Assert.Equal(0.087m, quote.Bid);
        Assert.Equal(0.097m, quote.Ask);
        Assert.Equal("EUR", quote.Currency);
        Assert.Equal("Turbo Open-End su Oro", quote.ProductName);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 58, 1, 578, DateTimeKind.Utc), quote.StampUtc);

        // una richiesta per la scheda e una per i prezzi, dichiarandosi «Pelago»
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Contains("Pelago", r.Headers.UserAgent.ToString()));
        Assert.Contains("application/json", handler.Requests[0].Headers.Accept.ToString());
        Assert.EndsWith("/Prices/Live?productId=1234567", handler.Requests[1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task When_nothing_traded_in_the_last_moments_the_day_s_prices_are_used()
    {
        var evening = "[{\"Ask\":0.2,\"Bid\":0.19,\"Date\":\"2026-10-09T20:00:00Z\"}]";
        var handler = new FakeHandler(r => Route(r, live: "[]", intraday: evening));
        using var provider = new SocieteGeneraleQuoteProvider(handler);

        var result = await provider.GetQuoteAsync("DE000FAAD001", CancellationToken.None);

        Assert.Equal(DerivativeLookupStatus.Ok, result.Status);
        Assert.Equal(0.19m, result.Quote!.Price);
        Assert.Equal(new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc), result.Quote.StampUtc);
        Assert.Equal(3, handler.Requests.Count);
        Assert.EndsWith("/Prices/Intraday?productId=1234567", handler.Requests[2].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task A_product_with_no_tick_anywhere_has_no_price_and_a_missing_bid_counts_as_zero()
    {
        using var empty = new SocieteGeneraleQuoteProvider(new FakeHandler(r => Route(r, "[]", "[]")));
        Assert.Equal(DerivativeLookupStatus.NoPrice, (await empty.GetQuoteAsync("DE000FAAD001", CancellationToken.None)).Status);

        using var askOnly = new SocieteGeneraleQuoteProvider(new FakeHandler(r => Route(r, "[{\"Bid\":null,\"Ask\":0.5,\"Date\":\"2026-10-12T08:00:00Z\"}]")));
        var result = await askOnly.GetQuoteAsync("DE000FAAD001", CancellationToken.None);
        Assert.Equal(DerivativeLookupStatus.Ok, result.Status);
        Assert.Equal(0m, result.Quote!.Price);
        Assert.Equal(0.5m, result.Quote.Ask);
    }

    [Fact]
    public async Task An_unknown_product_is_not_found_and_odd_replies_count_as_a_refusal()
    {
        using var unknown = new SocieteGeneraleQuoteProvider(new FakeHandler(_ => Json("{}", HttpStatusCode.NotFound)));
        var notFound = await unknown.GetQuoteAsync("DE000FAAB009", CancellationToken.None);
        Assert.Equal(DerivativeLookupStatus.NotFound, notFound.Status);
        Assert.Equal(404, notFound.HttpStatus);

        using var refused = new SocieteGeneraleQuoteProvider(new FakeHandler(_ => Json("{}", (HttpStatusCode)429)));
        var blocked = await refused.GetQuoteAsync("DE000FAAD001", CancellationToken.None);
        Assert.Equal(DerivativeLookupStatus.Blocked, blocked.Status);
        Assert.Equal(429, blocked.HttpStatus);

        using var notACard = new SocieteGeneraleQuoteProvider(new FakeHandler(_ => Json("<html>manutenzione</html>")));
        Assert.Equal(DerivativeLookupStatus.Blocked, (await notACard.GetQuoteAsync("DE000FAAD001", CancellationToken.None)).Status);

        using var failsOnPrices = new SocieteGeneraleQuoteProvider(new FakeHandler(r =>
            r.RequestUri!.AbsolutePath.Contains("/Products/") ? Json(Product) : Json("{}", HttpStatusCode.InternalServerError)));
        Assert.Equal(DerivativeLookupStatus.Blocked, (await failsOnPrices.GetQuoteAsync("DE000FAAD001", CancellationToken.None)).Status);
    }
}
