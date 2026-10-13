using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Pelago.Models;

namespace Pelago.Services;

/// <param name="Unrecognized">Nomi di titoli in lista che non sono né BTP a tasso fisso riconosciuti né una famiglia esclusa nota:
/// se non è vuoto, Borsa Italiana potrebbe aver introdotto un nuovo formato di nome e qualche BTP andrebbe aggiunto al riconoscimento.</param>
public sealed record BtpFetchResult(List<BtpBond> Bonds, int TotalListed, int SkippedNoPrice, List<string> Unrecognized);

/// <summary>
/// Recupera online l'elenco dei BTP a tasso fisso dalla lista MOT di Borsa Italiana (la stessa fonte
/// del foglio Excel): ISIN, descrizione, ultimo prezzo, scadenza. Il tasso annuo si legge dalla
/// descrizione ("Btp Fx 4.1% Feb29"), non dalla colonna "Cedola" della lista, che per i titoli recenti
/// è la prossima cedola effettiva (con primo periodo irregolare) e non il tasso nominale/2.
/// Restano esclusi strip, BTP Italia, €i, Valore, Futura e Più: hanno cedole indicizzate, a gradino o
/// assenti, che il calcolatore non gestisce.
/// </summary>
public static partial class BtpOnlineProvider
{
    public const string SourceName = "Borsa Italiana (MOT), ultimo contratto";

