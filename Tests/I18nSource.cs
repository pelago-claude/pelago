using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Pelago.Services;

namespace Pelago.Tests;

/// <summary>
/// Raccoglie tutti i testi italiani che l'app può mostrare (le «chiavi» dei dizionari di traduzione): le chiamate <c>T("…")</c>, <c>H("…")</c>
/// e <c>Plural(n, "…", "…")</c> nel codice, i titoli delle colonne, le etichette fisse e i testi dei servizi (aiuto, storia, nomi di Stati e valute).
/// </summary>
internal static class I18nSource
{
    internal static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, ".."));

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private const string Str = "\"(?:[^\"\\\\]|\\\\.)*\"";
    private static readonly string Concat = $"{Str}(?:\\s*\\+\\s*{Str})*";

    // chiamate di traduzione: T("…"), Loc.T("…"), H("…"), Loc.H("…")
    private static readonly Regex CallRegex = new($@"(?<![\w.])(?:Loc\.)?[TH]\(\s*(?<key>{Concat})", RegexOptions.Compiled);
    private static readonly Regex PluralRegex = new($@"\bPlural\([^,]+,\s*(?<a>{Concat})\s*,\s*(?<b>{Concat})", RegexOptions.Compiled);
    private static readonly Regex ColumnRegex = new($@"\bCol\.\w+<[^>]+>\(\s*(?<key>{Str})", RegexOptions.Compiled);
    private static readonly Regex StringRegex = new(Str, RegexOptions.Compiled);

    public static IEnumerable<string> SourceFiles()
    {
        foreach (var dir in new[] { "Pages", "Components", "Shared", "Services", "Models" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, dir), "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal)))
            {
                yield return file;
            }
        }
        yield return Path.Combine(Root, "ShellForm.cs");
    }

    /// <summary>Tutte le chiavi richieste dal codice e dai servizi.</summary>
    public static SortedSet<string> RequiredKeys()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in SourceFiles())
        {
            var name = Path.GetFileName(file);
            if (name is "Loc.cs" or "HelpTexts.cs" or "ChangeLog.cs")
            {
                continue;
            }

            var text = File.ReadAllText(file, Encoding.UTF8);
            foreach (Match m in CallRegex.Matches(text))
            {
                AddFolded(keys, m.Groups["key"].Value);
            }
            foreach (Match m in PluralRegex.Matches(text))
            {
                AddFolded(keys, m.Groups["a"].Value);
                AddFolded(keys, m.Groups["b"].Value);
            }
            foreach (Match m in ColumnRegex.Matches(text))
            {
                AddFolded(keys, m.Groups["key"].Value);
            }

            // etichette fisse tradotte «al momento di mostrarle»: righe di BtpCalc / BtpCompare, filtri, periodi
            if (name == "BtpCalc.razor")
            {
                AddMatches(keys, text, @"\bL\(\$?(" + Str + ")");
                AddMatches(keys, text, @"\bL\(\s*(" + Str + ")");
            }
            if (name == "BtpCompare.razor")
            {
                AddMatches(keys, text, @"\bRow\(\s*(" + Str + ")");
            }
            if (name == "Securities.razor")
            {
                AddMatches(keys, text, @"\(""(?:STOCK|FUND|BOND|CRYPTO|DERIVATIVE)"",\s*(" + Str + @")\)");
            }
            if (name is "Currencies.razor" or "BondCountry.razor")
            {
                AddMatches(keys, text, @"\(\s*(" + Str + @")\s*,\s*\d+\)");
            }
            if (text.Contains("FooterLabel=\""))
            {
                AddMatches(keys, text, @"FooterLabel=(" + Str + ")");
            }
            if (name == "AppState.cs")
            {
                AddMatches(keys, text, @"RunUpdateAsync\([^,]+,\s*(" + Str + ")");
            }
            if (name is "Quotes.razor" or "CommandPalette.razor")
            {
                AddMatches(keys, text, @"(?:Run|RunUpdateAsync)\(\s*(?:new\[\]\s*\{[^}]*\}|[\w.]+)\s*,\s*(" + Str + ")");
            }
            if (name == "DataTable.razor")
            {
                keys.Add("Totale");
            }
            if (name == "CouponSchedule.cs")
            {
                keys.Add("Cedola");
                keys.Add("Rimborso");
            }
            if (name == "PeriodPointsGenerator.cs")
            {
                AddMatches(keys, text, @"\(PeriodType\.\w+,\s*(" + Str + @")\)");
            }
            if (name == "AssetClassPalette.cs")
            {
                AddMatches(keys, text, @"\(""[A-Z]+"",\s*(" + Str + @"),\s*Color\.");
            }
            if (name == "Fmt.cs")
            {
                AddMatches(keys, text, @"\[""[A-Z]+""\]\s*=\s*(" + Str + ")");
            }
            if (name == "TaxReportCalculator.cs")
            {
                AddMatches(keys, text, @"TaxCategory\.\w+\s*=>\s*(" + Str + ")");
                AddMatches(keys, text, @"\b_\s*=>\s*(" + Str + ")");
                AddMatches(keys, text, @"Kind:\s*[^?]*\?\s*(" + Str + @")\s*:");
                AddMatches(keys, text, @"Kind:\s*[^?]*\?\s*" + Str + @"\s*:\s*(" + Str + ")");
            }
            if (name == "TradingStatsCalculator.cs")
            {
                AddMatches(keys, text, @"""(Lunedì|Martedì|Mercoledì|Giovedì|Venerdì|Sabato|Domenica)""");
            }
            if (name == "FxBoard.cs")
            {
                AddMatches(keys, text, @"new\(""[A-Z]{3}"",\s*(" + Str + @")\)");
                keys.Add("BCE");
                keys.Add("Yahoo Finance");
            }
            if (name == "GovBonds.cs")
            {
                AddMatches(keys, text, @"new\(""[A-Z]{2}"",\s*(" + Str + @"),\s*""[A-Z]{3}"",");
                keys.Add("Tesoro USA");
                keys.Add("Bundesbank");
            }
            if (name == "BtpOnlineProvider.cs")
            {
                keys.Add("Borsa Italiana (MOT), ultimo contratto");
            }
        }

        // testi dell'assistente: titoli, spiegazioni, parole chiave
        foreach (var entry in HelpTexts.All)
        {
            keys.Add(entry.Title);
            keys.Add(entry.Short);
            if (entry.More is not null)
            {
                keys.Add(entry.More);
            }
            if (entry.Keywords.Length > 0)
            {
                keys.Add(entry.Keywords);
            }
        }

        // la storia dell'app
        foreach (var release in ChangeLog.Releases)
        {
            keys.Add(release.Title);
            keys.Add(release.Summary);
            foreach (var s in release.Story.Concat(release.News))
            {
                keys.Add(s);
            }
        }

        // parole chiave delle voci della ricerca rapida (CommandPalette): K("…")
        foreach (var file in SourceFiles().Where(f => Path.GetFileName(f) == "CommandPalette.razor"))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file, Encoding.UTF8), @"\bK\((" + Str + @")\)"))
            {
                AddFolded(keys, m.Groups[1].Value);
            }
        }

        keys.RemoveWhere(k => string.IsNullOrWhiteSpace(k));
        return keys;
    }

    private static void AddMatches(SortedSet<string> keys, string text, string pattern)
    {
        foreach (Match m in Regex.Matches(text, pattern))
        {
            AddFolded(keys, m.Groups[1].Value);
        }
    }

    /// <summary>Unisce i pezzi di una concatenazione di stringhe ("a" + "b") e toglie le virgolette e le sequenze di escape.</summary>
    private static void AddFolded(SortedSet<string> keys, string expression)
    {
        var sb = new StringBuilder();
        foreach (Match m in StringRegex.Matches(expression))
        {
            sb.Append(Unescape(m.Value[1..^1]));
        }

        var key = sb.ToString();
        if (key.Length > 0 && Regex.IsMatch(key, @"\p{L}{2}"))
        {
            keys.Add(key);
        }
    }

    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
}
