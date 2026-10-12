using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DPOper.Models;

namespace DPOper.Services;

public sealed record FxCurrencyInfo(string Code, string Name);

/// <summary>Un cambio contro l'euro: quante unità della valuta valgono 1 €, con le variazioni e il piccolo grafico degli ultimi 30 giorni.</summary>
public sealed class FxRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Unità della valuta per 1 euro (esempio: 1,12 per il dollaro).</summary>
    public decimal Rate { get; set; }

    /// <summary>Variazione rispetto alla chiusura precedente, come frazione (0,002 = +0,2%).</summary>
    public decimal? Change1d { get; set; }

    /// <summary>Variazione rispetto a 30 giorni fa, come frazione.</summary>
    public decimal? Change30d { get; set; }

    public DateTime AsOfUtc { get; set; }

    /// <summary>Da dove arriva il valore: <see cref="FxBoard.LiveSource"/> o <see cref="FxBoard.EcbSource"/>.</summary>
    public string Source { get; set; } = "";

    public List<decimal> Spark { get; set; } = new();

    [JsonIgnore] public bool IsLive => Source == FxBoard.LiveSource;
    [JsonIgnore] public decimal Inverse => Rate == 0m ? 0m : 1m / Rate;
}

public sealed class FxSnapshot
{
    public DateTime FetchedUtc { get; set; }
    public List<FxRow> Rows { get; set; } = new();
}

/// <summary>
/// Cambi principali contro l'euro. Fonti, dalla più alla meno aggiornata:
/// <list type="number">
/// <item><b>Yahoo Finance</b> (non ufficiale, senza chiave): il valore di mercato di adesso, in ritardo di pochi secondi.</item>
/// <item><b>BCE via Frankfurter</b> (ufficiale, senza chiave): il cambio di riferimento delle 14:15 UTC, una volta al giorno nei giorni lavorativi.
/// Fa da ripiego se Yahoo non risponde (o dà un valore assurdo) e fornisce lo storico per i grafici.</item>
/// </list>
/// Il sito OANDA non è usato: non ha un'API gratuita e i suoi termini vietano di leggere i cambi con programmi automatici.
/// </summary>
public static class FxBoard
{
    public const string LiveSource = "Yahoo Finance";
    public const string EcbSource = "BCE";

    /// <summary>Se Yahoo si scosta dalla BCE di più di così, si diffida e si usa la BCE.</summary>
    internal const decimal MaxLiveDeviation = 0.10m;

    public static readonly IReadOnlyList<FxCurrencyInfo> Currencies = new FxCurrencyInfo[]
    {
        new("USD", "Dollaro statunitense"), new("GBP", "Sterlina britannica"), new("CHF", "Franco svizzero"), new("JPY", "Yen giapponese"),
        new("CAD", "Dollaro canadese"), new("AUD", "Dollaro australiano"), new("NZD", "Dollaro neozelandese"), new("CNY", "Yuan cinese"),
        new("HKD", "Dollaro di Hong Kong"), new("SGD", "Dollaro di Singapore"), new("SEK", "Corona svedese"), new("NOK", "Corona norvegese"),
        new("DKK", "Corona danese"), new("PLN", "Zloty polacco"), new("CZK", "Corona ceca"), new("HUF", "Fiorino ungherese"),
        new("TRY", "Lira turca"), new("BRL", "Real brasiliano"), new("INR", "Rupia indiana"), new("MXN", "Peso messicano"), new("ZAR", "Rand sudafricano"),
    };

    /// <summary>La bandiera (codice del file in wwwroot\img\flags) del Paese di una valuta; l'euro ha quella dell'Unione Europea.</summary>
    public static string? FlagOf(string currency) => currency.ToUpperInvariant() switch
    {
        "EUR" => "eu", "USD" => "us", "GBP" => "gb", "CHF" => "ch", "JPY" => "jp", "CAD" => "ca", "AUD" => "au", "NZD" => "nz",
        "CNY" => "cn", "HKD" => "hk", "SGD" => "sg", "SEK" => "se", "NOK" => "no", "DKK" => "dk", "PLN" => "pl", "CZK" => "cz",
        "HUF" => "hu", "TRY" => "tr", "BRL" => "br", "INR" => "in", "MXN" => "mx", "ZAR" => "za",
        _ => null,
    };

