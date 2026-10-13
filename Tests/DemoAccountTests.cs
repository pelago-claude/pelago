using System.Text.Json.Nodes;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class DemoAccountTests
{
    private static string NewFolder() => Path.Combine(Path.GetTempPath(), "pelago-demo-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void The_example_is_extracted_with_fresh_prices_and_the_language_of_who_opens_it()
    {
        var folder = NewFolder();
        try
        {
            var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
            var csv = DemoAccount.Prepare(folder, "en", "dark", now);

            Assert.Equal(Path.Combine(folder, "esempio-operazioni.csv"), csv);
            Assert.True(File.Exists(csv));
            Assert.Equal(csv, File.ReadAllText(Path.Combine(folder, "lastfile.txt")));       // la finestra dell'esempio apre da sola questo file
            Assert.True(File.Exists(Path.Combine(folder, "catalog.json")));
            Assert.True(File.Exists(Path.Combine(folder, "nomi-titoli.json")));
            Assert.True(File.Exists(Path.Combine(folder, "btp.json")));

            var prefs = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "webprefs.json")))!;
            Assert.Equal("en", (string?)prefs["lang"]);
            Assert.Equal("dark", (string?)prefs["theme"]);

            // l'ultimo prezzo di ogni titolo porta la data di adesso (meno mezz'ora): altrimenti dopo una settimana l'esempio direbbe «Prezzi vecchi»
            var quoteFiles = Directory.GetFiles(Path.Combine(folder, "quotes"), "*.json");
            Assert.True(quoteFiles.Length >= 20);
            foreach (var file in quoteFiles)
            {
                var quotes = (JsonArray)JsonNode.Parse(File.ReadAllText(file))!;
                var last = DateTime.Parse((string)quotes[quotes.Count - 1]!["Timestamp"]!, null, System.Globalization.DateTimeStyles.RoundtripKind);
                Assert.Equal(now.AddMinutes(-30), last);
                Assert.True(quotes.Count < 2 || DateTime.Parse((string)quotes[0]!["Timestamp"]!, null, System.Globalization.DateTimeStyles.RoundtripKind) < now.AddMonths(-6));   // lo storico resta quello di prima
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task The_example_loads_in_the_app_and_has_no_stale_price_warning()
    {
        var folder = NewFolder();
        var root = DataLocations.Root;
        var copied = new List<string>();
        var previous = Environment.GetEnvironmentVariable("PELAGO_TEST");
        Environment.SetEnvironmentVariable("PELAGO_TEST", "1");
        try
        {
            var csv = DemoAccount.Prepare(folder, "it", null, DateTime.UtcNow);
            Directory.CreateDirectory(root);
            foreach (var name in new[] { "catalog.json", "nomi-titoli.json", "nomi-emittenti.json", "btp.json" })
            {
                File.Copy(Path.Combine(folder, name), Path.Combine(root, name), overwrite: true);
                copied.Add(Path.Combine(root, name));
            }
            var quotes = Path.Combine(root, "quotes");
            Directory.CreateDirectory(quotes);
            foreach (var file in Directory.GetFiles(Path.Combine(folder, "quotes")))
            {
                File.Copy(file, Path.Combine(quotes, Path.GetFileName(file)), overwrite: true);
                copied.Add(Path.Combine(quotes, Path.GetFileName(file)));
            }

            using var state = new AppState(new UiPrefs());
            Assert.True(await state.LoadFileAsync(csv));

            Assert.True(state.HasData);
            Assert.True(state.Operations!.Count >= 150);
            Assert.DoesNotContain(state.Findings, f => f.Code == "stale" && f.Level == FindingLevel.Warning);
            Assert.True(state.Snapshots.Count > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PELAGO_TEST", previous);
            foreach (var file in copied)
            {
                File.Delete(file);
            }
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_example_window_is_recognised_from_its_environment_variable()
    {
        var previous = Environment.GetEnvironmentVariable(DemoAccount.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(DemoAccount.EnvironmentVariable, null);
            Assert.False(DemoAccount.IsActive);

            Environment.SetEnvironmentVariable(DemoAccount.EnvironmentVariable, "1");
            Assert.True(DemoAccount.IsActive);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DemoAccount.EnvironmentVariable, previous);
        }
    }
}
