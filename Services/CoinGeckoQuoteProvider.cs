using System.Net.Http.Json;
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

    public void Dispose() => _httpClient.Dispose();
}
