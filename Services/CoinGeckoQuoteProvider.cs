using System.Net.Http.Json;
using System.Text.Json;
using static DPOper.Services.Loc;

namespace DPOper.Services;

/// <summary>
/// Recupera la quotazione corrente di una singola crypto da CoinGecko (API pubblica, senza chiave).
/// </summary>
public sealed class CoinGeckoQuoteProvider : IDisposable
{
    private readonly HttpClient _httpClient;

    public CoinGeckoQuoteProvider()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Senza uno User-Agent "da browser" CoinGecko risponde 403 (HttpClient non ne invia uno di default).
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DPOper/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<decimal> GetPriceEurAsync(string coinGeckoId, CancellationToken cancellationToken)
    {
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(coinGeckoId)}&vs_currencies=eur";
        var response = await _httpClient.GetFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(url, cancellationToken)
            ?? throw new InvalidOperationException(T("Risposta vuota da CoinGecko."));

        if (!response.TryGetValue(coinGeckoId, out var prices) || !prices.TryGetValue("eur", out var price))
        {
            throw new InvalidOperationException(T("CoinGecko non ha restituito una quotazione per \"{0}\".", coinGeckoId));
        }

        return price;
    }

    /// <summary>
    /// Cerca su CoinGecko l'id della crypto con questo simbolo (per esempio «QNT» → «quant-network»). Serve perché l'id non è
    /// quello che si legge nell'indirizzo della pagina: la pagina di Quant è <c>.../coins/quant</c>, ma con «quant» il prezzo
    /// è quello di un'altra moneta. Restituisce null se non c'è nessuna crypto con quel simbolo e una capitalizzazione nota.
    /// </summary>
    public async Task<string?> FindIdAsync(string symbol, CancellationToken cancellationToken)
    {
        var url = $"https://api.coingecko.com/api/v3/search?query={Uri.EscapeDataString(symbol.Trim())}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return PickId(await response.Content.ReadAsStringAsync(cancellationToken), symbol);
    }

    /// <summary>
    /// Dalla risposta della ricerca sceglie la moneta il cui simbolo è proprio quello cercato, e tra più monete con lo stesso
    /// simbolo quella con la capitalizzazione più alta. Quelle senza capitalizzazione (token sconosciuti) non si accettano:
    /// meglio nessun prezzo che il prezzo di una moneta sbagliata.
    /// </summary>
    internal static string? PickId(string json, string symbol)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("coins", out var coins) || coins.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? bestId = null;
            var bestRank = long.MaxValue;
            foreach (var coin in coins.EnumerateArray())
            {
                if (!coin.TryGetProperty("symbol", out var coinSymbol)
                    || !string.Equals(coinSymbol.GetString(), symbol.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !coin.TryGetProperty("id", out var idElement) || idElement.GetString() is not { Length: > 0 } id
                    || !coin.TryGetProperty("market_cap_rank", out var rankElement) || rankElement.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var rank = rankElement.GetInt64();
                if (rank < bestRank)
                {
                    bestId = id;
                    bestRank = rank;
                }
            }

            return bestId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
