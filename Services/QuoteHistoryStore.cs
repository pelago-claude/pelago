using System.Text.Json;
using Pelago.Models;

namespace Pelago.Services;

/// <summary>
/// Storico delle quotazioni: un file JSON per asset (il nome è quello registrato in
/// <see cref="AssetCatalogEntry.FileName"/>), così un aggiornamento su un asset non tocca gli
/// altri e ogni file resta piccolo indipendentemente da quanti asset teniamo sotto osservazione.
/// </summary>
public static class QuoteHistoryStore
{
    private static readonly string QuotesDirectory = Path.Combine(
        DataLocations.Root, "quotes");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Data/ora (UTC) dell'ultimo aggiornamento di una qualsiasi quotazione, o null se non ce ne sono.</summary>
    public static DateTime? LastUpdateUtc()
    {
        if (!Directory.Exists(QuotesDirectory))
        {
            return null;
        }

        var files = new DirectoryInfo(QuotesDirectory).GetFiles("*.json");
        return files.Length == 0 ? null : files.Max(f => f.LastWriteTimeUtc);
    }

    public static List<QuoteEntry> Load(AssetCatalogEntry entry)
    {
        var path = Path.Combine(QuotesDirectory, entry.FileName);
        if (!File.Exists(path))
        {
            return new List<QuoteEntry>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<QuoteEntry>>(json, JsonOptions) ?? new List<QuoteEntry>();
        }
        catch (IOException)
        {
            return new List<QuoteEntry>();
        }
        catch (JsonException)
        {
            return new List<QuoteEntry>();
        }
    }

    public static void Append(AssetCatalogEntry entry, QuoteEntry quote)
    {
        Directory.CreateDirectory(QuotesDirectory);
        var quotes = Load(entry);
        quotes.Add(quote);
        var path = Path.Combine(QuotesDirectory, entry.FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(quotes, JsonOptions));
    }


    /// <summary>Riscrive l'intero storico di un asset (usato dalla riparazione dei prezzi in valuta estera).</summary>
    public static void Save(AssetCatalogEntry entry, List<QuoteEntry> quotes)
    {
        Directory.CreateDirectory(QuotesDirectory);
        File.WriteAllText(Path.Combine(QuotesDirectory, entry.FileName), JsonSerializer.Serialize(quotes, JsonOptions));
    }
    /// <summary>
    /// Carica lo storico di tutti gli asset del catalogo come timeline (timestamp, prezzo) per
    /// chiave, pronto per essere passato a <see cref="PortfolioCalculator"/>.
    /// </summary>
    public static Dictionary<string, List<(DateTime Timestamp, decimal Price)>> LoadAllAsTimelines(List<AssetCatalogEntry> catalog)
    {
        var result = new Dictionary<string, List<(DateTime, decimal)>>();
        foreach (var entry in catalog)
        {
            var quotes = Load(entry);
            if (quotes.Count == 0)
            {
                continue;
            }
            // Solo prezzi in euro: una quotazione in un'altra valuta (non ancora riparata) darebbe un valore sbagliato.
            var inEuro = quotes.Where(q => CurrencyConverter.IsEur(q.Currency)).Select(q => (q.Timestamp, q.Price)).ToList();
            if (inEuro.Count > 0)
            {
                result[entry.Key] = inEuro;
            }
        }
        return result;
    }
}
