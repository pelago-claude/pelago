namespace Pelago.Services;

/// <summary>
/// Il prezzo di un derivato letto dal sito dell'emittente: l'ultimo Geld (quello a cui potresti vendere) e, se c'è, il Brief.
/// <paramref name="ProductName"/> è il nome del prodotto secondo l'emittente (senza il livello, che sta già nel nome del file: «Turbo su Oro»);
/// <paramref name="StampUtc"/> è l'ora a cui si riferisce il prezzo, se il sito la dà e non è «adesso» (per esempio l'ultimo scambio di venerdì sera).
/// </summary>
public sealed record DerivativeQuote(decimal Price, decimal? Bid, decimal? Ask, decimal? Latest, DateTime? LatestUtc, string Currency,
    string? ProductName = null, DateTime? StampUtc = null);

public enum DerivativeLookupStatus
{
    Ok,

    /// <summary>La pagina non esiste: prodotto scaduto o non più in elenco.</summary>
    NotFound,

    /// <summary>La pagina c'è ma non mostra un prezzo (prodotto scaduto o con knock-out).</summary>
    NoPrice,

    /// <summary>Il sito ha rifiutato la richiesta o non risponde (403, 429, errori del server).</summary>
    Blocked,
}

public sealed record DerivativeLookup(DerivativeLookupStatus Status, DerivativeQuote? Quote = null, int HttpStatus = 0);

/// <summary>Un sito di emittente da cui si legge il prezzo dei derivati (uno per emittente: si interroga un prodotto alla volta).</summary>
public interface IDerivativeProvider : IDisposable
{
    /// <summary>Come si chiama la fonte nello storico delle quotazioni.</summary>
    string Source { get; }

    /// <summary>Il nome dell'emittente, per i messaggi del registro.</summary>
    string IssuerName { get; }

    Task<DerivativeLookup> GetQuoteAsync(string isin, CancellationToken cancellationToken);
}

/// <summary>Le richieste ai siti degli emittenti partono tutte allo stesso modo: educate, dichiarandosi «Pelago».</summary>
internal static class PelagoHttp
{
    public static HttpClient Create(HttpMessageHandler? handler, string acceptLanguage)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(25);
        var version = typeof(PelagoHttp).Assembly.GetName().Version;
        var label = version is null ? "Pelago" : $"Pelago/{version.Major}.{version.Minor}";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{label} (applicazione personale per il proprio portafoglio)");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return http;
    }
}
