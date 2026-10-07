using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Libros de Excel de prueba, armados parte por parte como los escribe Excel: el ZIP, las
/// relaciones, el libro, las hojas, los textos compartidos y los estilos.
/// </summary>
internal static class TestWorkbooks
{
    public const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string StrictMainNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    public const string StrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";

    /// <summary>Estilo 1: fecha (formato integrado 14). Estilo 2: fecha y hora propia. Estilo 3: "0.00".</summary>
    public const int DateStyle = 1;
    public const int DateTimeStyle = 2;
    public const int DecimalStyle = 3;

    /// <summary>Una hoja: su nombre, sus filas y si está oculta.</summary>
    public sealed record Sheet(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows, bool Hidden = false);

    /// <summary>Una celda escrita tal cual, para los casos que Excel escribe a mano.</summary>
    public sealed record Raw(string Xml);

    /// <summary>Un número con un estilo.</summary>
    public sealed record Styled(double Value, int Style);

    /// <summary>Un libro con una hoja.</summary>
    public static byte[] Single(params object?[][] rows) => Build([new Sheet("Sheet1", rows)]);

    /// <summary>
    /// Un libro con estas hojas. Las celdas: texto (compartido), números, booleanos, fechas
    /// (con estilo de fecha), <see cref="Styled"/>, <see cref="Raw"/>; null deja la celda fuera.
    /// </summary>
    public static byte[] Build(IReadOnlyList<Sheet> sheets, bool strict = false, bool date1904 = false)
    {
        var main = strict ? StrictMainNamespace : MainNamespace;
        var rels = strict ? StrictRelationshipsNamespace : RelationshipsNamespace;
        var relType = strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships" : "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var prefix = strict ? "x:" : string.Empty;
        var xmlns = strict ? $"xmlns:x=\"{main}\"" : $"xmlns=\"{main}\"";

        var shared = new List<string>();
        var parts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[Content_Types].xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>",
            ["_rels/.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + $"<Relationship Id=\"rId1\" Type=\"{relType}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>",
        };

        var workbook = new StringBuilder();
        workbook.Append(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><{prefix}workbook {xmlns} xmlns:r=\"{rels}\">");
        workbook.Append(date1904 ? $"<{prefix}workbookPr date1904=\"1\"/>" : $"<{prefix}workbookPr/>");
        workbook.Append(CultureInfo.InvariantCulture, $"<{prefix}sheets>");

        var workbookRels = new StringBuilder(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

        for (var i = 0; i < sheets.Count; i++)
        {
            var sheet = sheets[i];
            var state = sheet.Hidden ? " state=\"hidden\"" : string.Empty;
            workbook.Append(CultureInfo.InvariantCulture, $"<{prefix}sheet name=\"{Escape(sheet.Name)}\" sheetId=\"{i + 1}\"{state} r:id=\"rIdSheet{i + 1}\"/>");
            workbookRels.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rIdSheet{i + 1}\" Type=\"{relType}/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            parts[$"xl/worksheets/sheet{i + 1}.xml"] = SheetXml(sheet, shared, prefix, xmlns);
        }

        workbook.Append(CultureInfo.InvariantCulture, $"</{prefix}sheets></{prefix}workbook>");
        workbookRels.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rIdStrings\" Type=\"{relType}/sharedStrings\" Target=\"sharedStrings.xml\"/>");
        workbookRels.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rIdStyles\" Type=\"{relType}/styles\" Target=\"/xl/styles.xml\"/>");
        workbookRels.Append("</Relationships>");

