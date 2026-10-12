using DPOper.Models;
using static DPOper.Services.Loc;

namespace DPOper.Services;

/// <param name="Blocked">Il sito dell'emittente ha rifiutato la richiesta (403, 429, errori del server): dopo due di fila si smette per questa volta.</param>
/// <param name="ProductName">Il nome del prodotto secondo l'emittente (derivati), se il sito l'ha dato: l'app lo ricorda (vedi <see cref="IssuerNames"/>).</param>
public sealed record AssetUpdateResult(AssetCatalogEntry Entry, bool Success, decimal? Price, string Message, bool Blocked = false, string? ProductName = null);

/// <summary>
/// Aggiorna le quotazioni di un gruppo di asset in parallelo (con un tetto alla concorrenza per
/// restare educati verso le API), accodando ogni risultato allo storico e notificando l'avanzamento
/// via callback man mano che ogni asset finisce (non aspetta che finiscano tutti).
///
/// Instrada ogni asset al provider giusto in base alla classe: CRYPTO → CoinGecko, STOCK/FUND →
/// Yahoo Finance (con risoluzione ISIN→ticker la prima volta, poi salvata nel catalogo), FX →
/// Frankfurter, DERIVATIVE → pagina del prodotto sul sito dell'emittente (una richiesta alla volta, con una pausa: vedi <see cref="IDerivativeProvider"/>
/// e <see cref="VontobelQuoteProvider"/>).
/// Ogni quotazione azione/fondo non in euro (USD, GBP, AUD..., anche in pence) viene convertita in EUR con il
/// cambio della sua valuta, recuperato una sola volta per l'intero lotto.
/// </summary>
public static class AssetUpdateService
{
    private const int MaxConcurrency = 5;

    /// <summary>Il lettore dei prezzi per ogni emittente di derivati: null se per quell'emittente non se ne ha ancora uno.</summary>
    public static IDerivativeProvider? DefaultDerivativeProvider(Issuer issuer) => issuer switch
    {
        Issuer.Vontobel => new VontobelQuoteProvider(),
        Issuer.SocieteGenerale => new SocieteGeneraleQuoteProvider(),
        _ => null,
    };

