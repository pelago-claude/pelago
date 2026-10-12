using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DPOper.Services;

public enum GovKind
{
    /// <summary>Stato dell'area euro.</summary>
    Euro,

    /// <summary>Altro Stato dell'Unione Europea, con valuta propria.</summary>
    OtherEu,

    /// <summary>Stati Uniti.</summary>
    Usa,

    /// <summary>L'area euro nel suo insieme (curva dei titoli AAA della BCE).</summary>
    EuroAggregate,
}

public sealed record GovCountry(string Code, string Name, string Currency, GovKind Kind)
{
    /// <summary>Le prime due lettere dell'ISIN dei titoli di quel Stato.</summary>
    public string IsinPrefix => Kind == GovKind.EuroAggregate ? "" : Code;

    /// <summary>Il file della bandiera (wwwroot\img\flags): quella dell'Unione Europea per l'area euro.</summary>
    public string Flag => Kind == GovKind.EuroAggregate ? "eu" : Code.ToLowerInvariant();
}

public sealed record YieldPoint(DateOnly Date, decimal Value);

public sealed record CurvePoint(decimal Years, decimal Yield);

public sealed class YieldCurve
{
    public string Source { get; set; } = "";
    public DateOnly Date { get; set; }
    public List<CurvePoint> Points { get; set; } = new();
}

/// <summary>Una emissione di titoli del Tesoro USA (da TreasuryDirect).</summary>
public sealed record TreasuryAuction(string Type, string Term, DateOnly Issue, DateOnly Maturity, decimal? Coupon, decimal? Yield, decimal? Price, decimal? BidToCover);

/// <summary>Tutto quello che si sa dei titoli di Stato di un paese, con le fonti usate.</summary>
public sealed class GovCountryData
{
    public string Code { get; set; } = "";
    public DateTime FetchedUtc { get; set; }

    /// <summary>Rendimento a 10 anni, un dato al mese (BCE), con circa 5-6 settimane di ritardo.</summary>
    public List<YieldPoint> Monthly { get; set; } = new();

    /// <summary>Rendimento a 10 anni, un dato al giorno (Germania: Bundesbank; USA: Tesoro; area euro: BCE).</summary>
    public List<YieldPoint> Daily { get; set; } = new();

    public string? DailySource { get; set; }
    public YieldCurve? Curve { get; set; }
    public YieldCurve? CurveMonthAgo { get; set; }
    public YieldCurve? CurveYearAgo { get; set; }
    public List<TreasuryAuction> Auctions { get; set; } = new();
}

/// <summary>
/// Elenco dei paesi e scaricamento dei rendimenti da fonti ufficiali gratuite:
/// <list type="bullet">
/// <item><b>BCE</b> (statistiche sui tassi a lungo termine): rendimento a 10 anni mensile di tutti gli Stati UE; e la curva giornaliera dei titoli AAA dell'area euro.</item>
/// <item><b>Bundesbank</b>: curva giornaliera dei Bund (rendimenti a 1-30 anni).</item>
/// <item><b>Tesoro USA</b>: curva giornaliera dei rendimenti e ultime aste (TreasuryDirect).</item>
/// </list>
/// Per i titoli singoli (ISIN, prezzo di mercato) non esiste un'analoga fonte gratuita e ufficiale fuori dall'Italia: per i BTP si usa Borsa Italiana.
/// </summary>
public static class GovBonds
{
    public const string EcbSource = "BCE";
    public const string BundesbankSource = "Bundesbank";
    public const string TreasurySource = "Tesoro USA";

