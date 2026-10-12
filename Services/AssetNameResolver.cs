using System.Text.RegularExpressions;

namespace DPOper.Services;

/// <summary>
/// Per i derivati (turbo/warrant) il campo "name" del CSV contiene solo un'etichetta sintetica
/// (es. "Long 61,49 $"), poco utile per riconoscere lo strumento. La descrizione della singola
/// operazione contiene però il nome completo del prodotto ("Turbo auf ACME TECH. DL-,00001"):
/// questa classe lo estrae per comporre un nome più leggibile.
/// </summary>
public static partial class AssetNameResolver
{
    [GeneratedRegex(
        @"^(?:Buy trade|Sell trade|Savings plan execution|Cancellation Sell trade|Correction Sell trade)\s+\S+\s+(?<name>.+?),\s*quantity:",
        RegexOptions.IgnoreCase)]
    private static partial Regex ProductNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string ResolveDisplayName(string shortName, string assetClass, IEnumerable<string> descriptions)
    {
        if (assetClass != "DERIVATIVE")
        {
            return shortName;
        }

        var fullName = descriptions
            .Select(ExtractProductName)
            .FirstOrDefault(n => n is not null);

        return fullName is null ? shortName : $"{fullName} ({shortName})";
    }

    /// <summary>
    /// Nome "intero" per le tabelle riassuntive: per fondi, obbligazioni e derivati ricostruisce il nome del prodotto dalla
    /// descrizione delle operazioni (la più recente); per azioni e crypto il nome breve è già quello giusto.
    /// Per i fondi toglie l'emittente davanti al trattino ("Xtrackers - Xtrackers MSCI EM ..." → "Xtrackers MSCI EM ...").
    /// </summary>
    public static string ResolveFullName(string shortName, string assetClass, IEnumerable<string> descriptionsNewestFirst)
    {
        if (assetClass is not ("FUND" or "BOND" or "DERIVATIVE"))
        {
            return shortName;
        }

        var product = descriptionsNewestFirst.Select(ExtractProductName).FirstOrDefault(n => n is not null);
        if (product is null)
        {
            return shortName;
        }

        if (assetClass == "FUND")
        {
            var dash = product.IndexOf(" - ", StringComparison.Ordinal);
            return dash > 0 ? product[(dash + 3)..].Trim() : product;
        }

        return string.Equals(product, shortName, StringComparison.OrdinalIgnoreCase) ? product : $"{product} ({shortName})";
    }

    private static string? ExtractProductName(string description)
    {
        var match = ProductNameRegex().Match(description);
        if (!match.Success)
        {
            return null;
        }

        return WhitespaceRegex().Replace(match.Groups["name"].Value, " ").Trim();
    }
}
