namespace DPOper.Services;

/// <summary>
/// Backup automatico dei dati dell'app (quotazioni, catalogo, BTP, preferenze) in uno zip: una copia ogni 7 giorni, tenendo
/// le ultime 8. La cartella di destinazione si sceglie nella pagina «Dati e file». Il file CSV dell'utente non viene toccato.
/// </summary>
public static class BackupService
{
    public const int EveryDays = 7;
    public const int KeepLast = 8;
    private const string LastKey = "backup.last";

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pelago backup");

    /// <summary>Se è passata una settimana dall'ultimo backup ne crea uno e restituisce il nome del file; altrimenti null.</summary>
    public static string? RunIfDue(UiPrefs prefs, string folder, DateTime now)
    {
        if (DateTime.TryParse(prefs.Get(LastKey), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var last) && (now - last).TotalDays is >= 0 and < EveryDays)
        {
            return null;
        }

        if (!Directory.Exists(DataLocations.Root))
        {
            return null;
        }

        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"pelago-backup-{now:yyyyMMdd-HHmm}.zip");
        DataLocations.CreateBackup(target);
        prefs.Set(LastKey, now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        Prune(folder);
        return target;
    }

    private static void Prune(string folder)
    {
        var old = new DirectoryInfo(folder).GetFiles("pelago-backup-*.zip").OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeepLast);
        foreach (var file in old)
        {
            file.Delete();
        }
    }
}
