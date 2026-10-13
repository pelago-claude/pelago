using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pelago.Services;

/// <summary>
/// Prezzi dei derivati emessi da Vontobel, letti dalla pagina pubblica del prodotto (<c>markets.vontobel.com</c>): la pagina contiene un blocco di
/// dati (<c>__NEXT_DATA__</c>) con Geld, Brief e ultimo prezzo. Si manda al sito solo il codice del prodotto, in modo educato: una richiesta alla volta,
/// con una pausa, dichiarandosi «Pelago». Trade Republic offre solo derivati di Vontobel e Société Générale; qui si coprono
/// quelli di Vontobel (ISIN che inizia per <c>DE000V</c> o <c>DE000BD</c>).
///
/// Le condizioni d'uso del sito dicono che copie e scaricamenti sono solo per uso privato non commerciale e che il sito si rivolge a chi risiede in
/// Germania, Austria, Lussemburgo e Liechtenstein: per questo la funzione è spenta finché l'utente non la attiva (vedi <see cref="AppState.VontobelEnabled"/>).
/// </summary>
public sealed class VontobelQuoteProvider : IDerivativeProvider
{
    public const string SourceName = "vontobel";

    /// <summary>Una categoria qualunque tra quelle valide porta alla stessa pagina del prodotto: se ne usa una sola.</summary>
    private const string Category = "turbo-optionsscheine-open-end";

    private static readonly Regex NextData = new(@"<script id=""__NEXT_DATA__""[^>]*>(?<json>.*?)</script>", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public VontobelQuoteProvider(HttpMessageHandler? handler = null)
    {
        _http = PelagoHttp.Create(handler, "de-DE,de;q=0.9,it;q=0.6");
    }

    public string Source => SourceName;

    public string IssuerName => Issuers.Name(Issuer.Vontobel);

    /// <summary>Il prodotto è di Vontobel (lo dice il prefisso del codice ISIN)?</summary>
    public static bool Covers(string? isin) => Issuers.OfIsin(isin) == Issuer.Vontobel;

    public static string UrlFor(string isin) => $"https://markets.vontobel.com/de-de/produkte/hebel/{Category}/{isin.Trim().ToUpperInvariant()}";

    public async Task<DerivativeLookup> GetQuoteAsync(string isin, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(UrlFor(isin), cancellationToken);
        var status = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new DerivativeLookup(DerivativeLookupStatus.NotFound, HttpStatus: status);
        }
        if (!response.IsSuccessStatusCode)
        {
            return new DerivativeLookup(DerivativeLookupStatus.Blocked, HttpStatus: status);
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParsePage(html) is { } quote
            ? new DerivativeLookup(DerivativeLookupStatus.Ok, quote, status)
            : new DerivativeLookup(DerivativeLookupStatus.NoPrice, HttpStatus: status);
    }

    /// <summary>
    /// Legge il prezzo dal blocco di dati della pagina (<c>props.pageProps.additionalData.price</c>). Il prezzo di valutazione è il Geld; se manca si ripiega
    /// sull'ultimo prezzo. Restituisce null se la pagina non ha un prezzo. Dallo stesso blocco si legge il nome del sottostante (<c>data.shortName</c>).
    /// </summary>
    public static DerivativeQuote? ParsePage(string html)
    {
        var match = NextData.Match(html);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(match.Groups["json"].Value);
            if (!TryGet(document.RootElement, out var price, "props", "pageProps", "additionalData", "price"))
            {
                return null;
            }

            var bid = Number(price, "bid");
            var ask = Number(price, "ask");
            var latest = Number(price, "latest");
            var value = bid ?? latest ?? Number(price, "valuation");
            if (value is null)
            {
                return null;
            }

            DateTime? latestUtc = price.TryGetProperty("latestTimestamp", out var ts) && ts.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed.UtcDateTime
                    : null;
            var currency = price.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(cur.GetString())
                ? cur.GetString()!
                : "EUR";

            return new DerivativeQuote(value.Value, bid, ask, latest, latestUtc, currency, ProductName: ProductNameOf(document.RootElement));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Il nome del prodotto: la pagina non ne dà uno già fatto, ma dice su quale titolo è il prodotto («Acme Energy»). Qui si compone «Turbo su Acme Energy»:
    /// la direzione e il livello (come «Long 123,59 $») stanno già nel nome del file e restano in coda.
    /// </summary>
    private static string? ProductNameOf(JsonElement root)
    {
        if (!TryGet(root, out var data, "props", "pageProps", "additionalData", "data"))
        {
            return null;
        }

        var underlying = Text(data, "shortName") ?? Text(data, "name");
        return underlying is null ? null : $"Turbo su {underlying}";
    }

    private static bool TryGet(JsonElement element, out JsonElement found, params string[] path)
    {
        found = element;
        foreach (var name in path)
        {
            if (found.ValueKind != JsonValueKind.Object || !found.TryGetProperty(name, out found))
            {
                return false;
            }
        }
        return found.ValueKind == JsonValueKind.Object;
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static decimal? Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

    public void Dispose() => _http.Dispose();
}
