using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pelago.Services;

/// <summary>
/// Il conto di esempio: un CSV con dati inventati e i prezzi che servono per vederlo vivo (tutto dentro l'eseguibile, vedi la cartella
/// <c>demo</c> del progetto). «Prova con un conto di esempio» lo estrae in una cartella temporanea e apre una SECONDA finestra di Pelago
/// che usa quella cartella come cartella dati: i dati veri dell'utente non si toccano mai, e chiusa la finestra l'esempio non lascia
/// nulla (le cartelle vecchie si ripuliscono da sole).
/// </summary>
public static class DemoAccount
{
    /// <summary>Impostata a «1» nella finestra del conto di esempio.</summary>
    public const string EnvironmentVariable = "PELAGO_DEMO";

    public const string CsvName = "esempio-operazioni.csv";

    private const string ResourcePrefix = "demo/";
    private const string FolderPrefix = "Pelago-esempio-";

    public static bool IsActive => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>
    /// Prepara la cartella dati dell'esempio: i file inventati, i prezzi con l'ultima data portata a «adesso» (altrimenti dopo una settimana
    /// l'esempio segnalerebbe «Prezzi vecchi»), il CSV da aprire e la lingua e il tema di chi lo apre. Restituisce il percorso del CSV.
    /// </summary>
    public static string Prepare(string folder, string? language, string? theme, DateTime nowUtc)
    {
        Directory.CreateDirectory(folder);
        var assembly = typeof(DemoAccount).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var relative = resource[ResourcePrefix.Length..].Replace('\\', '/');
            var target = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var file = File.Create(target);
            stream.CopyTo(file);
        }

        RefreshLastQuotes(Path.Combine(folder, "quotes"), nowUtc.AddMinutes(-30));

        var csv = Path.Combine(folder, CsvName);
        File.WriteAllText(Path.Combine(folder, "lastfile.txt"), csv);

        var prefs = new JsonObject();
        if (!string.IsNullOrEmpty(language))
        {
            prefs["lang"] = language;
        }
        if (!string.IsNullOrEmpty(theme))
        {
            prefs["theme"] = theme;
        }
        File.WriteAllText(Path.Combine(folder, "webprefs.json"), prefs.ToJsonString());
        return csv;
    }

    /// <summary>Apre la seconda finestra con il conto di esempio. Lancia un'eccezione se non riesce ad avviarla.</summary>
    public static void Launch(string? language, string? theme)
    {
        var folder = Path.Combine(Path.GetTempPath(), FolderPrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Prepare(folder, language, theme, DateTime.UtcNow);

        var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Percorso del programma sconosciuto"))
        {
            UseShellExecute = false,
        };
        // la finestra dell'esempio non eredita le variabili delle prove automatiche
        foreach (var name in new[] { "PELAGO_TEST", "PELAGO_AUTO", "PELAGO_CDP_PORT", "PELAGO_TEST_ADD", "PELAGO_TAX_ACCEPTED" })
        {
            start.Environment.Remove(name);
        }
        start.Environment[EnvironmentVariable] = "1";
        start.Environment["PELAGO_DATA"] = folder;
        Process.Start(start)?.Dispose();

        DeleteOldFolders(folder);
    }

    /// <summary>Toglie le cartelle degli esempi di più di un giorno fa (quelle recenti potrebbero essere ancora aperte in un'altra finestra).</summary>
    private static void DeleteOldFolders(string keep)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(Path.GetTempPath(), FolderPrefix + "*"))
            {
                if (string.Equals(dir, keep, StringComparison.OrdinalIgnoreCase) || DateTime.Now - Directory.GetCreationTime(dir) < TimeSpan.FromDays(1))
                {
                    continue;
                }
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (IOException)
                {
                    // ancora in uso: si riproverà la volta dopo
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
    }

    private static void RefreshLastQuotes(string quotesFolder, DateTime when)
    {
        if (!Directory.Exists(quotesFolder))
        {
            return;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        foreach (var path in Directory.EnumerateFiles(quotesFolder, "*.json"))
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonArray { Count: > 0 } quotes && quotes[quotes.Count - 1] is JsonObject last)
            {
                last["Timestamp"] = when.ToString("o");
                File.WriteAllText(path, quotes.ToJsonString(options), new UTF8Encoding(false));
            }
        }
    }
}
