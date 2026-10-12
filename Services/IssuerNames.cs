using System.Text;
using System.Text.Json;

namespace DPOper.Services;

/// <summary>Il nome che l'emittente dà a un prodotto («Turbo su Oro»), letto dal suo sito, e quando.</summary>
public sealed record IssuerNameEntry(string Product, DateTime FetchedUtc);

/// <summary>
/// I nomi dei derivati letti dal sito dell'emittente, uno per codice ISIN. Si imparano gli stessi giorni dei prezzi (la pagina del prodotto li contiene):
/// non servono richieste in più. Restano salvati anche quando il prodotto si chiude, perché il nome non cambia. Si salvano in <c>nomi-emittenti.json</c>
/// nella cartella dati (finisce anche nel backup). Nelle liste il nome dell'emittente prende il posto di quello ricavato dal file del broker, ma
/// non quello scelto dall'utente (vedi <see cref="InstrumentNames"/> e <see cref="AppState.FullNameOf"/>).
/// </summary>
public sealed class IssuerNames
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, IssuerNameEntry> _entries;

    public IssuerNames() : this(Path.Combine(DataLocations.Root, "nomi-emittenti.json"))
    {
    }

    public IssuerNames(string path)
    {
        _path = path;
        _entries = Load(path);
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Il nome del prodotto secondo l'emittente, o null se non lo si è ancora letto.</summary>
    public string? Get(string? code)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(InstrumentNames.Normalize(code), out var entry) ? entry.Product : null;
        }
    }

    public IssuerNameEntry? Entry(string? code)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(InstrumentNames.Normalize(code), out var entry) ? entry : null;
        }
    }

    public IReadOnlyList<(string Code, IssuerNameEntry Entry)> All()
    {
        lock (_lock)
        {
            return _entries.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => (e.Key, e.Value)).ToList();
        }
    }

    /// <summary>Registra il nome letto dal sito. Restituisce true se qualcosa è cambiato (nome nuovo o diverso).</summary>
    public bool Set(string? code, string? product)
    {
        var key = InstrumentNames.Normalize(code);
        var clean = InstrumentNames.Clean(product);
        if (key.Length == 0 || clean.Length == 0)
        {
            return false;
        }

        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var old) && old.Product == clean)
            {
                return false;
            }
            _entries[key] = new IssuerNameEntry(clean, DateTime.UtcNow);
            Save();
            return true;
        }
    }

    // ---------------------------------------------------------------- file

    private sealed class StoreFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, StoredName> Names { get; set; } = new();
    }

    private sealed class StoredName
    {
        public string Product { get; set; } = "";
        public DateTime Fetched { get; set; }
    }

    private static Dictionary<string, IssuerNameEntry> Load(string path)
    {
        var result = new Dictionary<string, IssuerNameEntry>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var file = JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(path, Encoding.UTF8));
            foreach (var (code, stored) in file?.Names ?? new())
            {
                var key = InstrumentNames.Normalize(code);
                var product = InstrumentNames.Clean(stored.Product);
                if (key.Length > 0 && product.Length > 0)
                {
                    result[key] = new IssuerNameEntry(product, DateTime.SpecifyKind(stored.Fetched, DateTimeKind.Utc));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // file rovinato: si tiene da parte e si riparte da zero (i nomi si rileggono dai siti al prossimo aggiornamento)
            try
            {
                File.Move(path, path + ".rovinato", overwrite: true);
            }
            catch (IOException)
            {
            }
        }
        return result;
    }

    /// <summary>Scrive il file in modo sicuro: prima un file temporaneo, poi lo si sostituisce.</summary>
    private void Save()
    {
        var file = new StoreFile
        {
            Names = _entries.OrderBy(e => e.Key, StringComparer.Ordinal)
                .ToDictionary(e => e.Key, e => new StoredName { Product = e.Value.Product, Fetched = e.Value.FetchedUtc }),
        };
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }
}
