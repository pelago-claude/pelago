namespace DPOper.Services;

/// <summary>
/// Etichetta e colore condivisi per ogni categoria di asset, così tutte le viste (composizione ad
/// una data, grafico di composizione giornaliera, ecc.) usano esattamente gli stessi colori.
/// "Derivati" usa un marrone (non un rosso) apposta: nei grafici lo scostamento sopra/sotto i
/// versamenti usa verde/rosso, e un rosso troppo simile ai Derivati genererebbe confusione visiva.
/// </summary>
public static class AssetClassPalette
{
    public static readonly (string Key, string Label, Color Color)[] Categories =
    {
        ("CASH", "Saldo disponibile", Color.SteelBlue),
        ("CRYPTO", "Crypto", Color.MediumPurple),
        ("BOND", "Obbligazioni", Color.MediumSeaGreen),
        ("STOCK", "Azioni", Color.DarkOrange),
        ("FUND", "Fondi", Color.Goldenrod),
        ("DERIVATIVE", "Derivati", Color.SaddleBrown),
        ("INTEREST", "Interessi", Color.Teal),
    };

    public static Color ColorFor(string key) => Categories.First(c => c.Key == key).Color;

    public static string LabelFor(string key) => Loc.T(Categories.First(c => c.Key == key).Label);
}
