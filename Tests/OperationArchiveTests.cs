using DPOper.Services;
using Xunit;

namespace Pelago.Tests;

public class OperationArchiveTests : IDisposable
{
    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pelago-archive-" + Guid.NewGuid().ToString("N"));

    public OperationArchiveTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, params string[] rows)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllLines(path, new[] { Header }.Concat(rows));
        return path;
    }

    private static string Row(string date, string type, string amount, string id, string description = "x", string time = "10:00:00") =>
        $"\"{date}T{time}.000Z\",\"{date}\",\"DEFAULT\",\"CASH\",\"{type}\",\"\",\"\",\"\",\"\",\"\",\"{amount}\",\"\",\"\",\"EUR\",\"{description}\",\"{id}\"";

    private string Archive => Path.Combine(_folder, "operazioni.csv");

    [Fact]
    public void Creates_the_archive_from_the_open_file_plus_the_new_one()
    {
        var seed = Write("tutto.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"), Row("2026-08-20", "INTEREST_PAYMENT", "5.00", "a2"));
        var incoming = Write("settembre.csv", Row("2026-09-02", "INTEREST_PAYMENT", "6.00", "b1"));

        var report = OperationArchive.Merge(Archive, seed, incoming);

        Assert.True(report.CreatedArchive);
        Assert.Equal(1, report.Added);
        Assert.Equal(3, report.TotalAfter);
        Assert.Equal(4, File.ReadAllLines(Archive).Length);               // intestazione + 3 righe
        Assert.False(report.Overlaps);
        Assert.Equal(13, report.GapDays);                                  // dal 20/08 al 02/09: probabilmente manca qualcosa
    }

    [Fact]
    public void Importing_overlapping_files_adds_only_the_new_rows()
    {
        var first = Write("a.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"), Row("2026-08-20", "INTEREST_PAYMENT", "5.00", "a2"));
        OperationArchive.Merge(Archive, first, first);
        var second = Write("b.csv", Row("2026-08-20", "INTEREST_PAYMENT", "5.00", "a2"), Row("2026-09-02", "INTEREST_PAYMENT", "6.00", "b1"));

        var report = OperationArchive.Merge(Archive, null, second);

        Assert.False(report.CreatedArchive);
        Assert.Equal(1, report.Added);
        Assert.Equal(1, report.AlreadyPresent);
        Assert.Equal(0, report.Conflicts);
        Assert.True(report.Overlaps);
        Assert.Equal(3, report.TotalAfter);
    }

    [Fact]
    public void Adding_the_same_file_twice_changes_nothing()
    {
        var file = Write("a.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"));
        OperationArchive.Merge(Archive, file, file);
        var before = File.ReadAllText(Archive);

        var report = OperationArchive.Merge(Archive, null, file);

        Assert.Equal(0, report.Added);
        Assert.Equal(1, report.AlreadyPresent);
        Assert.Equal(before, File.ReadAllText(Archive));
        Assert.False(File.Exists(Path.Combine(_folder, "operazioni.prev.bak")));   // nessuna scrittura, nessuna copia
    }

    [Fact]
    public void A_known_id_with_different_content_is_a_conflict_and_the_old_row_stays()
    {
        var first = Write("a.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"));
        OperationArchive.Merge(Archive, first, first);
        var corrected = Write("b.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1500.00", "a1"));

        var report = OperationArchive.Merge(Archive, null, corrected);

        Assert.Equal(1, report.Conflicts);
        Assert.Equal(0, report.Added);
        Assert.Contains("1000.00", File.ReadAllText(Archive));
        Assert.DoesNotContain("1500.00", File.ReadAllText(Archive));
        Assert.Single(report.ConflictExamples);
    }

    [Fact]
    public void Rows_without_an_id_are_matched_by_content_and_repeats_are_counted()
    {
        var first = Write("a.csv", Row("2026-08-01", "INTEREST_PAYMENT", "5.00", ""), Row("2026-08-01", "INTEREST_PAYMENT", "5.00", ""));
        OperationArchive.Merge(Archive, first, first);
        // il file nuovo ha tre righe identiche: due sono già nello storico, una è nuova
        var second = Write("b.csv", Row("2026-08-01", "INTEREST_PAYMENT", "5.00", ""), Row("2026-08-01", "INTEREST_PAYMENT", "5.00", ""), Row("2026-08-01", "INTEREST_PAYMENT", "5.00", ""));

        var report = OperationArchive.Merge(Archive, null, second);

        Assert.Equal(1, report.Added);
        Assert.Equal(2, report.AlreadyPresent);
        Assert.Equal(3, report.TotalAfter);
    }

    [Fact]
    public void Merged_rows_are_in_chronological_order_and_a_backup_is_kept()
    {
        var first = Write("a.csv", Row("2026-09-02", "INTEREST_PAYMENT", "6.00", "b1"));
        OperationArchive.Merge(Archive, first, first);
        var older = Write("b.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"));

        OperationArchive.Merge(Archive, null, older);

        var lines = File.ReadAllLines(Archive);
        Assert.Contains("2026-08-01", lines[1]);
        Assert.Contains("2026-09-02", lines[2]);
        Assert.True(File.Exists(Path.Combine(_folder, "operazioni.prev.bak")));
    }

    [Fact]
    public void A_file_with_columns_in_another_order_is_realigned()
    {
        var first = Write("a.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1"));
        OperationArchive.Merge(Archive, first, first);
        var swapped = Path.Combine(_folder, "ordine.csv");
        File.WriteAllLines(swapped, new[]
        {
            "\"transaction_id\",\"amount\",\"type\",\"category\",\"date\"",
            "\"z9\",\"7.00\",\"INTEREST_PAYMENT\",\"CASH\",\"2026-09-03\"",
        });

        var report = OperationArchive.Merge(Archive, null, swapped);

        Assert.Equal(1, report.Added);
        var operations = CsvOperationReader.Read(Archive);
        Assert.Equal(2, operations.Count);
        Assert.Equal(7m, operations[1].Amount);
        Assert.Equal("z9", operations[1].TransactionId);
    }

    [Fact]
    public void A_file_that_is_not_an_export_is_rejected()
    {
        var bad = Path.Combine(_folder, "bad.csv");
        File.WriteAllLines(bad, new[] { "a,b,c", "1,2,3" });

        var ex = Assert.Throws<InvalidDataException>(() => OperationArchive.Merge(Archive, null, bad));

        Assert.Contains("date", ex.Message);
    }

    [Fact]
    public void The_archive_is_a_valid_export_the_reader_understands()
    {
        var file = Write("a.csv", Row("2026-08-01", "TRANSFER_INSTANT_INBOUND", "1000.00", "a1", "Incoming, with comma \"\"quoted\"\""));
        OperationArchive.Merge(Archive, file, file);

        var operations = CsvOperationReader.Read(Archive);

        var op = Assert.Single(operations);
        Assert.Equal("Incoming, with comma \"quoted\"", op.Description);
        Assert.Equal(1000m, op.Amount);
    }
}