    public static readonly IReadOnlyList<GovCountry> Countries = new GovCountry[]
    {
        new("IT", "Italia", "EUR", GovKind.Euro), new("DE", "Germania", "EUR", GovKind.Euro), new("FR", "Francia", "EUR", GovKind.Euro),
        new("ES", "Spagna", "EUR", GovKind.Euro), new("PT", "Portogallo", "EUR", GovKind.Euro), new("GR", "Grecia", "EUR", GovKind.Euro),
        new("NL", "Paesi Bassi", "EUR", GovKind.Euro), new("BE", "Belgio", "EUR", GovKind.Euro), new("AT", "Austria", "EUR", GovKind.Euro),
        new("IE", "Irlanda", "EUR", GovKind.Euro), new("FI", "Finlandia", "EUR", GovKind.Euro), new("LU", "Lussemburgo", "EUR", GovKind.Euro),
        new("SK", "Slovacchia", "EUR", GovKind.Euro), new("SI", "Slovenia", "EUR", GovKind.Euro), new("LT", "Lituania", "EUR", GovKind.Euro),
        new("LV", "Lettonia", "EUR", GovKind.Euro), new("EE", "Estonia", "EUR", GovKind.Euro), new("CY", "Cipro", "EUR", GovKind.Euro),
        new("MT", "Malta", "EUR", GovKind.Euro), new("HR", "Croazia", "EUR", GovKind.Euro),
        new("SE", "Svezia", "SEK", GovKind.OtherEu), new("DK", "Danimarca", "DKK", GovKind.OtherEu), new("PL", "Polonia", "PLN", GovKind.OtherEu),
        new("CZ", "Repubblica Ceca", "CZK", GovKind.OtherEu), new("HU", "Ungheria", "HUF", GovKind.OtherEu), new("RO", "Romania", "RON", GovKind.OtherEu),
        new("US", "Stati Uniti", "USD", GovKind.Usa),
        new("EA", "Area euro (curva AAA)", "EUR", GovKind.EuroAggregate),
    };

    /// <summary>I paesi sempre in vista sopra l'elenco completo.</summary>
    public static readonly IReadOnlyList<string> Featured = new[] { "IT", "DE", "FR", "ES", "US", "EA" };

    public static GovCountry? Find(string? code) =>
        Countries.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, GovCountryData> Memory = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string StorePath = Path.Combine(DataLocations.Root, "govbonds.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);
    private static readonly object Gate = new();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/2.3");
        return client;
    }

    // ---- accesso ai dati (con memoria, file di riserva e scadenza di 30 minuti)

    /// <summary>I dati già scaricati per un paese (anche vecchi), per mostrare subito qualcosa. Null se non ce ne sono.</summary>
    public static GovCountryData? Cached(string code)
    {
        lock (Gate)
        {
            if (Memory.TryGetValue(code, out var data))
            {
                return data;
            }

            LoadStoreLocked();
            return Memory.GetValueOrDefault(code);
        }
    }

    public static bool IsFresh(GovCountryData data) => DateTime.UtcNow - data.FetchedUtc < MaxAge;

    /// <summary>Scarica (o riusa se recenti) i dati di un paese. In caso di errore di rete solleva l'eccezione.</summary>
    public static async Task<GovCountryData> GetAsync(string code, CancellationToken cancellationToken, bool force = false)
    {
        var country = Find(code) ?? throw new ArgumentException(Loc.T("Paese sconosciuto: {0}", code));
        if (!force && Cached(country.Code) is { } cached && IsFresh(cached))
        {
            return cached;
        }

        var data = new GovCountryData { Code = country.Code, FetchedUtc = DateTime.UtcNow };
        switch (country.Kind)
        {
            case GovKind.Euro or GovKind.OtherEu:
                data.Monthly = await FetchEcbMonthlyAsync(country, cancellationToken);
                if (country.Code == "DE")
                {
                    await FillBundesbankAsync(data, cancellationToken);
                }
                break;
            case GovKind.Usa:
                await FillTreasuryAsync(data, cancellationToken);
                break;
            case GovKind.EuroAggregate:
                await FillEuroAaaAsync(data, cancellationToken);
                break;
        }

        lock (Gate)
        {
            Memory[country.Code] = data;
            SaveStoreLocked();
        }

        return data;
    }

