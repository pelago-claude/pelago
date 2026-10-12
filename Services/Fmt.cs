using System.Globalization;

namespace DPOper.Services;

/// <summary>Formattazione all'italiana condivisa da tutte le pagine (importi, quantità, date, percentuali).</summary>
public static class Fmt
{
    /// <summary>La cultura della lingua scelta (il nome «It» è rimasto per le pagine che già lo usano): numeri, importi e date la seguono.</summary>
    public static CultureInfo It => Loc.Culture;

    /// <summary>Nomi italiani delle categorie (sono anche le chiavi di traduzione: si traducono in <see cref="ClassLabel"/>).</summary>
    public static readonly Dictionary<string, string> AssetClassLabels = new()
    {
        ["STOCK"] = "Azione",
        ["FUND"] = "Fondo",
        ["BOND"] = "Obbligazione",
        ["CRYPTO"] = "Crypto",
        ["DERIVATIVE"] = "Derivato",
        ["FX"] = "Cambio",
    };

    public static string ClassLabel(string assetClass) => Loc.T(AssetClassLabels.GetValueOrDefault(assetClass, assetClass));

    public static string Money(decimal? value) => value is { } v ? v.ToString("C2", It) : "";
    public static string Money4(decimal? value) => value is { } v ? v.ToString("C4", It) : "";
    public static string Num(decimal? value, int decimals = 4) => value is { } v ? v.ToString("N" + decimals, It) : "";
    public static string Pct(decimal? value, int decimals = 2) => value is { } v ? v.ToString("P" + decimals, It) : "";
    public static string Pct(double? value, int decimals = 2) => value is { } v ? v.ToString("P" + decimals, It) : Loc.T("n/d");
    public static string Date(DateTime? value, string format = "dd/MM/yyyy") => value is { } v ? v.ToString(format, It) : "";
    public static string Date(DateOnly value, string format = "dd/MM/yyyy") => value.ToString(format, It);

    public static string Hex(Color color) => ColorTranslator.ToHtml(color);

    /// <summary>Classe CSS per colorare importi positivi/negativi.</summary>
    public static string? Sign(decimal? value) => value switch { null => null, >= 0 => "pos", _ => "neg" };
}
