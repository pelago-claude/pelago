using System.Text.Json;

namespace DPOper.Services;

public sealed class GridColumnState
{
    public int Width { get; set; }
    public int DisplayIndex { get; set; }
}

public sealed class GridState
{
    public Dictionary<string, GridColumnState> Columns { get; set; } = new();
    public string? SortColumn { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class FormLayoutState
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int WindowState { get; set; }
    public Dictionary<string, GridState> Grids { get; set; } = new();
}

/// <summary>
/// Persiste su disco (JSON) la disposizione delle finestre e delle griglie: stessa idea di
/// WSDoc24Tools (un file unico, chiave per form), qui in <c>%LocalAppData%\DPOper\uisettings.json</c>
/// perché DPOper è mono-utente e non serve il sottopercorso per utente di dominio.
/// </summary>
public static class UiSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        DataLocations.Root, "uisettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static Dictionary<string, FormLayoutState>? _cache;

    public static FormLayoutState? Get(string key) => LoadAll().GetValueOrDefault(key);

    public static void Set(string key, FormLayoutState state)
    {
        var all = LoadAll();
        all[key] = state;

        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(all, JsonOptions));
        }
        catch (IOException)
        {
            // Non critico: se il salvataggio fallisce, al prossimo avvio si riparte dal layout di default.
        }
    }

    private static Dictionary<string, FormLayoutState> LoadAll()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(SettingsPath))
        {
            _cache = new Dictionary<string, FormLayoutState>();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            _cache = JsonSerializer.Deserialize<Dictionary<string, FormLayoutState>>(json, JsonOptions)
                ?? new Dictionary<string, FormLayoutState>();
        }
        catch (IOException)
        {
            _cache = new Dictionary<string, FormLayoutState>();
        }
        catch (JsonException)
        {
            _cache = new Dictionary<string, FormLayoutState>();
        }

        return _cache;
    }
}
