using System.Globalization;
using System.Text;
using static Pelago.Services.Loc;

namespace Pelago.Services;

/// <summary>Cosa è successo aggiungendo un CSV allo storico delle operazioni.</summary>
public sealed record MergeReport(
    bool CreatedArchive,
    int Added,
    int AlreadyPresent,
    int Conflicts,
    int TotalAfter,
    DateTime? ExistingFirst,
    DateTime? ExistingLast,
    DateTime? IncomingFirst,
    DateTime? IncomingLast,
    bool Overlaps,
    int GapDays,
    IReadOnlyList<string> ConflictExamples);

/// <summary>
/// Lo storico delle operazioni dell'app (<c>operazioni.csv</c> nella cartella dati): l'unione di tutti i CSV aggiunti nel tempo, nello stesso
/// formato dell'esportazione del broker. Aggiungere un CSV più recente (anche solo l'ultimo mese) tiene le righe nuove e scarta quelle già presenti:
/// <list type="bullet">
/// <item>le righe con <c>transaction_id</c> si riconoscono da quell'identificativo; se lo stesso id compare con contenuto diverso è un
/// <i>conflitto</i> (si tiene la riga già presente e lo si segnala);</item>
/// <item>le righe senza identificativo si riconoscono da data/tipo/titolo/quantità/importo/descrizione, contando le ripetizioni (due operazioni uguali
/// nello stesso istante restano due).</item>
/// </list>
/// I file scaricati non vengono mai modificati; prima di riscrivere lo storico se ne tiene una copia (<c>operazioni.prev.bak</c>).
/// </summary>
public static class OperationArchive
{
    private static readonly string[] Required = { "date", "category", "type", "amount" };

    // le righe senza transaction_id si riconoscono da questi campi
    private static readonly string[] CompositeColumns = { "datetime", "type", "symbol", "shares", "amount", "description" };

    public static string ArchivePath => Path.Combine(DataLocations.Root, "operazioni.csv");

