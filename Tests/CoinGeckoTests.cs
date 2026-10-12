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
}
