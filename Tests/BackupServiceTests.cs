using System.IO.Compression;
using System.Runtime.CompilerServices;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

/// <summary>Sposta la cartella dati dell'app in una cartella temporanea PRIMA che qualunque test la usi: i test non toccano mai i dati veri.</summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    public static void Init() =>
        Environment.SetEnvironmentVariable("PELAGO_DATA", Path.Combine(Path.GetTempPath(), "pelago-tests-" + Guid.NewGuid().ToString("N")));
}

[Collection("AppState")]   // scrive e comprime la cartella dati, la stessa che usano gli altri test: uno alla volta
public class BackupServiceTests
{
    private static string NewFolder() => Path.Combine(Path.GetTempPath(), "pelago-backup-test-" + Guid.NewGuid().ToString("N"));

    private static void SeedDataFolder()
    {
        Directory.CreateDirectory(Path.Combine(DataLocations.Root, "quotes"));
        File.WriteAllText(Path.Combine(DataLocations.Root, "catalog.json"), "[]");
        File.WriteAllText(Path.Combine(DataLocations.Root, "quotes", "AAA.json"), "[]");
    }

    [Fact]
    public void Creates_a_zip_when_due_and_not_again_before_a_week()
    {
        SeedDataFolder();
        var prefs = new UiPrefs();
        prefs.Set("backup.last", "");
        var folder = NewFolder();
        var now = new DateTime(2026, 10, 9, 10, 0, 0);

        var first = BackupService.RunIfDue(prefs, folder, now);
        var tooSoon = BackupService.RunIfDue(prefs, folder, now.AddDays(3));
        var due = BackupService.RunIfDue(prefs, folder, now.AddDays(8));

        Assert.NotNull(first);
        Assert.Null(tooSoon);
        Assert.NotNull(due);

        using (var zip = ZipFile.OpenRead(first!))
        {
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("catalog.json", names);
            Assert.Contains("quotes/AAA.json", names);
        }

        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void Keeps_only_the_latest_backups()
    {
        SeedDataFolder();
        var prefs = new UiPrefs();
        prefs.Set("backup.last", "");
        var folder = NewFolder();
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        for (var i = 0; i < BackupService.KeepLast + 3; i++)
        {
            BackupService.RunIfDue(prefs, folder, now.AddDays(i * 8));
        }

        Assert.Equal(BackupService.KeepLast, new DirectoryInfo(folder).GetFiles("pelago-backup-*.zip").Length);
        Directory.Delete(folder, recursive: true);
    }
}