    public static bool IsArchive(string? path) =>
        path is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(ArchivePath), StringComparison.OrdinalIgnoreCase);

    private sealed class Table
    {
        public List<string> Header { get; init; } = new();
        public List<string[]> Rows { get; init; } = new();
    }

    /// <summary>
    /// Aggiunge <paramref name="incomingPath"/> allo storico in <paramref name="archivePath"/>. Se lo storico non esiste lo crea partendo da
    /// <paramref name="seedPath"/> (il file oggi aperto) quando c'è, poi aggiunge le righe nuove.
    /// </summary>
    public static MergeReport Merge(string archivePath, string? seedPath, string incomingPath)
    {
        var archiveExists = File.Exists(archivePath);
        var existing = archiveExists ? ReadTable(archivePath)
            : seedPath is not null && File.Exists(seedPath) && !string.Equals(Path.GetFullPath(seedPath), Path.GetFullPath(incomingPath), StringComparison.OrdinalIgnoreCase)
                ? ReadTable(seedPath)
                : null;
        var incomingRaw = ReadTable(incomingPath);

        var header = existing?.Header ?? incomingRaw.Header;
        var column = ColumnIndex(header);

        // porta le righe di un file nell'ordine delle colonne dello storico (le colonne mancanti restano vuote); la mappa si calcola una volta per file
        Func<string[], string[]> Aligner(Table table)
        {
            var index = ColumnIndex(table.Header);
            var map = header.Select(h => index.TryGetValue(h.Trim(), out var from) ? from : -1).ToArray();
            return row =>
            {
                var result = new string[map.Length];
                for (var c = 0; c < map.Length; c++)
                {
                    result[c] = map[c] >= 0 && map[c] < row.Length ? row[map[c]] : "";
                }
                return result;
            };
        }

        var existingRows = existing is null ? new List<string[]>() : existing.Rows.Select(Aligner(existing)).ToList();
        var incomingRows = incomingRaw.Rows.Select(Aligner(incomingRaw)).ToList();

        string Get(string[] row, string name) => column.TryGetValue(name, out var i) && i < row.Length ? row[i].Trim() : "";
        string Id(string[] row) => Get(row, "transaction_id");
        string Composite(string[] row) => string.Join("|", CompositeColumns.Select(n => Get(row, n)));

        var byId = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var compositeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in existingRows)
        {
            if (Id(row) is { Length: > 0 } id)
            {
                byId.TryAdd(id, row);
            }
            else
            {
                var key = Composite(row);
                compositeCounts[key] = compositeCounts.GetValueOrDefault(key) + 1;
            }
        }

        var added = new List<string[]>();
        var present = 0;
        var conflicts = 0;
        var examples = new List<string>();
        var compositeSeen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in incomingRows)
        {
            if (Id(row) is { Length: > 0 } id)
            {
                if (byId.TryGetValue(id, out var old))
                {
                    if (old.Zip(row).All(p => p.First.Trim() == p.Second.Trim()))
                    {
                        present++;
                    }
                    else
                    {
                        conflicts++;
                        if (examples.Count < 5)
                        {
                            examples.Add($"{Get(row, "date")} {Get(row, "type")} {Get(row, "name")} {Get(row, "amount")}".Trim());
                        }
                    }
                }
                else
                {
                    added.Add(row);
                    byId[id] = row;
                }
            }
            else
            {
                var key = Composite(row);
                compositeSeen[key] = compositeSeen.GetValueOrDefault(key) + 1;
                if (compositeSeen[key] <= compositeCounts.GetValueOrDefault(key))
                {
                    present++;
                }
                else
                {
                    added.Add(row);
                }
            }
        }

        DateTime When(string[] row) =>
            DateTime.TryParse(Get(row, "datetime"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt
            : DateTime.TryParse(Get(row, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d
            : DateTime.MinValue;

        DateTime? existingFirst = existingRows.Count == 0 ? null : existingRows.Min(When);
        DateTime? existingLast = existingRows.Count == 0 ? null : existingRows.Max(When);
        DateTime? incomingFirst = incomingRows.Count == 0 ? null : incomingRows.Min(When);
        DateTime? incomingLast = incomingRows.Count == 0 ? null : incomingRows.Max(When);

        var overlaps = existingRows.Count > 0 && incomingFirst is { } inFirst && incomingLast is { } inLast
            && inFirst <= existingLast && inLast >= existingFirst;
        var gap = 0;
        if (existingRows.Count > 0 && !overlaps && incomingFirst is { } f && incomingLast is { } l)
        {
            gap = f > existingLast ? (f.Date - existingLast!.Value.Date).Days : (existingFirst!.Value.Date - l.Date).Days;
        }

        var merged = existingRows.Concat(added).Select((row, order) => (row, order)).OrderBy(x => When(x.row)).ThenBy(x => x.order).Select(x => x.row).ToList();

        var created = !archiveExists;
        if (created || added.Count > 0)
        {
            Write(archivePath, header, merged, archiveExists);
        }

        return new MergeReport(created, added.Count, present, conflicts, merged.Count, existingFirst, existingLast, incomingFirst, incomingLast, overlaps, gap, examples);
    }

    /// <summary>Posizione di ogni colonna per nome (senza distinguere le maiuscole); se un nome si ripete vale la prima.</summary>
    private static Dictionary<string, int> ColumnIndex(IReadOnlyList<string> header)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            index.TryAdd(header[i].Trim(), i);
        }
        return index;
    }

    private static Table ReadTable(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var headerLine = reader.ReadLine() ?? throw new InvalidDataException(T("Il file è vuoto."));
        var header = CsvOperationReader.ParseLine(headerLine);
        var names = header.Select(h => h.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in Required)
        {
            if (!names.Contains(required))
            {
                throw new InvalidDataException(T("Colonna attesa mancante nel CSV: \"{0}\".", required));
            }
        }

        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                rows.Add(CsvOperationReader.ParseLine(line).ToArray());
            }
        }
        return new Table { Header = header, Rows = rows };
    }

    private static void Write(string archivePath, List<string> header, List<string[]> rows, bool keepPrevious)
    {
        static string Quote(string field) => "\"" + field.Replace("\"", "\"\"") + "\"";

        var lines = new List<string> { string.Join(',', header.Select(Quote)) };
        lines.AddRange(rows.Select(r => string.Join(',', r.Select(Quote))));

        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        var temp = archivePath + ".tmp";
        File.WriteAllLines(temp, lines, new UTF8Encoding(false));
        if (keepPrevious)
        {
            File.Copy(archivePath, Path.ChangeExtension(archivePath, ".prev.bak"), overwrite: true);
        }
        File.Move(temp, archivePath, overwrite: true);
    }
}
