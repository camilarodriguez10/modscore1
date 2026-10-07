using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Modernization.Domain;
using Modernization.Domain.Sources;

namespace Modernization.Sources.Files;

/// <summary>Qué es un archivo entrante, por sus primeros bytes y no por su extensión.</summary>
public enum IncomingFileKind
{
    /// <summary>Texto: un CSV, o algo que el lector de CSV rechazará con su motivo.</summary>
    Text,

    /// <summary>Un ZIP: un libro de Excel (.xlsx).</summary>
    Workbook,

    /// <summary>Un documento compuesto de Office: un .xls antiguo o un libro protegido con contraseña.</summary>
    Compound,
}

/// <summary>El resultado de convertir un libro.</summary>
/// <param name="SheetName">La hoja que se leyó.</param>
/// <param name="Rows">Filas de datos escritas, sin el encabezado.</param>
/// <param name="Columns">Columnas del encabezado.</param>
public sealed record XlsxConversion(string SheetName, int Rows, int Columns);

/// <summary>
/// Convierte la primera hoja visible de un libro de Excel (.xlsx) en el CSV que lee el resto
/// de la carga: identificar la fuente, el mapping, la validación fila por fila y lo que queda
/// guardado son los mismos para los dos formatos (ADR 0034).
/// </summary>
/// <remarks>
/// Sin dependencias nuevas: el ZIP y el XML los lee el framework. Un .xlsx es un ZIP que
/// llega de afuera, así que cada defensa es explícita:
/// <list type="bullet">
/// <item>el directorio del ZIP se mide antes de abrirlo, para que un archivo con millones de
/// entradas no agote la memoria al listarlas;</item>
/// <item>cada parte se lee con un tope de bytes descomprimidos que no confía en el tamaño que
/// declara el ZIP: una bomba de compresión se corta al pasar el tope;</item>
/// <item>el XML se lee sin DTD ni resolución de entidades externas (XXE);</item>
/// <item>los textos compartidos, las columnas, las filas y el CSV resultante tienen tope;</item>
/// <item>nada se extrae a disco con el nombre que trae el ZIP: solo se leen las partes que el
/// propio libro declara.</item>
/// </list>
/// Las fórmulas no se calculan: se usa el último valor que guardó Excel. Las fechas salen en
/// ISO 8601 y los números en formato invariante.
/// </remarks>
public static class XlsxSourceConverter
{
    /// <summary>Tope de entradas del ZIP. Un libro real tiene unas decenas.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>Tope del XML de la hoja, descomprimido.</summary>
    public const long MaxSheetBytes = 1024L * 1024 * 1024;

    /// <summary>Tope del XML de textos compartidos, descomprimido.</summary>
    public const long MaxSharedStringsBytes = 256L * 1024 * 1024;

    /// <summary>Tope de cualquier otra parte: libro, relaciones y estilos.</summary>
    public const long MaxPartBytes = 16L * 1024 * 1024;

    /// <summary>Caracteres de textos compartidos que se guardan en memoria.</summary>
    public const long MaxSharedStringChars = 32L * 1024 * 1024;

    /// <summary>Columnas del encabezado. Los contratos tienen menos de 30.</summary>
    public const int MaxColumns = 1_000;

    /// <summary>El CSV resultante tiene el mismo tope que un CSV subido.</summary>
    public const long MaxCsvBytes = PublishLimits.MaxFileBytes;

    /// <summary>Los topes de una conversión. Las pruebas los achican para ejercitarlos sin gigas.</summary>
    internal sealed record Limits(
        int Entries,
        long SheetBytes,
        long SharedStringsBytes,
        long PartBytes,
        long SharedStringChars,
        long CsvBytes)
    {
        public static Limits Default { get; } = new(
            MaxEntries, MaxSheetBytes, MaxSharedStringsBytes, MaxPartBytes, MaxSharedStringChars, MaxCsvBytes);
    }