    /// <param name="derivativeProviders">Per le prove automatiche: i lettori da usare al posto di quelli veri.</param>
    /// <param name="derivativePauseMilliseconds">Per le prove automatiche: la pausa tra due richieste allo stesso sito.</param>
    public static async Task RunAsync(
        List<AssetCatalogEntry> entries,
        Action<AssetUpdateResult> onAssetCompleted,
        CancellationToken cancellationToken,
        Func<Issuer, IDerivativeProvider?>? derivativeProviders = null,
        int derivativePauseMilliseconds = DerivativePauseMilliseconds)
    {
        using var coinGecko = new CoinGeckoQuoteProvider();
        using var yahoo = new YahooFinanceQuoteProvider();
        using var fx = new ExchangeRateProvider();
        using var throttle = new SemaphoreSlim(MaxConcurrency);

        // Un cambio verso l'euro per ogni valuta incontrata (dollari, sterline, franchi, dollari australiani...),
        // chiesto una sola volta per tutto il lotto.
        var rates = new RateCache(fx);

        var tasks = entries.Where(e => e.AssetClass != "DERIVATIVE").Select(async entry =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                var result = await UpdateOneAsync(entry, coinGecko, yahoo, fx, rates, cancellationToken);
                onAssetCompleted(result);
            }
            finally
            {
                throttle.Release();
            }
        }).ToList();

        // i derivati vanno a parte: per ogni emittente una richiesta alla volta, con una pausa, per essere educati con il suo sito
        var factory = derivativeProviders ?? DefaultDerivativeProvider;
        foreach (var group in entries.Where(e => e.AssetClass == "DERIVATIVE").GroupBy(e => Issuers.OfIsin(e.Symbol)))
        {
            var provider = group.Key is { } issuer ? factory(issuer) : null;
            tasks.Add(RunDerivativesAsync(group.ToList(), provider, onAssetCompleted, derivativePauseMilliseconds, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>Pausa tra due richieste al sito di un emittente.</summary>
    public const int DerivativePauseMilliseconds = 1200;

    private static async Task RunDerivativesAsync(List<AssetCatalogEntry> entries, IDerivativeProvider? provider,
        Action<AssetUpdateResult> onAssetCompleted, int pauseMilliseconds, CancellationToken cancellationToken)
    {
        using var owned = provider;
        var blockedInARow = 0;
        var stopped = false;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var result = provider is null
                ? Fail(entry, T("Emittente non supportato: non so dove leggere il prezzo di questo prodotto."))
                : stopped
                    ? Fail(entry, T("Interrotto: {0} non ha risposto come previsto. Riprova più tardi.", provider.IssuerName))
                    : await UpdateDerivativeAsync(entry, provider, cancellationToken);

            if (!stopped)
            {
                blockedInARow = result.Blocked ? blockedInARow + 1 : 0;
                stopped = blockedInARow >= 2;               // due risposte anomale di fila: per questa volta si smette
            }
            onAssetCompleted(result);

            if (provider is not null && i < entries.Count - 1 && !stopped)
            {
                await Task.Delay(pauseMilliseconds, cancellationToken);
            }
        }
    }

    private static async Task<AssetUpdateResult> UpdateDerivativeAsync(AssetCatalogEntry entry, IDerivativeProvider provider, CancellationToken cancellationToken)
    {
        var issuer = provider.IssuerName;
        try
        {
            var lookup = await provider.GetQuoteAsync(entry.Symbol, cancellationToken);
            switch (lookup.Status)
            {
                case DerivativeLookupStatus.NotFound:
                    return Fail(entry, T("Prodotto non trovato sul sito di {0} (scaduto o non più in elenco).", issuer));
                case DerivativeLookupStatus.NoPrice:
                    return Fail(entry, T("Il sito di {0} non mostra un prezzo per questo prodotto (scaduto o con knock-out).", issuer));
                case DerivativeLookupStatus.Blocked:
                    return new AssetUpdateResult(entry, false, null, T("{0} non ha risposto come previsto (errore {1}).", issuer, lookup.HttpStatus), Blocked: true);
            }

            var quote = lookup.Quote!;
            if (!CurrencyConverter.IsEur(quote.Currency))
            {
                return Fail(entry, T("prezzo in {0}: non salvato (si accettano solo euro).", quote.Currency));
            }

            QuoteHistoryStore.Append(entry, new QuoteEntry
            {
                Timestamp = quote.StampUtc ?? DateTime.UtcNow,
                Price = quote.Price,
                Currency = "EUR",
                Source = provider.Source,
            });

            var note = quote is { Bid: { } bid, Ask: { } ask } ? T("Geld {0:0.####} / Brief {1:0.####}", bid, ask) : "OK";
            return new AssetUpdateResult(entry, true, quote.Price, note, ProductName: quote.ProductName);
        }
        catch (HttpRequestException ex)
        {
            return new AssetUpdateResult(entry, false, null, T("{0} non raggiungibile: {1}", issuer, ex.Message), Blocked: true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AssetUpdateResult(entry, false, null, T("{0} non ha risposto in tempo.", issuer), Blocked: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fail(entry, ex.Message);
        }
    }

    private static async Task<AssetUpdateResult> UpdateOneAsync(
        AssetCatalogEntry entry, CoinGeckoQuoteProvider coinGecko, YahooFinanceQuoteProvider yahoo,
        ExchangeRateProvider fx, RateCache rates, CancellationToken cancellationToken)
    {
        try
        {
            decimal price;
            string currency;
            string source;
            decimal? originalPrice = null;
            string? originalCurrency = null;
            string? foundNow = null;

            switch (entry.AssetClass)
            {
                case "CRYPTO":
                    var coinGeckoId = entry.ProviderMappings.TryGetValue("coingecko", out var knownId) && !string.IsNullOrWhiteSpace(knownId) ? knownId : null;
                    if (coinGeckoId is null)
                    {
                        // come per i titoli su Yahoo: la prima volta si cerca l'id sul sito, e si salva nel catalogo
                        coinGeckoId = await coinGecko.FindIdAsync(entry.Symbol, cancellationToken);
                        if (coinGeckoId is null)
                        {
                            return Fail(entry, T("Nessun id CoinGecko per «{0}»: la ricerca non ha trovato una crypto con questo simbolo. Puoi scriverlo a mano in catalog.json.", entry.Symbol));
                        }

                        AssetCatalogStore.UpdateMapping(entry.Key, "coingecko", coinGeckoId);
                        entry.ProviderMappings["coingecko"] = coinGeckoId;
                        foundNow = coinGeckoId;
                    }
                    price = await coinGecko.GetPriceEurAsync(coinGeckoId, cancellationToken);
                    currency = "EUR";
                    source = "coingecko";
                    break;

                case "STOCK":
                case "FUND":
                    var ticker = await EnsureYahooTickerAsync(entry, yahoo, cancellationToken);
                    if (ticker is null)
                    {
                        return Fail(entry, T("ISIN non risolto su Yahoo Finance (titolo non quotato/ritirato dal mercato o non coperto)."));
                    }

                    var (rawPrice, rawCurrency) = await yahoo.GetQuoteAsync(ticker, cancellationToken);
                    var (unitPrice, unitCurrency) = CurrencyConverter.NormalizeMinorUnit(rawPrice, rawCurrency);
                    if (CurrencyConverter.IsEur(unitCurrency))
                    {
                        price = unitPrice;
                    }
                    else
                    {
                        decimal rate;
                        try
                        {
                            rate = await rates.ToEurAsync(unitCurrency, cancellationToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Meglio nessun prezzo che un prezzo in una valuta sbagliata trattato come euro.
                            return Fail(entry, T("prezzo in {0}, cambio {1}/EUR non disponibile ({2}): non salvato.", rawCurrency, unitCurrency, ex.Message));
                        }

                        price = unitPrice * rate;
                        originalPrice = rawPrice;
                        originalCurrency = rawCurrency;
                    }
                    currency = "EUR";
                    source = "yahoo";
                    break;

                case "FX":
                    if (!entry.ProviderMappings.TryGetValue("frankfurter", out var fromCurrency) || string.IsNullOrWhiteSpace(fromCurrency))
                    {
                        return Fail(entry, T("Nessun mapping valuta configurato per questo cambio."));
                    }
                    price = await fx.GetRateAsync(fromCurrency, "EUR", cancellationToken);
                    currency = "EUR";
                    source = "frankfurter";
                    break;

                default:
                    return Fail(entry, T("Categoria \"{0}\" non supportata per l'aggiornamento automatico.", entry.AssetClass));
            }

            var quote = new QuoteEntry
            {
                Timestamp = DateTime.UtcNow,
                Price = price,
                Currency = currency,
                Source = source,
                OriginalPrice = originalPrice,
                OriginalCurrency = originalCurrency,
            };
            QuoteHistoryStore.Append(entry, quote);

            var message = foundNow is not null ? T("OK (associata a «{0}» su CoinGecko: se non è quella giusta, correggila in catalog.json)", foundNow)
                : originalCurrency is null ? "OK" : T("OK (convertito da {0:0.####} {1})", originalPrice, originalCurrency);
            return new AssetUpdateResult(entry, true, price, message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Fail(entry, ex.Message);
        }
    }

    private static async Task<string?> EnsureYahooTickerAsync(
        AssetCatalogEntry entry, YahooFinanceQuoteProvider yahoo, CancellationToken cancellationToken)
    {
        if (entry.ProviderMappings.TryGetValue("yahoo", out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        var ticker = await yahoo.ResolveTickerAsync(entry.Symbol, cancellationToken);
        if (ticker is not null)
        {
            AssetCatalogStore.UpdateMapping(entry.Key, "yahoo", ticker);
            entry.ProviderMappings["yahoo"] = ticker;
        }

        return ticker;
    }

    private static AssetUpdateResult Fail(AssetCatalogEntry entry, string message) => new(entry, false, null, message);
}
