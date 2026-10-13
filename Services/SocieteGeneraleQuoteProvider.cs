using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pelago.Services;

/// <summary>
/// Prezzi dei derivati emessi da Société Générale, letti dal sito dei prodotti per l'Italia (<c>prodotti.societegenerale.it</c>). La pagina del prodotto è
/// un guscio senza prezzi: i dati li prende da due chiamate pubbliche, le stesse che fa il sito. La prima (<c>Products/ISIN</c>) dà il numero interno e il nome
/// del prodotto, la seconda (<c>Prices/Live</c>, con ripiego su <c>Prices/Intraday</c> quando negli ultimi istanti non ci sono scambi, per esempio di sera) i
/// Geld e Brief. Si manda al sito solo il codice del prodotto, una richiesta alla volta, con una pausa, dichiarandosi «Pelago». Si coprono i prodotti il cui
/// ISIN inizia per <c>DE000F</c> o <c>DE000S</c> (vedi <see cref="Issuers"/>).
///
/// Le Note Legali del sito vietano la riproduzione dei contenuti «con qualsiasi mezzo» (memorizzazione compresa) senza autorizzazione scritta, e la pagina chiede
/// di accettare una dichiarazione (informazioni per chi risiede in Italia): per questo la funzione è spenta finché l'utente non la attiva, dopo aver letto
/// questi avvisi (vedi <see cref="AppState.SocGenEnabled"/>).
/// </summary>
public sealed class SocieteGeneraleQuoteProvider : IDerivativeProvider
{
    public const string SourceName = "societegenerale";

    private const string Api = "https://prodotti.societegenerale.it/EmcWebApi/api";

    // «Turbo Open-End Long su Oro»: la direzione si toglie, perché sta già nel nome del file («Long 4.100 $»)
    private static readonly Regex Direction = new(@"\s+(Long|Short)(?=\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;

    public SocieteGeneraleQuoteProvider(HttpMessageHandler? handler = null)
    {
        _http = PelagoHttp.Create(handler, "it-IT,it;q=0.9");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public string Source => SourceName;

    public string IssuerName => Issuers.Name(Issuer.SocieteGenerale);

    /// <summary>Il prodotto è di Société Générale (lo dice il prefisso del codice ISIN)?</summary>
    public static bool Covers(string? isin) => Issuers.OfIsin(isin) == Issuer.SocieteGenerale;

    public static string ProductUrl(string isin) => $"{Api}/Products/{isin.Trim().ToUpperInvariant()}";

    public static string LiveUrl(long productId) => $"{Api}/Prices/Live?productId={productId}";

    public static string IntradayUrl(long productId) => $"{Api}/Prices/Intraday?productId={productId}";

    public async Task<DerivativeLookup> GetQuoteAsync(string isin, CancellationToken cancellationToken)
    {
        var productReply = await GetAsync(ProductUrl(isin), cancellationToken);
        if (productReply.Failure is { } productFailure)
        {
            return productFailure;
        }

        var product = ParseProduct(productReply.Body);
        if (product is null)
        {
            // risposta che non è quella attesa: la si tratta come un rifiuto del sito
            return new DerivativeLookup(DerivativeLookupStatus.Blocked, HttpStatus: 200);
        }

        var live = await GetAsync(LiveUrl(product.Id), cancellationToken);
        if (live.Failure is { } liveFailure)
        {
            return liveFailure;
        }
        var tick = LastTick(live.Body);

        if (tick is null)
        {
            // negli ultimi istanti nessuno scambio (sera, fine settimana): si guarda l'ultimo tratto della giornata
            var intraday = await GetAsync(IntradayUrl(product.Id), cancellationToken);
            if (intraday.Failure is { } intradayFailure)
            {
                return intradayFailure;
            }
            tick = LastTick(intraday.Body);
        }

        if (tick is null)
        {
            return new DerivativeLookup(DerivativeLookupStatus.NoPrice, HttpStatus: 200);
        }

        var quote = new DerivativeQuote(tick.Bid ?? 0m, tick.Bid, tick.Ask, null, tick.DateUtc, product.Currency,
            ProductName: ProductNameFrom(product.Name), StampUtc: tick.DateUtc);
        return new DerivativeLookup(DerivativeLookupStatus.Ok, quote, 200);
    }

    internal sealed record Product(long Id, string? Name, string Currency);

    internal sealed record Tick(decimal? Bid, decimal? Ask, DateTime? DateUtc);

    private sealed record Reply(DerivativeLookup? Failure, string Body = "");

    private async Task<Reply> GetAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        var status = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new Reply(new DerivativeLookup(DerivativeLookupStatus.NotFound, HttpStatus: status));
        }
        if (!response.IsSuccessStatusCode)
        {
            return new Reply(new DerivativeLookup(DerivativeLookupStatus.Blocked, HttpStatus: status));
        }

        return new Reply(null, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>Legge numero interno, nome e valuta dalla scheda del prodotto; null se la risposta non è una scheda.</summary>
    internal static Product? ParseProduct(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryProperty(root, "Id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var number))
            {
                return null;
            }

            var name = TryProperty(root, "Name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var currency = TryProperty(root, "Currency", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString()) ? c.GetString()! : "EUR";
            return new Product(number, name, currency);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>L'ultimo scambio dell'elenco (quello con la data più recente) che ha almeno un prezzo; null se l'elenco è vuoto o non è un elenco.</summary>
    internal static Tick? LastTick(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            Tick? best = null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var bid = Number(item, "Bid");
                var ask = Number(item, "Ask");
                if (bid is null && ask is null)
                {
                    continue;
                }

                DateTime? date = TryProperty(item, "Date", out var d) && d.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(d.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                        ? parsed.UtcDateTime
                        : null;
                if (best is null || IsNewer(date, best.DateUtc))
                {
                    best = new Tick(bid, ask, date);
                }
            }
            return best;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsNewer(DateTime? candidate, DateTime? current) =>
        candidate is null ? current is null : current is null || candidate >= current;

    /// <summary>Il nome del prodotto senza la direzione: «Turbo Open-End Long su Oro» diventa «Turbo Open-End su Oro».</summary>
    internal static string? ProductNameFrom(string? siteName)
    {
        if (string.IsNullOrWhiteSpace(siteName))
        {
            return null;
        }
        return Direction.Replace(siteName.Trim(), "", 1).Trim();
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static decimal? Number(JsonElement parent, string name) =>
        TryProperty(parent, name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;

    public void Dispose() => _http.Dispose();
}
