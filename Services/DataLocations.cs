using System.IO.Compression;

namespace DPOper.Services;

/// <summary>Dove l'app tiene i propri file, e come elencarli o salvarne un backup.</summary>
public static class DataLocations
{
    /// <summary>
    /// La cartella dati. Di solito <c>%LocalAppData%\DPOper</c>; la variabile d'ambiente <c>DPOPER_DATA</c> la sposta
    /// altrove (serve per le prove e per le demo con dati di esempio, senza toccare i dati veri).
    /// </summary>
    public static readonly string Root = Environment.GetEnvironmentVariable("DPOPER_DATA") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DPOper");

    public static string QuotesFolder => Path.Combine(Root, "quotes");

    public sealed record FileRow(string Name, string Description, long Size, DateTime? Modified, string Path, bool IsFolder);

    /// <summary>I file e le cartelle principali dentro la cartella dati (solo quelli che esistono).</summary>
    public static List<FileRow> TopLevel()
    {
        var items = new (string Name, bool Folder)[]
        {
            ("quotes", true), ("catalog.json", false), ("btp.json", false), ("lastfile.txt", false),
            ("webprefs.json", false), ("uisettings.json", false), ("www", true), ("webview2", true),
        };

        var rows = new List<FileRow>();
        foreach (var (name, folder) in items)
        {
            var path = Path.Combine(Root, name);
            if (folder && Directory.Exists(path))
            {
                var files = new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                rows.Add(new FileRow(name + "\\", Describe(name), files.Sum(f => f.Length),
                    files.Count == 0 ? null : files.Max(f => f.LastWriteTime), path, true));
            }
            else if (!folder && File.Exists(path))
            {
                var info = new FileInfo(path);
                rows.Add(new FileRow(name, Describe(name), info.Length, info.LastWriteTime, path, false));
            }
        }
        return rows;
    }

    private static string Describe(string name)
    {
        var entry = HelpTexts.Find("file:" + name);
        return entry?.Short ?? "";
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };

    /// <summary>Crea uno zip con quotazioni, catalogo, BTP e preferenze (esclude i file tecnici www e webview2).</summary>
    public static int CreateBackup(string zipPath)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var count = 0;
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.TopDirectoryOnly))
        {
            zip.CreateEntryFromFile(file, Path.GetFileName(file));
            count++;
        }

        if (Directory.Exists(QuotesFolder))
        {
            foreach (var file in Directory.EnumerateFiles(QuotesFolder, "*", SearchOption.AllDirectories))
            {
                zip.CreateEntryFromFile(file, "quotes/" + Path.GetRelativePath(QuotesFolder, file).Replace('\\', '/'));
                count++;
            }
        }
        return count;
    }
}
