using DPOper.Models;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class CoinGeckoTests
{
    private const string QuantSearch = "{\"coins\":[" +
        "{\"id\":\"quant-network\",\"name\":\"Quant\",\"api_symbol\":\"quant-network\",\"symbol\":\"QNT\",\"market_cap_rank\":35}," +
        "{\"id\":\"quantinuum-bstocks-tokenized-stock\",\"name\":\"Altro\",\"symbol\":\"QNTB\",\"market_cap_rank\":3264}," +
        "{\"id\":\"quantra\",\"name\":\"Senza capitalizzazione\",\"symbol\":\"QNTR\",\"market_cap_rank\":null}]}";

    [Fact]
    public void The_search_picks_the_coin_whose_symbol_is_exactly_the_one_asked()
    {
        Assert.Equal("quant-network", CoinGeckoQuoteProvider.PickId(QuantSearch, "QNT"));
        Assert.Equal("quant-network", CoinGeckoQuoteProvider.PickId(QuantSearch, "  qnt "));      // maiuscole e spazi non contano
        Assert.Null(CoinGeckoQuoteProvider.PickId(QuantSearch, "QUANT"));                         // il nome della pagina non è un simbolo
        Assert.Null(CoinGeckoQuoteProvider.PickId(QuantSearch, "XYZ"));
    }

    [Fact]
    public void Among_coins_with_the_same_symbol_the_biggest_one_wins_and_unknown_tokens_are_refused()
    {
        var json = "{\"coins\":[" +
            "{\"id\":\"piccola\",\"symbol\":\"ADA\",\"market_cap_rank\":187}," +
            "{\"id\":\"cardano\",\"symbol\":\"ADA\",\"market_cap_rank\":17}," +
            "{\"id\":\"sconosciuta\",\"symbol\":\"ADA\",\"market_cap_rank\":null}]}";

        Assert.Equal("cardano", CoinGeckoQuoteProvider.PickId(json, "ADA"));
        // se l'unica con quel simbolo non ha una capitalizzazione, meglio nessun prezzo che quello di una moneta sbagliata
        Assert.Null(CoinGeckoQuoteProvider.PickId(QuantSearch, "QNTR"));
    }

    [Fact]
    public void An_unreadable_or_empty_answer_finds_nothing()
    {
        Assert.Null(CoinGeckoQuoteProvider.PickId("<html>manutenzione</html>", "QNT"));
        Assert.Null(CoinGeckoQuoteProvider.PickId("{\"coins\":[]}", "QNT"));
        Assert.Null(CoinGeckoQuoteProvider.PickId("{\"altro\":1}", "QNT"));
        Assert.Null(CoinGeckoQuoteProvider.PickId("[]", "QNT"));
    }

    [Fact]
    public void Quant_and_cardano_are_known_and_an_old_entry_without_an_id_gets_completed()
    {
        var path = Path.Combine(DataLocations.Root, "catalog.json");
        File.Delete(path);
        try
        {
            var operations = new List<Operation>
            {
                Ops.Buy("2026-09-01", "QNT", "CRYPTO", 1m, 100m),
                Ops.Buy("2026-09-01", "ADA", "CRYPTO", 100m, 0.5m),
                Ops.Buy("2026-09-01", "ZZZ", "CRYPTO", 1m, 1m),       // simbolo che l'app non conosce: l'id si cerca online al primo aggiornamento
            };

            var catalog = AssetCatalogStore.SyncFromOperations(operations, "CRYPTO");
            Assert.Equal("quant-network", catalog.Single(e => e.Symbol == "QNT").ProviderMappings["coingecko"]);
            Assert.Equal("cardano", catalog.Single(e => e.Symbol == "ADA").ProviderMappings["coingecko"]);
            Assert.False(catalog.Single(e => e.Symbol == "ZZZ").ProviderMappings.ContainsKey("coingecko"));

            // una voce creata prima, senza id: al sincronismo successivo si completa, e il catalogo si salva
            var old = AssetCatalogStore.Load();
            old.Single(e => e.Symbol == "QNT").ProviderMappings.Clear();
            old.Single(e => e.Symbol == "ADA").ProviderMappings["coingecko"] = "";
            AssetCatalogStore.Save(old);

            var healed = AssetCatalogStore.SyncFromOperations(operations, "CRYPTO");
            Assert.Equal("quant-network", healed.Single(e => e.Symbol == "QNT").ProviderMappings["coingecko"]);
            Assert.Equal("cardano", healed.Single(e => e.Symbol == "ADA").ProviderMappings["coingecko"]);
            Assert.Equal("quant-network", AssetCatalogStore.Load().Single(e => e.Symbol == "QNT").ProviderMappings["coingecko"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------- una richiesta sola per tutte le crypto

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public List<string> Paths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, System.Net.HttpStatusCode code = System.Net.HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static AssetCatalogEntry Crypto(string symbol, string? id = null) => new()
    {
        Key = symbol, Name = symbol, Symbol = symbol, AssetClass = "CRYPTO", FileName = symbol + ".json",
        ProviderMappings = id is null ? new() : new() { ["coingecko"] = id },
    };

    [Fact]
    public async Task Ten_known_cryptos_are_asked_with_a_single_request()
    {
        var ids = new[] { "bitcoin", "ethereum", "ripple", "solana", "near", "dogecoin", "stellar", "hedera-hashgraph", "cardano", "quant-network" };
        var handler = new FakeHandler(_ => Json("{" + string.Join(",", ids.Select((id, i) => $"\"{id}\":{{\"eur\":{i + 1}.5}}")) + "}"));
        using var provider = new CoinGeckoQuoteProvider(handler);
        var entries = ids.Select((id, i) => Crypto("S" + i, id)).ToList();

        var outcomes = await AssetUpdateService.LookUpCryptoAsync(entries, provider, CancellationToken.None, 0);

        var path = Assert.Single(handler.Paths);                                           // una richiesta, non dieci
        Assert.StartsWith("/api/v3/simple/price?ids=bitcoin,ethereum,", path);
        Assert.Contains("quant-network", path);
        Assert.Equal(10, outcomes.Count);
        Assert.All(outcomes.Values, o => Assert.NotNull(o.Price));
        Assert.Equal(10.5m, outcomes["S9"].Price);                                         // l'ultima della lista ha il suo prezzo
    }

    [Fact]
    public async Task When_the_site_says_too_many_requests_every_crypto_gets_a_clear_message()
    {
        var handler = new FakeHandler(_ => Json("{}", (System.Net.HttpStatusCode)429));
        using var provider = new CoinGeckoQuoteProvider(handler);

        var outcomes = await AssetUpdateService.LookUpCryptoAsync(new List<AssetCatalogEntry> { Crypto("ADA", "cardano"), Crypto("BTC", "bitcoin") }, provider, CancellationToken.None, 0);

        Assert.Single(handler.Paths);
        Assert.All(outcomes.Values, o =>
        {
            Assert.Null(o.Price);
            Assert.Contains("troppe", o.Error);                                            // non un errore tecnico HTTP
        });
    }

    [Fact]
    public async Task A_coin_the_site_does_not_return_fails_alone_and_a_malformed_id_is_never_sent()
    {
        var handler = new FakeHandler(_ => Json("{\"bitcoin\":{\"eur\":70000}}"));
        using var provider = new CoinGeckoQuoteProvider(handler);

        var outcomes = await AssetUpdateService.LookUpCryptoAsync(
            new List<AssetCatalogEntry> { Crypto("BTC", "bitcoin"), Crypto("XXX", "non-esiste"), Crypto("YYY", "Quant Network!") }, provider, CancellationToken.None, 0);

        Assert.Equal(70000m, outcomes["BTC"].Price);
        Assert.Contains("non-esiste", outcomes["XXX"].Error);
        Assert.Contains("non è valido", outcomes["YYY"].Error);
        Assert.DoesNotContain("Quant", handler.Paths.Single());
    }

    [Fact]
    public async Task A_crypto_without_an_id_is_searched_once_then_priced_in_the_same_batch()
    {
        var path = Path.Combine(DataLocations.Root, "catalog.json");
        File.Delete(path);
        try
        {
            var qnt = Crypto("QNT");
            var btc = Crypto("BTC", "bitcoin");
            AssetCatalogStore.Save(new List<AssetCatalogEntry> { qnt, btc });
            var handler = new FakeHandler(r => r.RequestUri!.AbsolutePath.EndsWith("/search")
                ? Json(QuantSearch)
                : Json("{\"bitcoin\":{\"eur\":70000},\"quant-network\":{\"eur\":205.5}}"));
            using var provider = new CoinGeckoQuoteProvider(handler);

            var outcomes = await AssetUpdateService.LookUpCryptoAsync(new List<AssetCatalogEntry> { btc, qnt }, provider, CancellationToken.None, 0);

            Assert.Equal(2, handler.Paths.Count);                                          // una ricerca e una richiesta di prezzi
            Assert.StartsWith("/api/v3/search?query=QNT", handler.Paths[0]);
            Assert.Contains("ids=bitcoin,quant-network", handler.Paths[1]);
            Assert.Equal(205.5m, outcomes["QNT"].Price);
            Assert.Equal("quant-network", outcomes["QNT"].FoundNow);                       // si dice a quale moneta è stata associata
            Assert.Null(outcomes["BTC"].FoundNow);
            Assert.Equal("quant-network", AssetCatalogStore.Load().Single(e => e.Key == "QNT").ProviderMappings["coingecko"]);   // e si ricorda
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_symbol_the_search_cannot_find_is_reported_and_costs_no_price_request()
    {
        var handler = new FakeHandler(_ => Json("{\"coins\":[]}"));
        using var provider = new CoinGeckoQuoteProvider(handler);

        var outcomes = await AssetUpdateService.LookUpCryptoAsync(new List<AssetCatalogEntry> { Crypto("ZZQXJ") }, provider, CancellationToken.None, 0);

        Assert.Single(handler.Paths);
        Assert.Null(outcomes["ZZQXJ"].Price);
        Assert.Contains("ZZQXJ", outcomes["ZZQXJ"].Error);
    }

    [Theory]
    [InlineData("quant-network", true)]
    [InlineData("hedera-hashgraph", true)]
    [InlineData("matic.network", true)]
    [InlineData("Quant", false)]
    [InlineData("quant network", false)]
    [InlineData("a&b=c", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_well_formed_ids_are_accepted(string? id, bool expected) =>
        Assert.Equal(expected, CoinGeckoQuoteProvider.IsValidId(id));
}