    private const int EndOfDirectorySignature = 0x06054b50;
    private const int EndOfDirectoryLength = 22;

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] CompoundSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>Qué es el archivo, por sus primeros bytes.</summary>
    public static IncomingFileKind Detect(string path)
    {
        using var stream = File.OpenRead(path);
        return Detect(stream);
    }

    /// <summary>Qué es el archivo, por los primeros bytes de un stream recién abierto.</summary>
    public static IncomingFileKind Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> head = stackalloc byte[8];
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);

        if (read >= ZipSignature.Length && head[..ZipSignature.Length].SequenceEqual(ZipSignature))
        {
            return IncomingFileKind.Workbook;
        }

        return read == CompoundSignature.Length && head.SequenceEqual(CompoundSignature)
            ? IncomingFileKind.Compound
            : IncomingFileKind.Text;
    }

    /// <summary>
    /// Escribe en <paramref name="csvPath"/> la primera hoja visible del libro como CSV.
    /// </summary>
    /// <exception cref="SourceSchemaException">El libro no se puede leer, con su motivo.</exception>
    public static XlsxConversion Convert(string workbookPath, string csvPath)
        => Convert(workbookPath, csvPath, Limits.Default);

    internal static XlsxConversion Convert(string workbookPath, string csvPath, Limits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        ArgumentNullException.ThrowIfNull(limits);

        if (Detect(workbookPath) == IncomingFileKind.Compound)
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxProtected));
        }

        try
        {
            using var input = File.OpenRead(workbookPath);
            CheckDirectory(input, limits.Entries);
            input.Position = 0;

            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
            var workbook = new Workbook(archive, limits);

            using var output = new CappedStream(File.Create(csvPath), limits.CsvBytes);
            using var writer = new StreamWriter(output, new UTF8Encoding(false));

            var conversion = workbook.WriteFirstSheet(writer);
            writer.Flush();

            return conversion;
        }
        catch (LimitExceededException ex)
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxTooLarge), ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or NotSupportedException
            or EndOfStreamException or FormatException or OverflowException)
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxUnreadable), ex);
        }
    }

    /// <summary>
    /// Lee el final del ZIP antes de abrirlo: cuántas entradas declara su directorio. El
    /// framework las lista todas en memoria al abrirlo, y un ZIP fabricado puede declarar
    /// millones en pocos megas.
    /// </summary>
    private static void CheckDirectory(FileStream input, int maxEntries)
    {
        var length = input.Length;
        var tail = (int)Math.Min(length, EndOfDirectoryLength + ushort.MaxValue);

        if (tail < EndOfDirectoryLength)
        {
            throw new InvalidDataException("Too short to be a ZIP.");
        }

        var buffer = new byte[tail];
        input.Position = length - tail;
        input.ReadExactly(buffer);

        for (var i = tail - EndOfDirectoryLength; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(i)) != EndOfDirectorySignature)
            {
                continue;
            }

            var entries = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(i + 10));

            // 0xFFFF es «mira el registro ZIP64»: un libro de este tamaño no lo necesita.
            if (entries > maxEntries)
            {
                throw new LimitExceededException();
            }

            return;
        }

        throw new InvalidDataException("End of central directory not found.");
    }

    private static XmlReaderSettings XmlSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersFromEntities = 1024,
        CloseInput = true,
    };

    /// <summary>
    /// Un número como lo escribiría un CSV: invariante, sin exponente y con las 15 cifras que
    /// muestra Excel. "1.234" se escribe "1.2340": el lector lee "1.234" como miles en un campo
    /// entero, y el valor es el mismo.
    /// </summary>
    internal static string FormatNumber(string raw)
    {
        var trimmed = raw.Trim();

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
        {
            return trimmed;
        }

        var text = value.ToString("G15", CultureInfo.InvariantCulture);

        if (text.Contains('E', StringComparison.Ordinal) && Math.Abs(value) < 7.9e28)
        {
            text = ((decimal)value).ToString(CultureInfo.InvariantCulture);
        }

        return LooksLikeThousands(text) ? text + "0" : text;
    }

    /// <summary>Un día de Excel como fecha ISO; con hora, si la tiene.</summary>
    internal static string FormatDate(string raw, bool date1904)
    {
        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            || serial < 0
            || serial > 2_958_465)
        {
            return FormatNumber(raw);
        }

        var days = Math.Floor(serial);
        var seconds = Math.Round((serial - days) * 86_400);

        if (seconds >= 86_400)
        {
            days++;
            seconds = 0;
        }

        DateTime origin;

        if (date1904)
        {
            origin = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        }
        else if (days < 60)
        {
            origin = new DateTime(1899, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        }
        else if (days == 60)
        {
            // El 29 de febrero de 1900 que Excel hereda de Lotus 1-2-3: no existe.
            return FormatNumber(raw);
        }
        else
        {
            origin = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);
        }

        var date = origin.AddDays(days).AddSeconds(seconds);

        return seconds == 0
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static bool LooksLikeThousands(string text)
    {
        var digits = text.StartsWith('-') ? text.AsSpan(1) : text.AsSpan();
        var dot = digits.IndexOf('.');

        return dot is >= 1 and <= 3
            && digits.Length - dot - 1 == 3
            && digits[..dot].IndexOfAnyExceptInRange('0', '9') < 0
            && digits[(dot + 1)..].IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>
    /// Los escapes de Excel para caracteres que el XML no admite: "_x000D_" es un retorno de
    /// carro y "_x005F_" un guion bajo literal.
    /// </summary>
    internal static string DecodeEscapes(string text)
    {
        if (!text.Contains("_x", StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '_' && i + 6 < text.Length && text[i + 1] == 'x' && text[i + 6] == '_'
                && int.TryParse(text.AsSpan(i + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
            {
                result.Append((char)code);
                i += 6;
                continue;
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Formatos de número que son fechas: los integrados de fecha y hora, y los propios que
    /// usan días, meses, años, horas o segundos fuera de comillas y corchetes.
    /// </summary>
    internal static bool IsDateFormat(int id, string? code)
    {
        if (id is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58))
        {
            return true;
        }

        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        var inQuotes = false;
        var inBrackets = false;

        for (var i = 0; i < code.Length; i++)
        {
            var character = code[i];

            if (inQuotes)
            {
                inQuotes = character != '"';
                continue;
            }

            if (inBrackets)
            {
                inBrackets = character != ']';
                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    continue;
                case '[':
                    inBrackets = true;
                    continue;
                case '\\' or '_' or '*':
                    // El carácter siguiente es literal, de relleno o de espacio.
                    i++;
                    continue;
            }

            if (char.ToLowerInvariant(character) is 'y' or 'm' or 'd' or 'h' or 's')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Las partes del libro que hacen falta para leer una hoja.</summary>
    private sealed class Workbook
    {
        private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly Limits _limits;

        public Workbook(ZipArchive archive, Limits limits)
        {
            _limits = limits;

            foreach (var entry in archive.Entries)
            {
                _entries.TryAdd(entry.FullName.Replace('\\', '/').TrimStart('/'), entry);
            }
        }

        public XlsxConversion WriteFirstSheet(TextWriter writer)
        {
            var workbookPath = Relationships("_rels/.rels", string.Empty)
                .FirstOrDefault(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal))?.Target
                ?? "xl/workbook.xml";

            if (!_entries.ContainsKey(workbookPath))
            {
                throw new InvalidDataException("The workbook part is missing.");
            }

            var related = Relationships(RelationshipsPathOf(workbookPath), workbookPath);
            var (sheets, date1904) = ReadWorkbook(workbookPath);

            var sheet = sheets.FirstOrDefault(s => s.Visible) ?? sheets.FirstOrDefault()
                ?? throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxNoSheet));

            var sheetPath = related.FirstOrDefault(r => string.Equals(r.Id, sheet.RelationshipId, StringComparison.Ordinal))?.Target;

            if (sheetPath is null || !_entries.ContainsKey(sheetPath))
            {
                throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxNoSheet));
            }

            var sharedPath = related.FirstOrDefault(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))?.Target;
            var stylesPath = related.FirstOrDefault(r => r.Type.EndsWith("/styles", StringComparison.Ordinal))?.Target;

            var shared = sharedPath is not null && _entries.ContainsKey(sharedPath) ? ReadSharedStrings(sharedPath) : [];
            var dates = stylesPath is not null && _entries.ContainsKey(stylesPath) ? ReadDateStyles(stylesPath) : [];

            var sheetReader = new SheetReader(shared, dates, date1904);

            using var reader = Open(sheetPath, _limits.SheetBytes);
            var (rows, columns) = sheetReader.Write(reader, writer);

            return new XlsxConversion(sheet.Name, rows, columns);
        }

        private XmlReader Open(string path, long maxBytes)
            => XmlReader.Create(new CappedStream(_entries[path].Open(), maxBytes), XmlSettings());

        private List<Relationship> Relationships(string relsPath, string sourcePart)
        {
            var result = new List<Relationship>();

            if (!_entries.ContainsKey(relsPath))
            {
                return result;
            }

            using var reader = Open(relsPath, _limits.PartBytes);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship"
                    || string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = reader.GetAttribute("Id");
                var type = reader.GetAttribute("Type");
                var target = reader.GetAttribute("Target");

                if (id is not null && type is not null && target is not null)
                {
                    result.Add(new Relationship(id, type, Resolve(sourcePart, target)));
                }
            }

            return result;
        }

        private (List<SheetEntry> Sheets, bool Date1904) ReadWorkbook(string path)
        {
            var sheets = new List<SheetEntry>();
            var date1904 = false;

            using var reader = Open(path, _limits.PartBytes);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.LocalName == "workbookPr")
                {
                    var value = reader.GetAttribute("date1904");
                    date1904 = value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                }
                else if (reader.LocalName == "sheet")
                {
                    var name = reader.GetAttribute("name") ?? string.Empty;
                    var state = reader.GetAttribute("state");
                    string? relationship = null;

                    // El r:id viene en el espacio de nombres de relaciones, que cambia entre el
                    // formato de transición y el estricto: se busca por nombre local.
                    while (reader.MoveToNextAttribute())
                    {
                        if (reader.LocalName == "id" && reader.NamespaceURI.Length > 0)
                        {
                            relationship = reader.Value;
                        }
                    }

                    reader.MoveToElement();

                    if (relationship is not null)
                    {
                        sheets.Add(new SheetEntry(name, relationship, state is null or "visible"));
                    }
                }
            }

            return (sheets, date1904);
        }

        private List<string> ReadSharedStrings(string path)
        {
            var strings = new List<string>();
            long characters = 0;

            using var reader = Open(path, _limits.SharedStringsBytes);
            reader.Read();

            // ReadText deja el lector en el nodo siguiente: avanzar otra vez saltaría un texto.
            while (!reader.EOF)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si")
                {
                    reader.Read();
                    continue;
                }

                var text = ReadText(reader);
                characters += text.Length;

                if (characters > _limits.SharedStringChars)
                {
                    throw new LimitExceededException();
                }

                strings.Add(text);
            }

            return strings;
        }

        private List<bool> ReadDateStyles(string path)
        {
            var custom = new Dictionary<int, string>();
            var formats = new List<int>();

            using var reader = Open(path, _limits.PartBytes);
            var cellFormatsDepth = -1;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "cellXfs")
                {
                    cellFormatsDepth = -1;
                    continue;
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "numFmt" when int.TryParse(reader.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out var id):
                        custom[id] = reader.GetAttribute("formatCode") ?? string.Empty;
                        break;
                    case "cellXfs" when !reader.IsEmptyElement:
                        cellFormatsDepth = reader.Depth;
                        break;
                    case "xf" when cellFormatsDepth >= 0 && reader.Depth == cellFormatsDepth + 1:
                        formats.Add(int.TryParse(reader.GetAttribute("numFmtId"), CultureInfo.InvariantCulture, out var format) ? format : 0);
                        break;
                }
            }

            return [.. formats.Select(id => IsDateFormat(id, custom.GetValueOrDefault(id)))];
        }

        private static string RelationshipsPathOf(string part)
        {
            var slash = part.LastIndexOf('/');
            return slash < 0 ? $"_rels/{part}.rels" : $"{part[..slash]}/_rels/{part[(slash + 1)..]}.rels";
        }

        /// <summary>El destino de una relación como ruta dentro del ZIP, sin subir de la raíz.</summary>
        private static string Resolve(string sourcePart, string target)
        {
            var decoded = Uri.UnescapeDataString(target).Replace('\\', '/');
            var slash = sourcePart.LastIndexOf('/');
            var combined = decoded.StartsWith('/')
                ? decoded
                : (slash < 0 ? string.Empty : sourcePart[..(slash + 1)]) + decoded;

            var segments = new List<string>();

            foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                }
                else if (segment != ".")
                {
                    segments.Add(segment);
                }
            }

            return string.Join('/', segments);
        }
    }

    /// <summary>
    /// El texto de un elemento con texto enriquecido (<c>si</c> o <c>is</c>): sus <c>t</c>, sin
    /// la guía fonética. Deja el lector después del elemento.
    /// </summary>
    private static string ReadText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return string.Empty;
        }

        var depth = reader.Depth;
        var text = new StringBuilder();
        reader.Read();

        while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName is "rPh" or "phoneticPr")
            {
                reader.Skip();
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t")
            {
                text.Append(reader.ReadElementContentAsString());
            }
            else
            {
                reader.Read();
            }
        }

        reader.Read();

        return DecodeEscapes(text.ToString());
    }

    /// <summary>Recorre las filas de una hoja y escribe el CSV.</summary>
    private sealed class SheetReader(List<string> shared, List<bool> dateStyles, bool date1904)
    {
        public (int Rows, int Columns) Write(XmlReader reader, TextWriter writer)
        {
            string[]? header = null;
            var cells = new string?[MaxColumns];
            var rows = 0;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                {
                    continue;
                }

                var width = header?.Length ?? MaxColumns;
                Array.Clear(cells);
                var last = ReadRow(reader, cells, width, header is null);

                if (last < 0)
                {
                    // Una fila vacía —o solo con formato— no es un registro.
                    continue;
                }

                if (header is null)
                {
                    header = [.. cells.Take(last + 1).Select(c => c ?? string.Empty)];
                    writer.Write(ColumnMapping.ToCsvLine(header));
                    writer.Write('\n');
                    continue;
                }

                if (rows >= CsvSourceReader.MaxRows)
                {
                    throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvTooManyRows, ("max", CsvSourceReader.MaxRows)));
                }

                writer.Write(ColumnMapping.ToCsvLine([.. cells.Take(header.Length).Select(c => c ?? string.Empty)]));
                writer.Write('\n');
                rows++;
            }

            if (header is null)
            {
                throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxNoSheet));
            }

            return (rows, header.Length);
        }

        /// <summary>
        /// Lee las celdas de una fila hasta <paramref name="width"/> columnas. Devuelve la
        /// posición de la última celda con valor, o -1 si la fila está vacía.
        /// </summary>
        private int ReadRow(XmlReader reader, string?[] cells, int width, bool isHeader)
        {
            if (reader.IsEmptyElement)
            {
                return -1;
            }

            var depth = reader.Depth;
            var next = 0;
            var last = -1;
            reader.Read();

            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c")
                {
                    reader.Read();
                    continue;
                }

                var column = ColumnOf(reader.GetAttribute("r")) ?? next;
                next = column + 1;

                var value = ReadCell(reader);

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (column >= width)
                {
                    // Sin encabezado encima, la celda no carga ningún campo; en el encabezado
                    // mismo, son demasiadas columnas.
                    if (isHeader)
                    {
                        throw new SourceSchemaException(Notice.Of(NoticeCodes.XlsxTooManyColumns, ("max", MaxColumns)));
                    }

                    continue;
                }

                cells[column] = value;
                last = Math.Max(last, column);
            }

            return last;
        }

        /// <summary>El valor de una celda como texto. Deja el lector después de la celda.</summary>
        private string ReadCell(XmlReader reader)
        {
            var type = reader.GetAttribute("t");
            var style = int.TryParse(reader.GetAttribute("s"), CultureInfo.InvariantCulture, out var s) ? s : 0;

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return string.Empty;
            }

            var depth = reader.Depth;
            string? raw = null;
            string? inline = null;
            reader.Read();

            while (!reader.EOF && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "v")
                {
                    raw = reader.ReadElementContentAsString();
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "is")
                {
                    inline = ReadText(reader);
                }
                else if (reader.NodeType == XmlNodeType.Element)
                {
                    // La fórmula no se calcula: vale el último resultado que guardó Excel.
                    reader.Skip();
                }
                else
                {
                    reader.Read();
                }
            }

            reader.Read();

            return type switch
            {
                "s" => SharedString(raw),
                "inlineStr" => inline ?? string.Empty,
                "str" => DecodeEscapes(raw ?? string.Empty),
                "b" => raw?.Trim() is "1" or "true" ? "TRUE" : raw is null ? string.Empty : "FALSE",
                "e" => string.Empty,
                "d" => raw?.Trim() ?? string.Empty,
                _ when raw is null => string.Empty,
                _ when style < dateStyles.Count && dateStyles[style] => FormatDate(raw, date1904),
                _ => FormatNumber(raw),
            };
        }

        private string SharedString(string? raw)
        {
            if (raw is null)
            {
                return string.Empty;
            }

            if (!int.TryParse(raw.Trim(), CultureInfo.InvariantCulture, out var index) || index < 0 || index >= shared.Count)
            {
                throw new InvalidDataException("A cell points to a shared string that does not exist.");
            }

            return shared[index];
        }

        /// <summary>La columna de una referencia como "AB12": 0 es la A.</summary>
        private static int? ColumnOf(string? reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }

            var column = 0;
            var letters = 0;

            foreach (var character in reference)
            {
                if (!char.IsAsciiLetter(character))
                {
                    break;
                }

                column = (column * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
                letters++;

                if (letters > 3)
                {
                    throw new InvalidDataException("Invalid cell reference.");
                }
            }

            return letters == 0 ? null : column - 1;
        }
    }

    private sealed record Relationship(string Id, string Type, string Target);

    private sealed record SheetEntry(string Name, string RelationshipId, bool Visible);

    /// <summary>Se pasó uno de los topes del libro.</summary>
    private sealed class LimitExceededException : Exception;

    /// <summary>
    /// Un stream que corta al pasar un tope de bytes, contando lo que de verdad pasa por él:
    /// el tamaño que declara una entrada del ZIP lo escribe quien fabricó el archivo.
    /// </summary>
    private sealed class CappedStream(Stream inner, long maxBytes) : Stream
    {
        private long _count;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _count;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override void Write(byte[] buffer, int offset, int count)
        {
            Count(count);
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Count(buffer.Length);
            inner.Write(buffer);
        }

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Count(int bytes)
        {
            _count += bytes;

            if (_count > maxBytes)
            {
                throw new LimitExceededException();
            }

            return bytes;
        }
    }
}
