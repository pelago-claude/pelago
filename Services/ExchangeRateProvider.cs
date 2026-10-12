using System.Net.Http.Json;
using static DPOper.Services.Loc;
using System.Text.Json.Serialization;

namespace DPOper.Services;

/// <summary>
/// Recupera il tasso di cambio tra due valute: quello corrente da Yahoo Finance (con ripiego sul riferimento BCE) e quelli storici da
/// Frankfurter (API pubblica, gratuita, senza chiave, basata sui tassi di riferimento della Banca Centrale Europea).
/// </summary>
public sealed class ExchangeRateProvider : IDisposable
{
    private readonly HttpClient _httpClient;

    private sealed class RatesResponse
    {
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }

    public ExchangeRateProvider()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/2.3");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Quante unità di <paramref name="to"/> vale un'unità di <paramref name="from"/>, al cambio più recente: quello di mercato di Yahoo
    /// Finance (aggiornato al secondo) se è plausibile, altrimenti il cambio di riferimento della BCE (una volta al giorno).
    /// Così il prezzo estero e il cambio con cui lo si converte sono dello stesso momento.
    /// </summary>
    public async Task<decimal> GetRateAsync(string from, string to, CancellationToken cancellationToken)
    {
        decimal? ecb = null;
        Exception? ecbError = null;
        try
        {
            ecb = await GetEcbRateAsync(from, to, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            ecbError = ex;
        }

        var live = await TryGetLiveRateAsync(from, to, cancellationToken);
        if (live is { } l && (ecb is null || Math.Abs(l / ecb.Value - 1m) <= FxBoard.MaxLiveDeviation))
        {
            return l;
        }

        return ecb ?? throw (ecbError ?? new InvalidOperationException(T("Nessun cambio {0}/{1} disponibile.", from, to)));
    }

    private async Task<decimal?> TryGetLiveRateAsync(string from, string to, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(
                $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(from)}{Uri.EscapeDataString(to)}=X?range=1d&interval=1d", cancellationToken);
            return FxBoard.ParseYahooMeta(json) is { Price: > 0m } quote ? quote.Price : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<decimal> GetEcbRateAsync(string from, string to, CancellationToken cancellationToken)
    {
        var url = $"https://api.frankfurter.dev/v1/latest?base={Uri.EscapeDataString(from)}&symbols={Uri.EscapeDataString(to)}";
        var response = await _httpClient.GetFromJsonAsync<RatesResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Risposta vuota da Frankfurter.");

        if (!response.Rates.TryGetValue(to, out var rate))
        {
            throw new InvalidOperationException($"Frankfurter non ha restituito un tasso {from}/{to}.");
        }

        return rate;
    }


    /// <summary>Come <see cref="GetRateAsync"/>, ma per un giorno passato (Frankfurter usa l'ultimo giorno di borsa utile).</summary>
    public async Task<decimal> GetRateAtAsync(string from, string to, DateOnly date, CancellationToken cancellationToken)
    {
        var url = $"https://api.frankfurter.dev/v1/{date:yyyy-MM-dd}?base={Uri.EscapeDataString(from)}&symbols={Uri.EscapeDataString(to)}";
        var response = await _httpClient.GetFromJsonAsync<RatesResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Risposta vuota da Frankfurter.");

        return response.Rates.TryGetValue(to, out var rate)
            ? rate
            : throw new InvalidOperationException(T("Frankfurter non ha restituito un tasso {0}/{1} per il {2:dd/MM/yyyy}.", from, to, date));
    }
    public void Dispose() => _httpClient.Dispose();
}
