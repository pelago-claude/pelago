using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace DPOper.Services;

/// <summary>
/// Lingue dell'app: italiano (la lingua in cui sono scritti i testi), inglese, francese, tedesco, spagnolo.
/// <para>
/// Il <b>testo italiano è la chiave</b>: nel codice si scrive <c>T("Costi e tasse")</c> e <see cref="T(string)"/> restituisce la traduzione
/// nella lingua corrente, o lo stesso testo italiano se manca (così una frase dimenticata si vede ma non rompe nulla).
/// I dizionari sono i file <c>i18n\en.json</c>, <c>fr.json</c>, <c>de.json</c>, <c>es.json</c> incorporati nell'exe: un oggetto JSON
/// «testo italiano» → «traduzione». I segnaposto <c>{0}</c>, <c>{1}</c>… vanno mantenuti identici in ogni lingua.
/// </para>
/// <para><b>REGOLA:</b> ogni testo nuovo visibile all'utente passa da <c>T(...)</c> e va aggiunto ai quattro dizionari: il test
/// <c>LocalizationTests</c> controlla che nessuna chiave manchi.</para>
/// </summary>
public static class Loc
{
    public sealed record Language(string Code, string Name, string Culture);

    public static readonly IReadOnlyList<Language> Languages = new Language[]
    {
        new("it", "Italiano", "it-IT"),
        new("en", "English", "en-IE"),   // en-IE: euro con il simbolo davanti e date giorno/mese/anno
        new("fr", "Français", "fr-FR"),
        new("de", "Deutsch", "de-DE"),
        new("es", "Español", "es-ES"),
    };

    private static readonly object Gate = new();
    private static Dictionary<string, string> _dictionary = new(StringComparer.Ordinal);

    public static string Lang { get; private set; } = "it";
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Scatta quando cambia la lingua: le pagine si ricostruiscono per mostrare i nuovi testi.</summary>
    public static event Action? Changed;

    /// <summary>Sceglie la lingua all'avvio: quella salvata, altrimenti la lingua di Windows se supportata, altrimenti l'italiano.</summary>
    public static void Init(UiPrefs prefs)
    {
        var saved = prefs.Get("lang");
        var code = Languages.Any(l => l.Code == saved)
            ? saved!
            : Languages.FirstOrDefault(l => l.Code == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)?.Code     // formati regionali di Windows
              ?? Languages.FirstOrDefault(l => l.Code == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)?.Code // lingua di Windows
              ?? "it";
        Apply(code);
    }

    /// <summary>Cambia lingua (e avvisa chi ascolta). Il salvataggio nelle preferenze lo fa chi chiama.</summary>
    public static void SetLanguage(string code)
    {
        if (Languages.All(l => l.Code != code) || code == Lang)
        {
            return;
        }

        Apply(code);
        Changed?.Invoke();
    }

    private static void Apply(string code)
    {
        var language = Languages.First(l => l.Code == code);
        var culture = CultureInfo.GetCultureInfo(language.Culture);
        lock (Gate)
        {
            _dictionary = code == "it" ? new(StringComparer.Ordinal) : LoadDictionary(code);
            Lang = code;
            Culture = culture;
        }

        // anche le conversioni implicite (importi e date dentro le stringhe) seguono la lingua scelta
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    internal static Dictionary<string, string> LoadDictionary(string code)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"i18n/{code}.json");
        if (stream is null)
        {
            return new(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) is { } loaded
            ? new Dictionary<string, string>(loaded, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
    }

    /// <summary>Il testo nella lingua corrente. In italiano (o se la traduzione manca) è il testo stesso.</summary>
    public static string T(string italian)
    {
        lock (Gate)
        {
            return _dictionary.TryGetValue(italian, out var translated) && translated.Length > 0 ? translated : italian;
        }
    }

    /// <summary>Come <see cref="T(string)"/> per frasi con segnaposto <c>{0}</c>, <c>{1}</c>…, riempiti nella cultura della lingua (numeri e date compresi).</summary>
    public static string T(string italian, params object?[] args) => Format(T(italian), italian, args);

    /// <summary>Riempie i segnaposto; se una traduzione sbagliata rompe il formato, si ripiega sul testo italiano invece di far fallire la pagina.</summary>
    private static string Format(string translated, string italian, object?[] args)
    {
        try
        {
            return string.Format(Culture, translated, args);
        }
        catch (FormatException)
        {
            return string.Format(Culture, italian, args);
        }
    }

    /// <summary>Come <see cref="T(string, object[])"/> ma per frasi con markup (grassetti, link): i valori vengono resi sicuri, il resto del testo è HTML.</summary>
    public static MarkupString H(string italian, params object?[] args) =>
        new(Format(T(italian), italian, args.Select(a => (object?)WebUtility.HtmlEncode(Convert.ToString(a, Culture) ?? "")).ToArray()));

    /// <summary>Singolare o plurale in base al numero: <c>Plural(n, "{0} giorno", "{0} giorni")</c>.</summary>
    public static string Plural(long count, string singular, string plural) => T(count == 1 ? singular : plural, count);

    /// <summary>Se esiste una traduzione per questo testo (usato dai test e dal controllo di copertura).</summary>
    public static bool Has(string italian)
    {
        lock (Gate)
        {
            return _dictionary.ContainsKey(italian);
        }
    }
}
