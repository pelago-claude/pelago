using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class LocalizationTests
{
    private static readonly string[] Languages = { "en", "fr", "de", "es" };

    /// <summary>Scrive l'elenco dei testi da tradurre in i18n\_keys.json (si lancia a mano: PELAGO_DUMP_KEYS=1).</summary>
    [Fact]
    public void Dump_required_keys_when_asked()
    {
        if (Environment.GetEnvironmentVariable("PELAGO_DUMP_KEYS") != "1")
        {
            return;
        }

        var keys = I18nSource.RequiredKeys();
        var path = Path.Combine(I18nSource.Root, "i18n", "_keys.json");
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(path, JsonSerializer.Serialize(keys, options), new UTF8Encoding(false));

        // cosa manca (e cosa non serve più) rispetto al dizionario inglese: base per tradurre le novità
        var en = Loc.LoadDictionary("en");
        File.WriteAllText(Path.Combine(I18nSource.Root, "i18n", "_missing.json"), JsonSerializer.Serialize(keys.Where(k => !en.ContainsKey(k)).ToList(), options), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(I18nSource.Root, "i18n", "_orphans.json"), JsonSerializer.Serialize(en.Keys.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList(), options), new UTF8Encoding(false));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    public void Every_text_has_a_translation(string code)
    {
        var dictionary = Loc.LoadDictionary(code);
        var missing = I18nSource.RequiredKeys().Where(k => !dictionary.TryGetValue(k, out var t) || string.IsNullOrWhiteSpace(t)).ToList();

        Assert.True(missing.Count == 0,
            $"{code}: mancano {missing.Count} traduzioni, per esempio:\n- " + string.Join("\n- ", missing.Take(15).Select(k => k.Length > 100 ? k[..100] + "…" : k)));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    public void Translations_keep_placeholders_and_markup(string code)
    {
        var problems = new List<string>();
        foreach (var (key, translation) in Loc.LoadDictionary(code))
        {
            if (!Same(Placeholders(key), Placeholders(translation)))
            {
                problems.Add($"segnaposto diversi: «{Short(key)}» -> «{Short(translation)}»");
            }
            else if (!Same(Tags(key), Tags(translation)))
            {
                problems.Add($"tag HTML diversi: «{Short(key)}» -> «{Short(translation)}»");
            }
        }

        Assert.True(problems.Count == 0, $"{code}: {problems.Count} problemi, per esempio:\n- " + string.Join("\n- ", problems.Take(10)));
    }

    [Fact]
    public void Dictionaries_have_no_orphan_keys()
    {
        var required = I18nSource.RequiredKeys();
        foreach (var code in Languages)
        {
            var orphans = Loc.LoadDictionary(code).Keys.Where(k => !required.Contains(k)).ToList();
            Assert.True(orphans.Count == 0, $"{code}: {orphans.Count} chiavi non più usate, per esempio:\n- " + string.Join("\n- ", orphans.Take(10).Select(Short)));
        }
    }

    [Fact]
    public void T_falls_back_to_the_italian_text_and_formats_with_the_culture()
    {
        Assert.Equal("Testo mai tradotto 5", Loc.T("Testo mai tradotto {0}", 5));
        Assert.Equal("Testo mai tradotto", Loc.T("Testo mai tradotto"));
    }

    private static List<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{([A-Za-z0-9_]+)(?::[^}]*)?\}").Select(m => m.Groups[1].Value).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static List<string> Tags(string text) =>
        Regex.Matches(text, @"</?[a-z][^>]*>").Select(m => Regex.Replace(m.Value, @"\s+", " ")).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static bool Same(List<string> a, List<string> b) => a.SequenceEqual(b);

    private static string Short(string s) => s.Length > 90 ? s[..90] + "…" : s;
}
