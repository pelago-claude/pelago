using System.Net.Http.Json;
using static Pelago.Services.Loc;
using System.Text.Json.Serialization;

namespace Pelago.Services;

/// <summary>
/// Risolve un ISIN nel ticker Yahoo Finance corrispondente e ne recupera la quotazione corrente.
/// API non ufficiale (nessuna documentazione pubblica, nessuna chiave): può cambiare o smettere di
/// funzionare senza preavviso, stesso rischio già accettato per CoinGecko.
/// </summary>
public sealed class YahooFinanceQuoteProvider : IDisposable
{
    private readonly HttpClient _httpClient;

    private sealed class SearchResponse
    {
        [JsonPropertyName("quotes")]
        public List<SearchQuote> Quotes { get; set; } = new();
    }

    private sealed class SearchQuote
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = "";

        [JsonPropertyName("quoteType")]
        public string QuoteType { get; set; } = "";

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    private sealed class ChartResponse
    {
        [JsonPropertyName("chart")]
        public ChartBody Chart { get; set; } = new();
    }

    private sealed class ChartBody
    {
        [JsonPropertyName("result")]
        public List<ChartResult>? Result { get; set; }
    }

    private sealed class ChartResult
    {
        [JsonPropertyName("meta")]
        public ChartMeta Meta { get; set; } = new();
    }

    private sealed class ChartMeta
    {
        [JsonPropertyName("regularMarketPrice")]
        public decimal RegularMarketPrice { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "";
    }

    public YahooFinanceQuoteProvider()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>Cerca il ticker Yahoo Finance più plausibile per un ISIN. Null se non trovato.</summary>
    public async Task<string?> ResolveTickerAsync(string isin, CancellationToken cancellationToken)
    {
        var url = $"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(isin)}";
        var response = await _httpClient.GetFromJsonAsync<SearchResponse>(url, cancellationToken);

        var best = response?.Quotes
            .Where(q => q.QuoteType is "EQUITY" or "ETF" or "MUTUALFUND")
            .OrderByDescending(q => q.Score)
            .FirstOrDefault();

        return best?.Symbol;
    }

    public async Task<(decimal Price, string Currency)> GetQuoteAsync(string ticker, CancellationToken cancellationToken)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&range=1d";
        var response = await _httpClient.GetFromJsonAsync<ChartResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException(T("Risposta vuota da Yahoo Finance."));

        var meta = response.Chart.Result?.FirstOrDefault()?.Meta
            ?? throw new InvalidOperationException(T("Yahoo Finance non ha restituito una quotazione per \"{0}\".", ticker));

        return (meta.RegularMarketPrice, meta.Currency);
    }

    public void Dispose() => _httpClient.Dispose();
}
