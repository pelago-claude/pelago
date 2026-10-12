using System.Text;
using System.Text.Json;

namespace DPOper.Services;

/// <summary>Il nome che l'utente ha dato a un titolo e quando l'ha scelto.</summary>
public sealed record NameEntry(string Name, DateTime ChangedUtc);

/// <summary>
/// Il registro dei nomi dei titoli: a ogni codice (ISIN, ticker o la chiave con cui l'app riconosce il titolo) l'utente può dare il nome che preferisce.
/// Da quel momento quel nome si vede in tutto il programma al posto di quello del file. Si salva in <c>nomi-titoli.json</c> nella cartella dati
/// (finisce anche nel backup): i CSV del broker e il catalogo delle quotazioni non vengono toccati, il nome serve solo per mostrare.
/// </summary>
public sealed class InstrumentNames
{
    public const int MaxLength = 80;

    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, NameEntry> _entries;

    public InstrumentNames() : this(Path.Combine(DataLocations.Root, "nomi-titoli.json"))
    {
    }

    public InstrumentNames(string path)
    {
        _path = path;
        _entries = Load(path);
    }

    /// <summary>È cambiato almeno un nome: le pagine devono rileggere i nomi.</summary>
    public event Action? Changed;

    /// <summary>Il codice come lo si confronta: senza spazi ai bordi e in maiuscolo (ISIN e ticker non distinguono le maiuscole).</summary>
    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>Spazi doppi tolti, bordi puliti, lunghezza limitata.</summary>
    public static string Clean(string? name)
    {
        var text = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length > MaxLength ? text[..MaxLength].TrimEnd() : text;
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

    /// <summary>Il nome scelto dall'utente per un codice, o null se non ne ha scelto uno.</summary>
    public string? Get(string? code)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(Normalize(code), out var entry) ? entry.Name : null;
        }
    }

    public NameEntry? Entry(string? code)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(Normalize(code), out var entry) ? entry : null;
        }
    }

    public bool Has(string? code) => Get(code) is not null;

    /// <summary>Tutti i nomi scelti, in ordine di codice.</summary>
    public IReadOnlyList<(string Code, NameEntry Entry)> All()
    {
        lock (_lock)
        {
            return _entries.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => (e.Key, e.Value)).ToList();
        }
    }

    /// <summary>Dà un nome a un codice; un nome vuoto toglie quello scelto (si torna al nome del file). Restituisce true se qualcosa è cambiato.</summary>
    public bool Set(string? code, string? name)
    {
        var key = Normalize(code);
        if (key.Length == 0)
        {
            return false;
        }

        var clean = Clean(name);
        lock (_lock)
        {
            if (clean.Length == 0)
            {
                if (!_entries.Remove(key))
                {
                    return false;
                }
            }
            else
            {
                if (_entries.TryGetValue(key, out var old) && old.Name == clean)
                {
                    return false;
                }
                _entries[key] = new NameEntry(clean, DateTime.UtcNow);
            }
            Save();
        }

        Changed?.Invoke();
        return true;
    }

    public bool Remove(string? code) => Set(code, null);

    /// <summary>Un altro codice che ha già lo stesso nome scelto (per avvisare che i due titoli verranno raggruppati), o null.</summary>
    public string? OtherCodeWithName(string? code, string? name)
    {
        var key = Normalize(code);
        var clean = Clean(name);
        if (clean.Length == 0)
        {
            return null;
        }

        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.Key != key && string.Equals(e.Value.Name, clean, StringComparison.CurrentCultureIgnoreCase)).Key;
        }
    }

    // ---------------------------------------------------------------- esporta / importa

    /// <summary>I nomi in un CSV con due colonne (isin;nome), da aprire in Excel.</summary>
    public string ExportCsv()
    {
        static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        var lines = new List<string> { "isin;nome" };
        lines.AddRange(All().Select(e => $"{Quote(e.Code)};{Quote(e.Entry.Name)}"));
        return string.Join("\r\n", lines) + "\r\n";
    }

    /// <summary>
    /// Legge un CSV (separatore «;» o «,», prima colonna il codice e seconda il nome; l'intestazione è facoltativa) e dà quei nomi ai codici:
    /// quelli già presenti vengono sostituiti. Le righe senza codice o senza nome sono ignorate.
    /// </summary>
    public (int Imported, int Skipped) ImportCsv(string content)
    {
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
        {
            return (0, 0);
        }

        var delimiter = lines[0].Contains(';') ? ';' : ',';
        var first = Split(lines[0], delimiter);
        var hasHeader = first.Count >= 2 && IsHeader(first[0]) && IsHeader2(first[1]);
        var imported = 0;
        var skipped = 0;
        var changed = false;

        lock (_lock)
        {
            foreach (var line in lines.Skip(hasHeader ? 1 : 0))
            {
                var parts = Split(line, delimiter);
                var key = parts.Count > 0 ? Normalize(parts[0]) : "";
                var name = parts.Count > 1 ? Clean(parts[1]) : "";
                if (key.Length == 0 || name.Length == 0)
                {
                    skipped++;
                    continue;
                }

                if (!_entries.TryGetValue(key, out var old) || old.Name != name)
                {
                    _entries[key] = new NameEntry(name, DateTime.UtcNow);
                    changed = true;
                }
                imported++;
            }

            if (changed)
            {
                Save();
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
        return (imported, skipped);
    }

    private static bool IsHeader(string s) => Normalize(s) is "ISIN" or "CODICE" or "CODE" or "CODICE ISIN";

    private static bool IsHeader2(string s) => Normalize(s) is "NOME" or "NAME" or "NOM" or "NAMEN" or "NOMBRE";

    /// <summary>Divide una riga CSV rispettando le virgolette.</summary>
    private static List<string> Split(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    // ---------------------------------------------------------------- file

    private sealed class StoreFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, StoredName> Names { get; set; } = new();
    }

    private sealed class StoredName
    {
        public string Name { get; set; } = "";
        public DateTime Changed { get; set; }
    }

    private static Dictionary<string, NameEntry> Load(string path)
    {
        var result = new Dictionary<string, NameEntry>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var file = JsonSerializer.Deserialize<StoreFile>(File.ReadAllText(path, Encoding.UTF8));
            foreach (var (code, stored) in file?.Names ?? new())
            {
                var key = Normalize(code);
                var name = Clean(stored.Name);
                if (key.Length > 0 && name.Length > 0)
                {
                    result[key] = new NameEntry(name, DateTime.SpecifyKind(stored.Changed, DateTimeKind.Utc));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // file rovinato: si tiene da parte (non si perde) e si riparte da zero
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
        var file = new StoreFile { Names = _entries.OrderBy(e => e.Key, StringComparer.Ordinal).ToDictionary(e => e.Key, e => new StoredName { Name = e.Value.Name, Changed = e.Value.ChangedUtc }) };
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }
}

/// <summary>Quale titolo si sta rinominando: la finestra dei nomi lo mostra, i codici cliccabili lo impostano.</summary>
public sealed class NameEditor
{
    public string? Code { get; private set; }

    public event Action? Changed;

    public void Open(string? code)
    {
        var key = InstrumentNames.Normalize(code);
        if (key.Length == 0)
        {
            return;
        }
        Code = key;
        Changed?.Invoke();
    }

    public void Close()
    {
        Code = null;
        Changed?.Invoke();
    }
}