    private static readonly HttpClient Http = CreateClient();
    private static readonly string StorePath = Path.Combine(DataLocations.Root, "fx.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/2.3");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    // ---- salvataggio dell'ultimo rilevamento (si vede subito anche senza rete)

    public static FxSnapshot? LoadSaved()
    {
        try
        {
            return File.Exists(StorePath) ? JsonSerializer.Deserialize<FxSnapshot>(File.ReadAllText(StorePath), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public static void Save(FxSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    // ---- scaricamento

    public static async Task<FxSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var symbols = string.Join(',', Currencies.Select(c => c.Code));

        Dictionary<string, List<(DateOnly Date, decimal Rate)>> ecb;
        try
        {
            var json = await Http.GetStringAsync($"https://api.frankfurter.dev/v1/{today.AddDays(-42):yyyy-MM-dd}..?base=EUR&symbols={symbols}", cancellationToken);
            ecb = ParseFrankfurterSeries(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            ecb = new();
        }

        using var gate = new SemaphoreSlim(6);
        var live = await Task.WhenAll(Currencies.Select(async c =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var json = await Http.GetStringAsync($"https://query1.finance.yahoo.com/v8/finance/chart/EUR{c.Code}=X?range=1d&interval=1d", cancellationToken);
                return (c.Code, Quote: ParseYahooMeta(json));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
            {
                return (c.Code, Quote: (YahooQuote?)null);
            }
            finally
            {
                gate.Release();
            }
        }));
        var liveByCode = live.ToDictionary(x => x.Code, x => x.Quote);

        var rows = new List<FxRow>();
        foreach (var currency in Currencies)
        {
            ecb.TryGetValue(currency.Code, out var series);
            var row = BuildRow(currency, series ?? new(), liveByCode.GetValueOrDefault(currency.Code), today);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(Loc.T("Nessuna fonte di cambi raggiungibile (Yahoo Finance e BCE)."));
        }

        return new FxSnapshot { FetchedUtc = DateTime.UtcNow, Rows = rows };
    }

    /// <summary>Mette insieme il cambio «live», la serie BCE e le variazioni di una valuta. Null se non c'è nessun dato.</summary>
    internal static FxRow? BuildRow(FxCurrencyInfo currency, List<(DateOnly Date, decimal Rate)> ecb, YahooQuote? live, DateOnly today)
    {
        var series = ecb.OrderBy(p => p.Date).ToList();
        decimal? ecbLast = series.Count > 0 ? series[^1].Rate : null;

        // Yahoo che si scosta troppo dalla BCE è un errore di quella fonte: meglio il dato ufficiale.
        if (live is { } q && (q.Price <= 0m || (ecbLast is { } e && e > 0m && Math.Abs(q.Price / e - 1m) > MaxLiveDeviation)))
        {
            live = null;
        }

        if (live is null && ecbLast is null)
        {
            return null;
        }

        var row = new FxRow { Code = currency.Code, Name = currency.Name };
        var window = series.Where(p => p.Date >= today.AddDays(-30)).Select(p => p.Rate).ToList();

        if (live is { } l)
        {
            row.Rate = l.Price;
            row.AsOfUtc = l.Time;
            row.Source = LiveSource;
            row.Change1d = l.PreviousClose is > 0m ? l.Price / l.PreviousClose.Value - 1m : null;
            window.Add(l.Price);
        }
        else
        {
            row.Rate = ecbLast!.Value;
            row.AsOfUtc = series[^1].Date.ToDateTime(new TimeOnly(14, 15), DateTimeKind.Utc);
            row.Source = EcbSource;
            row.Change1d = series.Count > 1 && series[^2].Rate > 0m ? series[^1].Rate / series[^2].Rate - 1m : null;
        }

        // Riferimento a 30 giorni fa: l'ultimo cambio BCE di quel giorno (o il più vecchio che abbiamo).
        var baseline = series.LastOrDefault(p => p.Date <= today.AddDays(-30));
        var baselineRate = baseline.Rate > 0m ? baseline.Rate : series.FirstOrDefault().Rate;
        row.Change30d = baselineRate > 0m ? row.Rate / baselineRate - 1m : null;
        row.Spark = window;
        return row;
    }

    /// <summary>Storico giornaliero BCE di una valuta contro l'euro, per il grafico.</summary>
    public static async Task<List<(DateOnly Date, decimal Rate)>> HistoryAsync(string code, DateOnly from, CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync($"https://api.frankfurter.dev/v1/{from:yyyy-MM-dd}..?base=EUR&symbols={Uri.EscapeDataString(code)}", cancellationToken);
        return ParseFrankfurterSeries(json).GetValueOrDefault(code) ?? new();
    }

    // ---- conversioni

    /// <summary>Converte un importo passando dall'euro (come fanno i cambi di riferimento). Null se manca il cambio di una delle due valute.</summary>
    public static decimal? Convert(decimal amount, string from, string to, FxSnapshot snapshot)
    {
        decimal? PerEuro(string code) => code.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? 1m
            : snapshot.Rows.FirstOrDefault(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Rate;

        return PerEuro(from) is > 0m and var f && PerEuro(to) is { } t && t > 0m ? amount / f * t : null;
    }

    // ---- lettura delle risposte

    internal sealed record YahooQuote(decimal Price, decimal? PreviousClose, DateTime Time);

    internal static YahooQuote? ParseYahooMeta(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("chart", out var chart) || !chart.TryGetProperty("result", out var result)
            || result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0
            || !result[0].TryGetProperty("meta", out var meta) || !meta.TryGetProperty("regularMarketPrice", out var price)
            || price.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        decimal? previous = meta.TryGetProperty("chartPreviousClose", out var prev) && prev.ValueKind == JsonValueKind.Number ? prev.GetDecimal() : null;
        var time = meta.TryGetProperty("regularMarketTime", out var t) && t.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()).UtcDateTime
            : DateTime.UtcNow;
        return new YahooQuote(price.GetDecimal(), previous, time);
    }

    internal static Dictionary<string, List<(DateOnly Date, decimal Rate)>> ParseFrankfurterSeries(string json)
    {
        var result = new Dictionary<string, List<(DateOnly, decimal)>>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("rates", out var rates))
        {
            return result;
        }

        foreach (var day in rates.EnumerateObject())
        {
            if (!DateOnly.TryParseExact(day.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            foreach (var rate in day.Value.EnumerateObject())
            {
                if (rate.Value.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                if (!result.TryGetValue(rate.Name, out var list))
                {
                    result[rate.Name] = list = new();
                }
                list.Add((date, rate.Value.GetDecimal()));
            }
        }

        return result;
    }
}

public sealed record FxExposureRow(string Currency, decimal Value, decimal Share);

/// <summary>Quanta parte del conto è quotata in ciascuna valuta (è la valuta di quotazione del titolo, non quella dei suoi sottostanti).</summary>
public static class FxExposure
{
    public static List<FxExposureRow> Compute(IEnumerable<(decimal Value, string? Currency)> positions)
    {
        var list = positions.Where(p => p.Value > 0m).ToList();
        var total = list.Sum(p => p.Value);
        if (total <= 0m)
        {
            return new();
        }

        return list
            .GroupBy(p => CurrencyConverter.IsEur(p.Currency) ? "EUR" : p.Currency!.ToUpperInvariant())
            .Select(g => new FxExposureRow(g.Key, g.Sum(p => p.Value), g.Sum(p => p.Value) / total))
            .OrderByDescending(r => r.Value)
            .ToList();
    }

    /// <summary>L'ultima valuta di quotazione di ciascun titolo: quella originale se il prezzo è stato convertito, altrimenti l'euro.</summary>
    public static Dictionary<string, string> LastQuoteCurrencies(IEnumerable<string> keys)
    {
        var wanted = keys.ToHashSet();
        var result = new Dictionary<string, string>();
        foreach (var entry in AssetCatalogStore.Load().Where(e => wanted.Contains(e.Key)))
        {
            var last = QuoteHistoryStore.Load(entry).OrderByDescending(q => q.Timestamp).FirstOrDefault();
            result[entry.Key] = last?.OriginalCurrency is { Length: > 0 } c ? c : "EUR";
        }
        return result;
    }
}
