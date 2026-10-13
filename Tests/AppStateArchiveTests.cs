using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class AppStateArchiveTests : IDisposable
{
    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pelago-appstate-" + Guid.NewGuid().ToString("N"));
    private readonly string? _previousTestMode = Environment.GetEnvironmentVariable("PELAGO_TEST");

    public AppStateArchiveTests()
    {
        Directory.CreateDirectory(_folder);
        Environment.SetEnvironmentVariable("PELAGO_TEST", "1");   // niente attività in rete durante la prova
        if (File.Exists(OperationArchive.ArchivePath))
        {
            File.Delete(OperationArchive.ArchivePath);
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PELAGO_TEST", _previousTestMode);
        Directory.Delete(_folder, recursive: true);
        if (File.Exists(OperationArchive.ArchivePath))
        {
            File.Delete(OperationArchive.ArchivePath);
        }
    }

    private static string Row(string date, string amount, string id) =>
        $"\"{date}T10:00:00.000Z\",\"{date}\",\"DEFAULT\",\"CASH\",\"TRANSFER_INSTANT_INBOUND\",\"\",\"\",\"\",\"\",\"\",\"{amount}\",\"\",\"\",\"EUR\",\"deposito\",\"{id}\"";

    private string Write(string name, params string[] rows)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllLines(path, new[] { Header }.Concat(rows));
        return path;
    }

    [Fact]
    public async Task Adding_a_newer_file_extends_the_loaded_data_without_duplicates()
    {
        var old = Write("vecchio.csv", Row("2026-07-01", "1000.00", "a1"), Row("2026-07-10", "500.00", "a2"), Row("2026-07-20", "250.00", "a3"));
        var newer = Write("nuovo.csv", Row("2026-07-20", "250.00", "a3"), Row("2026-08-05", "100.00", "b1"), Row("2026-08-15", "50.00", "b2"));
        using var state = new AppState(new UiPrefs());
        Assert.True(await state.LoadFileAsync(old));
        Assert.Equal(3, state.Operations!.Count);
        Assert.False(state.IsArchive);

        Assert.True(await state.AddMovementsAsync(newer));

        Assert.True(state.IsArchive);
        Assert.Equal(5, state.Operations!.Count);
        var report = Assert.IsType<MergeReport>(state.LastMerge);
        Assert.True(report.CreatedArchive);
        Assert.Equal(2, report.Added);
        Assert.Equal(1, report.AlreadyPresent);
        Assert.True(report.Overlaps);

        // aggiungere di nuovo lo stesso file non cambia nulla
        Assert.True(await state.AddMovementsAsync(newer));
        Assert.Equal(5, state.Operations!.Count);
        Assert.Equal(0, state.LastMerge!.Added);
        Assert.False(state.LastMerge.CreatedArchive);

        state.DismissMerge();
        Assert.Null(state.LastMerge);
    }

    [Fact]
    public async Task A_file_that_is_not_an_export_leaves_everything_as_it_was()
    {
        var old = Write("vecchio.csv", Row("2026-07-01", "1000.00", "a1"));
        var bad = Path.Combine(_folder, "bad.csv");
        File.WriteAllLines(bad, new[] { "a,b", "1,2" });
        using var state = new AppState(new UiPrefs());
        await state.LoadFileAsync(old);

        Assert.False(await state.AddMovementsAsync(bad));

        Assert.False(state.IsArchive);
        Assert.Single(state.Operations!);
        Assert.False(File.Exists(OperationArchive.ArchivePath));
    }
}

[CollectionDefinition("AppState", DisableParallelization = true)]
public sealed class AppStateCollection
{
}
