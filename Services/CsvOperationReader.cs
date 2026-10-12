using System.Globalization;
using System.Text;
using DPOper.Models;
using static DPOper.Services.Loc;

namespace DPOper.Services;

/// <summary>
/// Legge il file CSV di export delle operazioni del conto (formato con intestazione,
/// campi tra virgolette e separatore virgola).
/// </summary>
public static class CsvOperationReader
{

    /// <summary>True se la prima riga del file ha le colonne di un export delle operazioni (per riconoscere un nuovo file nella cartella).</summary>
    public static bool LooksLikeExport(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var first = reader.ReadLine();
            if (first is null)
            {
                return false;
            }
            var columns = ParseLine(first).Select(c => c.Trim().ToLowerInvariant()).ToHashSet();
            return new[] { "date", "category", "type", "amount" }.All(columns.Contains);
        }
        catch (IOException)
        {
            return false;
        }
    }
    public static List<Operation> Read(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var headerLine = reader.ReadLine()
            ?? throw new InvalidDataException(T("Il file è vuoto."));
        var header = ParseLine(headerLine);

        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            columnIndex[header[i].Trim()] = i;
        }

        foreach (var required in new[] { "date", "category", "type", "amount" })
        {
            if (!columnIndex.ContainsKey(required))
            {
                throw new InvalidDataException(T("Colonna attesa mancante nel CSV: \"{0}\".", required));
            }
        }

        var operations = new List<Operation>();
        string? line;
        var lineNumber = 1;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseLine(line);
            operations.Add(ToOperation(fields, columnIndex, lineNumber));
        }

        return operations;
    }

    private static Operation ToOperation(List<string> fields, Dictionary<string, int> columnIndex, int lineNumber)
    {
        string Get(string column) =>
            columnIndex.TryGetValue(column, out var idx) && idx < fields.Count ? fields[idx].Trim() : "";

        var dateTimeText = Get("datetime");
        var dateText = Get("date");

        if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException(T("Data non valida alla riga {0}: \"{1}\".", lineNumber, dateText));
        }

        var dateTime = DateTime.TryParse(dateTimeText, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedDateTime)
            ? parsedDateTime
            : date.ToDateTime(TimeOnly.MinValue);

        return new Operation
        {
            DateTime = dateTime,
            Date = date,
            AccountType = Get("account_type"),
            Category = Get("category"),
            Type = Get("type"),
            AssetClass = Get("asset_class"),
            Name = Get("name"),
            Symbol = Get("symbol"),
            Shares = ParseDecimal(Get("shares")),
            Price = ParseDecimal(Get("price")),
            Amount = ParseDecimal(Get("amount")),
            Fee = ParseDecimal(Get("fee")),
            Tax = ParseDecimal(Get("tax")),
            Currency = Get("currency"),
            Description = Get("description"),
            TransactionId = Get("transaction_id"),
            CounterpartyName = Get("counterparty_name"),
            CounterpartyIban = Get("counterparty_iban"),
            PaymentReference = Get("payment_reference"),
        };
    }

    private static decimal? ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Parser CSV minimale ma robusto: gestisce campi tra virgolette, virgole al loro interno
    /// e virgolette raddoppiate ("") come escape di una virgoletta letterale.
    /// </summary>
    internal static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
