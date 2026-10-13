using System.Net.Http.Json;
using System.Text.Json;
using static Pelago.Services.Loc;

namespace Pelago.Services;

/// <summary>
/// Recupera le quotazioni correnti delle crypto da CoinGecko (API pubblica, senza chiave). Le chiede tutte insieme con una
/// sola richiesta: l'API gratuita rifiuta (errore 429) quelle che arrivano in raffica dopo le prime cinque o sei, e con una
/// richiesta per moneta restavano senza prezzo proprio le ultime della lista.
/// </summary>
public sealed class CoinGeckoQuoteProvider : IDisposable
{
    private readonly HttpClient _httpClient;

    public CoinGeckoQuoteProvider() : this(new HttpClientHandler())
    {
    }

    /// <summary>Per le prove automatiche: un gestore delle richieste finto al posto della rete.</summary>
    internal CoinGeckoQuoteProvider(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        // Senza uno User-Agent "da browser" CoinGecko risponde 403 (HttpClient non ne invia uno di default).
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>Un id CoinGecko è fatto di lettere minuscole, cifre, trattini e punti: altro vuol dire un errore nel catalogo, e non si manda al sito.</summary>
    public static bool IsValidId(string? id) =>
        id is { Length: > 0 and <= 100 } && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9][a-z0-9._-]*$");

    /// <summary>
    /// I prezzi in euro di più crypto con una richiesta sola. Nel risultato mancano gli id che il sito non conosce. Se il sito dice
    /// «troppe richieste» (429) si ferma con un messaggio chiaro invece di lasciare l'errore tecnico.
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetPricesEurAsync(IEnumerable<string> coinGeckoIds, CancellationToken cancellationToken)
    {
        var ids = coinGeckoIds.Where(IsValidId).Distinct().ToList();
        var result = new Dictionary<string, decimal>();
        if (ids.Count == 0)
        {
            return result;
        }

        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={string.Join(",", ids)}&vs_currencies=eur";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if ((int)response.StatusCode == 429)
        {
            throw new InvalidOperationException(T("CoinGecko ha limitato le richieste (troppe in poco tempo): riprova tra un minuto."));
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(cancellationToken)
            ?? throw new InvalidOperationException(T("Risposta vuota da CoinGecko."));
        foreach (var id in ids)
        {
            if (body.TryGetValue(id, out var prices) && prices.TryGetValue("eur", out var price))
            {
                result[id] = price;
            }
        }

        return result;
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
        if ((int)response.StatusCode == 429)
        {
            throw new InvalidOperationException(T("CoinGecko ha limitato le richieste (troppe in poco tempo): riprova tra un minuto."));
        }
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
