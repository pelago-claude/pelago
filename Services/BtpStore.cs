using System.Text.Json;
using Pelago.Models;

namespace Pelago.Services;

/// <summary>Gli ultimi valori usati nel calcolatore, così alla prossima apertura si riparte da lì.</summary>
public sealed class BtpInputs
{
    public decimal Capital { get; set; } = 50000m;
    public decimal Commission { get; set; } = 0m;
    public decimal TaxRate { get; set; } = 0.125m;
    public decimal Lot { get; set; } = 1000m;
}

/// <summary>Ultimo elenco BTP scaricato online (con data/ora e fonte), più gli ultimi input del calcolatore.</summary>
public sealed class BtpData
{
    public DateTime? UpdatedAt { get; set; }
    public string? Source { get; set; }
    public List<BtpBond> Bonds { get; set; } = new();
    public BtpInputs Inputs { get; set; } = new();
}

/// <summary>Persistenza dell'elenco BTP in <c>%LocalAppData%\Pelago\btp.json</c> (stessa cartella degli altri dati).</summary>
public static class BtpStore
{
    private static readonly string StorePath = Path.Combine(
        DataLocations.Root, "btp.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static BtpData Load()
    {
        if (!File.Exists(StorePath))
        {
            return new BtpData();
        }

        try
        {
            return JsonSerializer.Deserialize<BtpData>(File.ReadAllText(StorePath), JsonOptions) ?? new BtpData();
        }
        catch (IOException)
        {
            return new BtpData();
        }
        catch (JsonException)
        {
            return new BtpData();
        }
    }

    public static void Save(BtpData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(data, JsonOptions));
    }
}