        parts["xl/workbook.xml"] = workbook.ToString();
        parts["xl/_rels/workbook.xml.rels"] = workbookRels.ToString();
        parts["xl/sharedStrings.xml"] =
            $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><{prefix}sst {xmlns} count=\"{shared.Count}\" uniqueCount=\"{shared.Count}\">"
            + string.Concat(shared.Select(s => $"<{prefix}si><{prefix}t xml:space=\"preserve\">{Escape(s)}</{prefix}t></{prefix}si>"))
            + $"</{prefix}sst>";
        parts["xl/styles.xml"] =
            $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><{prefix}styleSheet {xmlns}>"
            + $"<{prefix}numFmts count=\"1\"><{prefix}numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\\ hh:mm\"/></{prefix}numFmts>"
            + $"<{prefix}cellStyleXfs count=\"1\"><{prefix}xf numFmtId=\"14\"/></{prefix}cellStyleXfs>"
            + $"<{prefix}cellXfs count=\"4\"><{prefix}xf numFmtId=\"0\"/><{prefix}xf numFmtId=\"14\" applyNumberFormat=\"1\"/>"
            + $"<{prefix}xf numFmtId=\"164\" applyNumberFormat=\"1\"/><{prefix}xf numFmtId=\"2\" applyNumberFormat=\"1\"/></{prefix}cellXfs>"
            + $"</{prefix}styleSheet>";

        return Zip(parts);
    }

    /// <summary>Un ZIP con estas partes, comprimidas como las comprime Excel.</summary>
    public static byte[] Zip(IReadOnlyDictionary<string, string> parts)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Las partes de un libro, para reemplazar alguna con un caso fabricado.</summary>
    public static Dictionary<string, string> PartsOf(byte[] workbook)
    {
        using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            parts[entry.FullName] = reader.ReadToEnd();
        }

        return parts;
    }

    /// <summary>La letra de una columna: 0 es la A.</summary>
    public static string ColumnName(int index)
    {
        var name = string.Empty;
        index++;

        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }

        return name;
    }

    private static string SheetXml(Sheet sheet, List<string> shared, string prefix, string xmlns)
    {
        var xml = new StringBuilder();
        xml.Append(CultureInfo.InvariantCulture, $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><{prefix}worksheet {xmlns}><{prefix}sheetData>");

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];

            if (row.Count == 0)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<{prefix}row r=\"{r + 1}\"/>");
                continue;
            }

            xml.Append(CultureInfo.InvariantCulture, $"<{prefix}row r=\"{r + 1}\">");

            for (var c = 0; c < row.Count; c++)
            {
                var reference = ColumnName(c) + (r + 1).ToString(CultureInfo.InvariantCulture);
                xml.Append(Cell(row[c], reference, shared, prefix));
            }

            xml.Append(CultureInfo.InvariantCulture, $"</{prefix}row>");
        }

        xml.Append(CultureInfo.InvariantCulture, $"</{prefix}sheetData></{prefix}worksheet>");
        return xml.ToString();
    }

    private static string Cell(object? value, string reference, List<string> shared, string prefix) => value switch
    {
        null => string.Empty,
        Raw raw => raw.Xml,
        string text => $"<{prefix}c r=\"{reference}\" t=\"s\"><{prefix}v>{SharedIndex(shared, text)}</{prefix}v></{prefix}c>",
        bool flag => $"<{prefix}c r=\"{reference}\" t=\"b\"><{prefix}v>{(flag ? 1 : 0)}</{prefix}v></{prefix}c>",
        DateTime date => $"<{prefix}c r=\"{reference}\" s=\"{(date.TimeOfDay == TimeSpan.Zero ? DateStyle : DateTimeStyle)}\"><{prefix}v>{Serial(date)}</{prefix}v></{prefix}c>",
        Styled styled => $"<{prefix}c r=\"{reference}\" s=\"{styled.Style}\"><{prefix}v>{styled.Value.ToString("R", CultureInfo.InvariantCulture)}</{prefix}v></{prefix}c>",
        IFormattable number => $"<{prefix}c r=\"{reference}\"><{prefix}v>{number.ToString(null, CultureInfo.InvariantCulture)}</{prefix}v></{prefix}c>",
        _ => throw new ArgumentException($"Unsupported cell: {value.GetType()}"),
    };

    private static int SharedIndex(List<string> shared, string text)
    {
        var index = shared.IndexOf(text);

        if (index >= 0)
        {
            return index;
        }

        shared.Add(text);
        return shared.Count - 1;
    }

    private static string Serial(DateTime date)
        => (date - new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified)).TotalDays.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