    private const string PageUrl = "https://www.borsaitaliana.it/borsa/obbligazioni/mot/btp/lista.html?lang=it&page={0}";
    private const int PagesPerBatch = 4;
    private const int MaxPages = 40;

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static readonly string[] MonthAbbreviations = { "Gen", "Feb", "Mar", "Apr", "Mag", "Giu", "Lug", "Ago", "Set", "Ott", "Nov", "Dic" };

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Pelago/1.0");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it-IT,it;q=0.9");
        return client;
    }

    [GeneratedRegex(
        @"<span class=""t-text"">(IT[0-9A-Z]{10})</span>\s*</a>\s*</td>" +
        @"\s*<td[^>]*>\s*<span class=""t-text"">\s*([^<]*?)\s*</span>\s*</td>" +
        @"\s*<td[^>]*>\s*<span class=""t-text"">\s*([^<]*?)\s*</span>\s*</td>" +
        @"\s*<td[^>]*>\s*<span class=""t-text"">\s*([^<]*?)\s*</span>\s*</td>" +
        @"\s*<td[^>]*>\s*<span class=""t-text"">\s*([^<]*?)\s*</span>")]
    private static partial Regex RowRegex();

    // Le tre famiglie di nomi dei BTP a tasso fisso presenti in lista.
    [GeneratedRegex(@"^Btp-\d{1,2}[a-z]{2}\d{2}\s+(?<rate>[\d,\.]+)%$", RegexOptions.IgnoreCase)]
    private static partial Regex OldStyleRegex();

    [GeneratedRegex(@"^Btp (?<green>Green )?(?:Fx|Tf) (?<rate>[\d,\.]+)% \S+ Eur$", RegexOptions.IgnoreCase)]
    private static partial Regex FxTfRegex();

    [GeneratedRegex(@"^Btpgreen\s*(?<rate>[\d,\.]+)%\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex GreenRegex();

    // Famiglie volutamente escluse: strip, BTP€i (indicizzati), Valore/Italia/Futura/Più (cedole a gradino o indicizzate).
    [GeneratedRegex(@"strip|^btpi|valore|italia|futura|piu", RegexOptions.IgnoreCase)]
    private static partial Regex KnownExcludedRegex();

    /// <summary>True se il nome appartiene a una famiglia di titoli che escludiamo di proposito.</summary>
    public static bool IsKnownExcludedFamily(string description) => KnownExcludedRegex().IsMatch(description);

    /// <summary>True se il nome è uno dei formati riconosciuti per i BTP a tasso fisso.</summary>
    public static bool IsRecognizedFixedRate(string description) => TryParseFixedRate(description, out _, out _);

    private sealed record ListedRow(string Isin, string Description, string Last, string Maturity);

    public static async Task<BtpFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var listed = new List<ListedRow>();

        for (var firstPage = 1; firstPage <= MaxPages; firstPage += PagesPerBatch)
        {
            var batch = await Task.WhenAll(Enumerable.Range(firstPage, PagesPerBatch)
                .Select(page => FetchPageAsync(page, cancellationToken)));

            var reachedEnd = false;
            foreach (var rows in batch)
            {
                if (rows.Count == 0)
                {
                    reachedEnd = true;
                    break;
                }
                listed.AddRange(rows);
            }

            if (reachedEnd)
            {
                break;
            }
        }

        if (listed.Count == 0)
        {
            throw new InvalidDataException(Loc.T("Borsa Italiana non ha restituito nessun titolo: il formato della pagina potrebbe essere cambiato."));
        }

        var bonds = new List<BtpBond>();
        var unrecognized = new List<string>();
        var skippedNoPrice = 0;
        foreach (var row in listed.GroupBy(r => r.Isin).Select(g => g.First()))
        {
            if (!TryParseFixedRate(row.Description, out var rate, out var green))
            {
                if (!IsKnownExcludedFamily(row.Description))
                {
                    unrecognized.Add(row.Description);
                }
                continue;
            }

            if (!decimal.TryParse(row.Last, NumberStyles.Number, It, out var price) || price <= 0m)
            {
                skippedNoPrice++;
                continue;
            }

            if (!DateOnly.TryParseExact(row.Maturity, "dd/MM/yyyy", It, DateTimeStyles.None, out var maturity))
            {
                continue;
            }

            bonds.Add(new BtpBond(row.Isin, FriendlyName(rate, maturity, green), rate, maturity, price));
        }

        if (bonds.Count == 0)
        {
            throw new InvalidDataException(Loc.T("Nessun BTP a tasso fisso riconosciuto nella lista di Borsa Italiana."));
        }

        return new BtpFetchResult(
            bonds.OrderBy(b => b.Maturity).ThenBy(b => b.Isin).ToList(), listed.Count, skippedNoPrice, unrecognized);
    }

    private static async Task<List<ListedRow>> FetchPageAsync(int page, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(string.Format(PageUrl, page), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return RowRegex().Matches(html)
            .Select(m => new ListedRow(
                m.Groups[1].Value,
                WebUtility.HtmlDecode(m.Groups[2].Value).Trim(),
                m.Groups[3].Value.Trim(),
                m.Groups[5].Value.Trim()))
            .ToList();
    }

    private static bool TryParseFixedRate(string description, out decimal rate, out bool green)
    {
        rate = 0m;
        green = false;

        string? rateText = null;
        var match = OldStyleRegex().Match(description);
        if (match.Success)
        {
            rateText = match.Groups["rate"].Value;
        }
        else if ((match = FxTfRegex().Match(description)).Success)
        {
            rateText = match.Groups["rate"].Value;
            green = match.Groups["green"].Success;
        }
        else if ((match = GreenRegex().Match(description)).Success)
        {
            rateText = match.Groups["rate"].Value;
            green = true;
        }

        if (rateText is null
            || !decimal.TryParse(rateText.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var percent)
            || percent <= 0m)
        {
            return false;
        }

        rate = Math.Round(percent / 100m, 6);
        return true;
    }

    /// <summary>Nome leggibile nello stile del foglio Excel: "BTP 2,95% Lug30", "BTP Green 4% Ott31".</summary>
    private static string FriendlyName(decimal rate, DateOnly maturity, bool green)
    {
        var percent = (rate * 100m).ToString("0.####", It);
        var month = MonthAbbreviations[maturity.Month - 1];
        return $"BTP{(green ? " Green" : "")} {percent}% {month}{maturity.Year % 100:00}";
    }
}
