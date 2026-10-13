using System.IO.Compression;
using System.Text.Json.Nodes;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class TaxDisclaimerTests
{
    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";

    private static string Buy(string symbol, string id) =>
        $"\"2026-09-01T10:00:00.000Z\",\"2026-09-01\",\"DEFAULT\",\"TRADING\",\"BUY\",\"STOCK\",\"Titolo {symbol}\",\"{symbol}\",\"10\",\"5\",\"-50\",\"\",\"\",\"EUR\",\"Buy trade {symbol}\",\"{id}\"";

    [Fact]
    public async Task The_tax_warning_waits_for_a_loaded_file_and_stays_gone_once_understood()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pelago-taxwarn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var prefsFile = Path.Combine(DataLocations.Root, "webprefs.json");
        var previous = Environment.GetEnvironmentVariable("PELAGO_TEST");
        Environment.SetEnvironmentVariable("PELAGO_TEST", "1");
        try
        {
            Directory.CreateDirectory(DataLocations.Root);
            File.Delete(prefsFile);
            var csv = Path.Combine(folder, "t.csv");
            File.WriteAllLines(csv, new[] { Header, Buy("US00TEST0001", "t1") });

            using var state = new AppState(new UiPrefs());
            Assert.False(state.TaxDisclaimerPending);                 // senza un file non c'è nulla che si possa usare male

            Assert.True(await state.LoadFileAsync(csv));
            Assert.True(state.TaxDisclaimerPending);                  // la prima volta con dei dati: la finestra compare

            var notified = 0;
            state.Changed += () => notified++;
            state.AcceptTaxDisclaimer();
            Assert.False(state.TaxDisclaimerPending);
            Assert.True(notified > 0);                                // le pagine si aggiornano e la finestra sparisce

            // «Ho capito» si ricorda: una nuova sessione (stesso file di preferenze) non la mostra più
            using var next = new AppState(new UiPrefs());
            Assert.True(await next.LoadFileAsync(csv));
            Assert.False(next.TaxDisclaimerPending);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PELAGO_TEST", previous);
            File.Delete(prefsFile);
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_example_inherits_the_acceptance_of_the_warning()
    {
        var accepted = Path.Combine(Path.GetTempPath(), "pelago-demo-tax-" + Guid.NewGuid().ToString("N"));
        var fresh = Path.Combine(Path.GetTempPath(), "pelago-demo-tax-" + Guid.NewGuid().ToString("N"));
        try
        {
            DemoAccount.Prepare(accepted, "it", null, DateTime.UtcNow, taxDisclaimerAccepted: true);
            DemoAccount.Prepare(fresh, "it", null, DateTime.UtcNow);

            Assert.Equal("1", (string?)JsonNode.Parse(File.ReadAllText(Path.Combine(accepted, "webprefs.json")))![AppState.TaxDisclaimerPreference]);
            Assert.Null(JsonNode.Parse(File.ReadAllText(Path.Combine(fresh, "webprefs.json")))![AppState.TaxDisclaimerPreference]);
        }
        finally
        {
            Directory.Delete(accepted, recursive: true);
            Directory.Delete(fresh, recursive: true);
        }
    }

    [Fact]
    public void An_excel_export_can_carry_a_warning_at_the_bottom_without_widening_the_columns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pelago-note-{Guid.NewGuid():N}.xlsx");
        const string note = "Solo a scopo informativo: non è consulenza fiscale né un modello per la dichiarazione dei redditi, e questa riga è molto più lunga di qualunque colonna.";
        try
        {
            XlsxWriter.Write(path, "Prova", new[] { "Titolo", "Importo" }, new[] { ColumnKind.Text, ColumnKind.Money },
                new[] { new object?[] { "A", 10m } }, note);

            var sheet = ReadSheet(path);

            Assert.Contains(note, sheet);
            Assert.Contains("<row r=\"4\">", sheet);                      // riga 1 intestazione, 2 dati, 3 vuota, 4 avvertenza
            Assert.DoesNotContain("<row r=\"3\">", sheet);
            var width = double.Parse(System.Text.RegularExpressions.Regex.Match(sheet, "<col min=\"1\"[^>]*width=\"([^\"]+)\"").Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(width < 20);                                      // la riga lunga non allarga la prima colonna

            // senza avvertenza il foglio è quello di sempre
            XlsxWriter.Write(path, "Prova", new[] { "Titolo", "Importo" }, new[] { ColumnKind.Text, ColumnKind.Money }, new[] { new object?[] { "A", 10m } });
            Assert.DoesNotContain("<row r=\"4\">", ReadSheet(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string ReadSheet(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        return reader.ReadToEnd();
    }
}
