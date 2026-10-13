using System.Text.Json;
using Pelago.Models;

namespace Pelago.Services;

/// <summary>
/// Anagrafica degli asset per cui teniamo uno storico quotazioni (catalog.json), separata dallo
/// storico vero e proprio (un file per asset, vedi <see cref="QuoteHistoryStore"/>) perché cambia
/// raramente mentre lo storico cresce ad ogni aggiornamento.
/// </summary>
public static class AssetCatalogStore
{
    private static readonly string CatalogPath = Path.Combine(
        DataLocations.Root, "catalog.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Mapping di default simbolo → id CoinGecko per le crypto più comuni. Se un simbolo non è
    /// presente qui, la voce di catalogo viene comunque creata ma senza mapping "coingecko": andrà
    /// aggiunto a mano nel file (è JSON semplice) o esteso qui.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultCoinGeckoIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum",
        ["XRP"] = "ripple",
        ["SOL"] = "solana",
        ["NEAR"] = "near",
        ["DOGE"] = "dogecoin",
        ["XLM"] = "stellar",
        ["HBAR"] = "hedera-hashgraph",
        ["ADA"] = "cardano",
        ["DOT"] = "polkadot",
        ["QNT"] = "quant-network",   // non «quant»: è l'indirizzo della pagina, ma con quell'id il prezzo è di un'altra moneta
    };

    public static List<AssetCatalogEntry> Load()
    {
        if (!File.Exists(CatalogPath))
        {
            return new List<AssetCatalogEntry>();
        }

        try
        {
            var json = File.ReadAllText(CatalogPath);
            return JsonSerializer.Deserialize<List<AssetCatalogEntry>>(json, JsonOptions) ?? new List<AssetCatalogEntry>();
        }
        catch (IOException)
        {
            return new List<AssetCatalogEntry>();
        }
        catch (JsonException)
        {
            return new List<AssetCatalogEntry>();
        }
    }

    public static void Save(List<AssetCatalogEntry> catalog)
    {
        var directory = Path.GetDirectoryName(CatalogPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(CatalogPath, JsonSerializer.Serialize(catalog, JsonOptions));
    }

    /// <summary>
    /// Assicura che ogni asset presente nelle operazioni (della classe indicata) abbia una voce di
    /// catalogo, aggiungendo quelle mancanti con il mapping di default se noto. Restituisce il
    /// catalogo aggiornato (e lo salva se sono state aggiunte voci nuove).
    /// </summary>
    public static List<AssetCatalogEntry> SyncFromOperations(List<Operation> operations, string assetClass)
    {
        var catalog = Load();
        var byKey = catalog.ToDictionary(e => e.Key);
        var usedFileNames = catalog.Select(e => e.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        var assets = operations
            .Where(o => o.AssetClass == assetClass && !string.IsNullOrWhiteSpace(o.PositionKey))
            .GroupBy(o => o.PositionKey)
            .Select(g => g.First());

        foreach (var op in assets)
        {
            if (byKey.TryGetValue(op.PositionKey, out var existing))
            {
                // una voce creata prima che il simbolo fosse nell'elenco, senza id CoinGecko: la si completa invece di lasciarla per sempre senza prezzo
                if (existing.AssetClass == "CRYPTO"
                    && (!existing.ProviderMappings.TryGetValue("coingecko", out var current) || string.IsNullOrWhiteSpace(current))
                    && DefaultCoinGeckoIds.TryGetValue(existing.Symbol, out var knownId))
                {
                    existing.ProviderMappings["coingecko"] = knownId;
                    changed = true;
                }
                continue;
            }

            var fileName = SanitizeFileName(op.PositionKey, usedFileNames);
            usedFileNames.Add(fileName);

            var providerMappings = new Dictionary<string, string>();
            if (op.AssetClass == "CRYPTO" && DefaultCoinGeckoIds.TryGetValue(op.Symbol, out var coinGeckoId))
            {
                providerMappings["coingecko"] = coinGeckoId;
            }

            // Per STOCK/FUND il campo "symbol" del CSV è già l'ISIN: non serve estrarne uno dalla
            // descrizione (che per le crypto invece contiene un codice interno diverso dal ticker).
            var internalIsin = op.AssetClass == "CRYPTO" ? ExtractInternalIsin(op.Description) : "";

            var entry = new AssetCatalogEntry
            {
                Key = op.PositionKey,
                Name = op.Name,
                Symbol = op.Symbol,
                InternalIsin = internalIsin,
                AssetClass = op.AssetClass,
                FileName = fileName,
                ProviderMappings = providerMappings,
            };

            catalog.Add(entry);
            byKey[entry.Key] = entry;
            changed = true;
        }

        if (changed)
        {
            Save(catalog);
        }

        return catalog;
    }

    /// <summary>
    /// Assicura che il catalogo contenga la voce per il cambio EUR/USD (serve a convertire in EUR
    /// le quotazioni di azioni/fondi denominate in USD, ed è anche un "ticker" aggiornabile a sé).
    /// </summary>
    public static void EnsureFxEntry()
    {
        var catalog = Load();
        if (catalog.Any(e => e.Key == "EURUSD"))
        {
            return;
        }

        catalog.Add(new AssetCatalogEntry
        {
            Key = "EURUSD",
            Name = "Cambio EUR/USD",
            Symbol = "EURUSD",
            InternalIsin = "",
            AssetClass = "FX",
            FileName = "EURUSD.json",
            ProviderMappings = new Dictionary<string, string> { ["frankfurter"] = "USD" },
        });

        Save(catalog);
    }

    /// <summary>Salva il mapping di un provider risolto per un asset (es. il ticker Yahoo trovato per un ISIN).</summary>
    public static void UpdateMapping(string key, string provider, string mappingValue)
    {
        var catalog = Load();
        var entry = catalog.FirstOrDefault(e => e.Key == key);
        if (entry is null)
        {
            return;
        }

        entry.ProviderMappings[provider] = mappingValue;
        Save(catalog);
    }

    private static string ExtractInternalIsin(string description)
    {
        // Le descrizioni delle operazioni crypto sono del tipo "Buy trade XF000XRP0018 XRP, quantity: ...":
        // il secondo token è il codice interno del broker (non un ISIN standard, ma l'unico identificativo oltre al simbolo).
        var parts = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[2] : "";
    }

    private static string SanitizeFileName(string key, HashSet<string> usedFileNames)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(key.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (sanitized.Length == 0)
        {
            sanitized = "asset";
        }

        var candidate = sanitized + ".json";
        var suffix = 2;
        while (usedFileNames.Contains(candidate))
        {
            candidate = $"{sanitized}_{suffix}.json";
            suffix++;
        }

        return candidate;
    }
}
