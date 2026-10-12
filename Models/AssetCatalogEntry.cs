namespace DPOper.Models;

/// <summary>
/// Anagrafica di un asset per cui teniamo uno storico quotazioni: le sue identità note
/// (simbolo, ISIN interno) e il codice di mapping per ciascun provider di quotazioni,
/// più il nome del file (sanificato) dove è salvato il suo storico.
/// </summary>
public sealed class AssetCatalogEntry
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string InternalIsin { get; set; } = "";
    public string AssetClass { get; set; } = "";
    public string FileName { get; set; } = "";
    public Dictionary<string, string> ProviderMappings { get; set; } = new();
}