    private static void LoadStoreLocked()
    {
        try
        {
            if (File.Exists(StorePath) && JsonSerializer.Deserialize<Dictionary<string, GovCountryData>>(File.ReadAllText(StorePath), JsonOptions) is { } stored)
            {
                foreach (var (key, value) in stored)
                {
                    Memory.TryAdd(key, value);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // file di riserva rovinato: si riscarica
        }
    }

    private static void SaveStoreLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(Memory, JsonOptions));
        }
        catch (IOException)
        {
            // niente di grave: i dati restano in memoria
        }
    }

    // ---- BCE: rendimento a 10 anni mensile (tassi a lungo termine, criterio di convergenza)

    private static async Task<List<YieldPoint>> FetchEcbMonthlyAsync(GovCountry country, CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.Year - 11;
        var url = $"https://data-api.ecb.europa.eu/service/data/IRS/M.{country.Code}.L.L40.CI.0000.{country.Currency}.N.Z?startPeriod={from}-01&format=csvdata";
        return ParseEcbMonthly(await Http.GetStringAsync(url, cancellationToken));
    }

    internal static List<YieldPoint> ParseEcbMonthly(string csv) =>
        ParseKeyedCsv(csv, ',', "TIME_PERIOD", "OBS_VALUE", keyColumn: null)
            .Select(r => new YieldPoint(ParseMonth(r.Period), r.Value))
            .Where(p => p.Date != default)
            .OrderBy(p => p.Date)
            .ToList();

    private static DateOnly ParseMonth(string period) =>
        DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : default;

    // ---- BCE: curva dei titoli AAA dell'area euro (giornaliera)

    private static readonly int[] CurveYears = { 1, 2, 3, 5, 7, 10, 15, 20, 30 };

    private static async Task FillEuroAaaAsync(GovCountryData data, CancellationToken cancellationToken)
    {
        var keys = string.Join('+', CurveYears.Select(y => $"SR_{y}Y"));
        var recent = await Http.GetStringAsync(
            $"https://data-api.ecb.europa.eu/service/data/YC/B.U2.EUR.4F.G_N_A.SV_C_YM.{keys}?lastNObservations=270&format=csvdata", cancellationToken);
        var table = ParseKeyedCsv(recent, ',', "TIME_PERIOD", "OBS_VALUE", keyColumn: "DATA_TYPE_FM");
        FillCurves(data, EcbSource, table.Select(r => (r.Key, r.Period, r.Value)), key => int.TryParse(key.AsSpan(3, key.Length - 4), out var y) ? y : null);

        var longHistory = await Http.GetStringAsync(
            "https://data-api.ecb.europa.eu/service/data/YC/B.U2.EUR.4F.G_N_A.SV_C_YM.SR_10Y?lastNObservations=1500&format=csvdata", cancellationToken);
        data.Daily = ParseKeyedCsv(longHistory, ',', "TIME_PERIOD", "OBS_VALUE", keyColumn: null)
            .Select(r => new YieldPoint(DateOnly.ParseExact(r.Period, "yyyy-MM-dd", CultureInfo.InvariantCulture), r.Value)).OrderBy(p => p.Date).ToList();
        data.DailySource = EcbSource;
    }

    // ---- Bundesbank: curva dei Bund (rendimenti par con cedola annuale, giornaliera)

    private static async Task FillBundesbankAsync(GovCountryData data, CancellationToken cancellationToken)
    {
        var keys = string.Join('+', CurveYears.Select(y => $"R{y:00}XX"));
        var recent = await Http.GetStringAsync(
            $"https://api.statistiken.bundesbank.de/rest/data/BBSIS/D.I.ZAR.ZI.EUR.S1311.B.A604.{keys}.R.A.A._Z._Z.A?lastNObservations=270&format=csv&lang=en", cancellationToken);
        var table = ParseBundesbank(recent);
        FillCurves(data, BundesbankSource, table, key => int.TryParse(key, out var y) ? y : null);

        var longHistory = await Http.GetStringAsync(
            "https://api.statistiken.bundesbank.de/rest/data/BBSIS/D.I.ZAR.ZI.EUR.S1311.B.A604.R10XX.R.A.A._Z._Z.A?lastNObservations=1500&format=csv&lang=en", cancellationToken);
        data.Daily = ParseBundesbank(longHistory).Select(r => new YieldPoint(DateOnly.ParseExact(r.Period, "yyyy-MM-dd", CultureInfo.InvariantCulture), r.Value))
            .OrderBy(p => p.Date).ToList();
        data.DailySource = BundesbankSource;
    }

    /// <summary>
    /// Legge il CSV della Bundesbank: una riga di intestazione con i codici delle serie (…R10XX…), alcune righe di note e poi
    /// «data,valore,flag,valore,flag…». Restituisce righe (scadenza in anni, data, valore).
    /// </summary>
    internal static List<(string Key, string Period, decimal Value)> ParseBundesbank(string csv)
    {
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count == 0)
        {
            return new();
        }

        // posizione di ogni serie nella riga: l'intestazione ha «"",serie1,serie1_FLAGS,serie2,…»
        var header = SplitCsvLine(lines[0], ',');
        var columns = new List<(int Index, string Years)>();
        for (var i = 1; i < header.Count; i++)
        {
            var m = Regex.Match(header[i], @"\.R(\d\d)XX\.");
            if (m.Success && !header[i].EndsWith("_FLAGS", StringComparison.Ordinal))
            {
                columns.Add((i, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
            }
        }

        var result = new List<(string, string, decimal)>();
        foreach (var line in lines.Skip(1))
        {
            if (line.Length < 11 || !Regex.IsMatch(line, @"^\d{4}-\d{2}-\d{2},"))
            {
                continue;
            }

            var cells = SplitCsvLine(line, ',');
            foreach (var (index, years) in columns)
            {
                if (index < cells.Count && decimal.TryParse(cells[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    result.Add((years, cells[0], value));
                }
            }
        }

        return result;
    }

    // ---- Tesoro USA: curva giornaliera (par yield) e ultime aste

    private static async Task FillTreasuryAsync(GovCountryData data, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var all = new List<(DateOnly Date, Dictionary<string, decimal> Values)>();
        for (var y = year; y >= year - 5; y--)
        {
            try
            {
                var csv = await Http.GetStringAsync(
                    $"https://home.treasury.gov/resource-center/data-chart-center/interest-rates/daily-treasury-rates.csv/{y}/all?type=daily_treasury_yield_curve&field_tdr_date_value={y}&page&_format=csv", cancellationToken);
                all.AddRange(ParseTreasuryCsv(csv));
            }
            catch (HttpRequestException) when (y < year)
            {
                break; // gli anni più vecchi sono facoltativi
            }
        }

        if (all.Count == 0)
        {
            throw new InvalidOperationException(Loc.T("Il Tesoro USA non ha restituito la curva dei rendimenti."));
        }

        all = all.OrderBy(r => r.Date).ToList();
        data.Daily = all.Where(r => r.Values.ContainsKey("10 Yr")).Select(r => new YieldPoint(r.Date, r.Values["10 Yr"])).ToList();
        data.DailySource = TreasurySource;
        FillCurves(data, TreasurySource, all.SelectMany(r => r.Values.Select(v => (v.Key, r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), v.Value))),
            key => TreasuryYears(key));

        try
        {
            var json = await Http.GetStringAsync("https://www.treasurydirect.gov/TA_WS/securities/auctioned?format=json&days=120", cancellationToken);
            data.Auctions = ParseAuctions(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // le aste sono un di più: la curva basta
        }
    }

    internal static decimal? TreasuryYears(string column) => column switch
    {
        "1 Mo" => 1m / 12m, "1.5 Month" => 1.5m / 12m, "2 Mo" => 2m / 12m, "3 Mo" => 0.25m, "4 Mo" => 4m / 12m, "6 Mo" => 0.5m,
        "1 Yr" => 1m, "2 Yr" => 2m, "3 Yr" => 3m, "5 Yr" => 5m, "7 Yr" => 7m, "10 Yr" => 10m, "20 Yr" => 20m, "30 Yr" => 30m,
        _ => null,
    };

    internal static List<(DateOnly Date, Dictionary<string, decimal> Values)> ParseTreasuryCsv(string csv)
    {
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        var result = new List<(DateOnly, Dictionary<string, decimal>)>();
        if (lines.Count < 2)
        {
            return result;
        }

        var header = SplitCsvLine(lines[0], ',');
        foreach (var line in lines.Skip(1))
        {
            var cells = SplitCsvLine(line, ',');
            if (cells.Count == 0 || !DateOnly.TryParseExact(cells[0], "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            var values = new Dictionary<string, decimal>();
            for (var i = 1; i < header.Count && i < cells.Count; i++)
            {
                if (decimal.TryParse(cells[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    values[header[i]] = v;
                }
            }
            result.Add((date, values));
        }

        return result;
    }

    internal static List<TreasuryAuction> ParseAuctions(string json)
    {
        var list = new List<TreasuryAuction>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        string Str(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
        decimal? Num(JsonElement e, string name) =>
            decimal.TryParse(Str(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        DateOnly? Day(JsonElement e, string name) =>
            Str(e, name) is { Length: >= 10 } s && DateOnly.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var type = Str(e, "securityType");
            if (type is not ("Note" or "Bond" or "TIPS" or "FRN") || Day(e, "issueDate") is not { } issue || Day(e, "maturityDate") is not { } maturity)
            {
                continue;
            }

            list.Add(new TreasuryAuction(type, Str(e, "securityTerm"), issue, maturity, Num(e, "interestRate"), Num(e, "highYield"), Num(e, "pricePer100"), Num(e, "bidToCoverRatio")));
        }

        return list.OrderByDescending(a => a.Issue).ThenBy(a => a.Maturity).ToList();
    }

    // ---- curve: ultimo giorno, un mese fa, un anno fa

    private static void FillCurves(GovCountryData data, string source, IEnumerable<(string Key, string Period, decimal Value)> rows, Func<string, decimal?> yearsOf)
    {
        var byDate = new SortedDictionary<DateOnly, List<CurvePoint>>();
        foreach (var (key, period, value) in rows)
        {
            if (yearsOf(key) is not { } years ||
                !DateOnly.TryParseExact(period, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!byDate.TryGetValue(date, out var list))
            {
                byDate[date] = list = new();
            }
            list.Add(new CurvePoint(years, value));
        }

        if (byDate.Count == 0)
        {
            return;
        }

        var last = byDate.Keys.Max();
        data.Curve = BuildCurve(byDate, last, source);
        data.CurveMonthAgo = BuildCurve(byDate, last.AddMonths(-1), source);
        data.CurveYearAgo = BuildCurve(byDate, last.AddYears(-1), source);
    }

    /// <summary>La curva del giorno indicato o, se manca (festivi), dell'ultimo giorno disponibile prima. Null se non ce n'è.</summary>
    internal static YieldCurve? BuildCurve(SortedDictionary<DateOnly, List<CurvePoint>> byDate, DateOnly target, string source)
    {
        var day = byDate.Keys.Where(d => d <= target).DefaultIfEmpty().Max();
        if (day == default)
        {
            return null;
        }

        return new YieldCurve { Source = source, Date = day, Points = byDate[day].OrderBy(p => p.Years).ToList() };
    }

    // ---- confronti

    /// <summary>Differenza in punti base tra due serie mensili, sui mesi presenti in entrambe (rendimento del paese meno quello di riferimento).</summary>
    public static List<YieldPoint> SpreadBasisPoints(IEnumerable<YieldPoint> country, IEnumerable<YieldPoint> reference)
    {
        var refByDate = reference.ToDictionary(p => p.Date, p => p.Value);
        return country.Where(p => refByDate.ContainsKey(p.Date)).Select(p => new YieldPoint(p.Date, (p.Value - refByDate[p.Date]) * 100m)).ToList();
    }

    // ---- lettura dei CSV

    /// <summary>Righe (chiave, periodo, valore) di un CSV con intestazione e colonne indicate per nome (SDMX della BCE).</summary>
    internal static List<(string Key, string Period, decimal Value)> ParseKeyedCsv(string csv, char separator, string periodColumn, string valueColumn, string? keyColumn)
    {
        var result = new List<(string, string, decimal)>();
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count < 2)
        {
            return result;
        }

        var header = SplitCsvLine(lines[0], separator);
        var period = header.IndexOf(periodColumn);
        var value = header.IndexOf(valueColumn);
        var key = keyColumn is null ? -1 : header.IndexOf(keyColumn);
        if (period < 0 || value < 0)
        {
            return result;
        }

        foreach (var line in lines.Skip(1))
        {
            var cells = SplitCsvLine(line, separator);
            if (cells.Count <= Math.Max(period, value) ||
                !decimal.TryParse(cells[value], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                continue;
            }

            result.Add((key >= 0 && key < cells.Count ? cells[key] : "", cells[period], v));
        }

        return result;
    }

    /// <summary>Divide una riga CSV rispettando i campi tra virgolette (le virgole dentro le virgolette non separano).</summary>
    internal static List<string> SplitCsvLine(string line, char separator)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == separator)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
