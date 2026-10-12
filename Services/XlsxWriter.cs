using System.IO.Compression;
using System.Text;

namespace DPOper.Services;

/// <summary>Come formattare una colonna in Excel.</summary>
public enum ColumnKind { Text, Number, Money, Percent, Date }

/// <summary>
/// Scrive un file Excel (.xlsx) con un solo foglio, senza librerie esterne: un .xlsx è un archivio zip di piccoli file XML.
/// Numeri, importi, percentuali e date vengono scritti come veri valori (non come testo), così si possono sommare e ordinare in Excel.
/// </summary>
public static class XlsxWriter
{
    // Indici degli stili in styles.xml
    private const int StyleDateTime = 1, StyleMoney = 2, StylePercent = 3, StyleDate = 4, StyleHeader = 5;

    public static void Write(string path, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<ColumnKind> kinds, IEnumerable<object?[]> rows)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
        Add(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(zip, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + Escape(SafeSheetName(sheetName)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(zip, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
        Add(zip, "xl/styles.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"4\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\\ hh:mm\"/><numFmt numFmtId=\"165\" formatCode=\"dd/mm/yyyy\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"#,##0.00\"/><numFmt numFmtId=\"167\" formatCode=\"0.00%\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"167\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs></styleSheet>");

        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sheet.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        var data = rows as IList<object?[]> ?? rows.ToList();
        sheet.Append("<cols>");
        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Append("<col min=\"").Append(c + 1).Append("\" max=\"").Append(c + 1).Append("\" width=\"")
                .Append(ColumnWidth(c, headers[c], data)).Append("\" customWidth=\"1\"/>");
        }
        sheet.Append("</cols><sheetData>");

        sheet.Append("<row r=\"1\">");
        for (var c = 0; c < headers.Count; c++)
        {
            sheet.Append(StringCell(c, 1, headers[c], StyleHeader));
        }
        sheet.Append("</row>");

        var r = 2;
        foreach (var row in data)
        {
            sheet.Append("<row r=\"").Append(r).Append("\">");
            for (var c = 0; c < row.Length && c < headers.Count; c++)
            {
                sheet.Append(Cell(c, r, row[c], kinds[c]));
            }
            sheet.Append("</row>");
            r++;
        }
        sheet.Append("</sheetData></worksheet>");
        Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    /// <summary>Larghezza di colonna (in caratteri) che basta per l'intestazione e per i valori più lunghi: date e numeri hanno un minimo fisso, altrimenti Excel mostra "###".</summary>
    private static string ColumnWidth(int column, string header, IList<object?[]> data)
    {
        var width = header.Length + 4;
        foreach (var row in data)
        {
            if (column >= row.Length || row[column] is null)
            {
                continue;
            }

            var length = row[column] switch
            {
                DateTime => 18,
                DateOnly => 12,
                decimal or double or float or int or long or short => 16,
                var other => (other?.ToString() ?? "").Length + 2,
            };
            width = Math.Max(width, length);
        }

        return Math.Clamp(width, 12, 60).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Cell(int column, int row, object? value, ColumnKind kind)
    {
        switch (value)
        {
            case null:
                return "";
            case DateTime dt:
                return NumberCell(column, row, (dt - new DateTime(1899, 12, 30)).TotalDays, StyleDateTime);
            case DateOnly d:
                return NumberCell(column, row, d.DayNumber - new DateOnly(1899, 12, 30).DayNumber, StyleDate);
            case decimal or double or float or int or long or short:
                var number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    return "";
                }
                return NumberCell(column, row, number, kind switch { ColumnKind.Money => StyleMoney, ColumnKind.Percent => StylePercent, _ => 0 });
            default:
                return StringCell(column, row, value.ToString() ?? "", 0);
        }
    }

    private static string NumberCell(int column, int row, double value, int style) =>
        $"<c r=\"{Ref(column, row)}\" s=\"{style}\"><v>{value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}</v></c>";

    private static string StringCell(int column, int row, string text, int style) =>
        $"<c r=\"{Ref(column, row)}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>";

    /// <summary>Riferimento di cella in stile Excel: colonna 0 → "A", 27 → "AB".</summary>
    internal static string Ref(int column, int row)
    {
        var letters = "";
        for (var n = column; n >= 0; n = n / 26 - 1)
        {
            letters = (char)('A' + n % 26) + letters;
        }
        return letters + row;
    }

    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        return cleaned.Length == 0 ? "Foglio1" : cleaned[..Math.Min(31, cleaned.Length)];
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch < 0x20 && ch is not ('\t' or '\n' or '\r'))
            {
                continue; // caratteri non ammessi in XML
            }
            sb.Append(ch switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;", _ => ch.ToString() });
        }
        return sb.ToString();
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
