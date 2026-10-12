using System.Text.Json;

namespace DPOper.Services;

/// <summary>Preferenze della nuova interfaccia (tema, ordinamento delle tabelle...) in <c>%LocalAppData%\DPOper\webprefs.json</c>.</summary>
public sealed class UiPrefs
{
    private static readonly string PrefsPath = Path.Combine(
        DataLocations.Root, "webprefs.json");

    private readonly object _lock = new();
    private readonly Dictionary<string, string> _values;

    public UiPrefs()
    {
        _values = Load();
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            return _values.GetValueOrDefault(key);
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            if (_values.TryGetValue(key, out var existing) && existing == value)
            {
                return;
            }
            _values[key] = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath)!);
                File.WriteAllText(PrefsPath, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException)
            {
                // Non critico: le preferenze si perdono solo al prossimo avvio.
            }
        }
    }

    private static Dictionary<string, string> Load()
    {
        try
        {
            return File.Exists(PrefsPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PrefsPath)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new();
        }
    }
}
