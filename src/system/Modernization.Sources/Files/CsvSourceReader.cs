using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Modernization.Domain.Sources;
using Modernization.Domain;

namespace Modernization.Sources.Files;

/// <summary>
/// Lee un CSV validandolo contra su contrato de esquema.
/// </summary>
/// <remarks>
/// Dos niveles de fallo, deliberadamente distintos:
/// <list type="bullet">
/// <item>Una columna obligatoria que falta invalida el archivo completo y lanza:
/// seguir leyendo produciria un portafolio incompleto sin que nadie lo note.</item>
/// <item>Una fila mal formada se rechaza con su motivo y la lectura continua: un
/// dato malo en la fila 300 no puede dejar sin score a las otras 249.</item>
/// </list>
/// </remarks>
public sealed class CsvSourceReader
{
    /// <summary>Tope de filas por archivo. Un archivo mayor es un error, no un caso.</summary>
    public const int MaxRows = 500_000;

    /// <summary>
    /// Tope de caracteres por campo. Ninguna columna de las fuentes se acerca: un
    /// campo mayor es un archivo corrupto, o uno fabricado para agotar memoria.
    /// </summary>
    public const int MaxFieldLength = 4096;

    /// <summary>Cuantas columnas del encabezado se devuelven cuando falta alguna obligatoria.</summary>
    private const int MaxEchoedHeaders = 40;

    /// <summary>Largo maximo de cada columna devuelta.</summary>
    private const int MaxEchoedHeaderLength = 64;

    private readonly SourceSchema _schema;

    public CsvSourceReader(SourceSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _schema = schema;
    }

    /// <summary>Fila leida como diccionario de columna canonica a valor convertido.</summary>
    public sealed record Row(int Number, IReadOnlyDictionary<string, object?> Values)
    {
        public string? Text(string column) => Values.GetValueOrDefault(column) as string;

        public int? Int(string column) => Values.GetValueOrDefault(column) as int?;

        public long? Long(string column) => Values.GetValueOrDefault(column) as long?;

        public decimal? Decimal(string column) => Values.GetValueOrDefault(column) as decimal?;

        public bool? Bool(string column) => Values.GetValueOrDefault(column) as bool?;

        public DateTimeOffset? Date(string column) => Values.GetValueOrDefault(column) as DateTimeOffset?;
    }

    /// <summary>Cuántos problemas distintos se devuelven por archivo.</summary>
    public const int MaxReasons = 20;

    /// <summary>Cuántos números de fila se devuelven por problema.</summary>
    public const int MaxSampleRows = 5;

    public sealed record ReadOutcome(IReadOnlyList<Row> Rows, IReadOnlyList<SourceRejection> Rejections)
    {
        /// <summary>
        /// Los rechazos agrupados por problema, con <b>todas</b> las columnas malas de cada
        /// fila y no solo la primera: quien corrige el archivo necesita la lista completa
        /// de una vez, no descubrirla carga tras carga.
        /// </summary>
        public IReadOnlyList<RejectionReason> Reasons { get; init; } = [];
    }

    public ReadOutcome Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            return ReadCore(reader);
        }
        catch (MaxFieldSizeException ex)
        {
            // Ninguna columna de las fuentes necesita tanto: el archivo esta corrupto
            // o no es el export esperado.
            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvFieldTooLong, ("max", MaxFieldLength), ("row", ex.Context?.Parser?.Row), ("file", _schema.FileName)),
                ex);
        }
        catch (CsvHelperException ex)
        {
            // Sin el mensaje de CsvHelper: incluye el contenido crudo de la fila.
            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvUnreadable, ("row", ex.Context?.Parser?.Row), ("file", _schema.FileName)),
                ex);
        }
    }

    /// <summary>
    /// Lee solo el encabezado, sin contrato. Es lo que permite identificar un
    /// archivo por lo que trae dentro en lugar de por como lo llamaron.
    /// </summary>
    public static IReadOnlyList<string> ReadHeader(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            using var csv = new CsvReader(reader, Configuration());

            if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is not { Length: > 0 } headers)
            {
                throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader));
            }

            return headers;
        }
        catch (MaxFieldSizeException ex)
        {
            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvFieldTooLong, ("max", MaxFieldLength), ("row", 1)),
                ex);
        }
        catch (CsvHelperException ex)
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)), ex);
        }
    }

    /// <summary>
    /// Los exports reales traen espacios sueltos y comillas irregulares: tolerarlos
    /// aqui es preferible a rechazar el archivo entero. El tope de campo no es
    /// tolerancia sino defensa, y por eso viaja en la misma configuracion.
    /// </summary>
    private static CsvConfiguration Configuration() => new(CultureInfo.InvariantCulture)
    {
        TrimOptions = TrimOptions.Trim,
        DetectColumnCountChanges = false,
        MissingFieldFound = null,
        BadDataFound = null,
        HasHeaderRecord = true,
        MaxFieldSize = MaxFieldLength,
    };

    private ReadOutcome ReadCore(TextReader reader)
    {
        using var csv = new CsvReader(reader, Configuration());

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader, ("file", _schema.FileName)));
        }

        var headers = csv.HeaderRecord
            ?? throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader, ("file", _schema.FileName)));

        var mapping = MapColumns(headers);
        var rows = new List<Row>();
        var rejections = new List<SourceRejection>();
        var reasons = new ReasonTally();
        var rowNumber = 1;

        while (csv.Read())
        {
            rowNumber++;

            if (rows.Count >= MaxRows)
            {
                throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvTooManyRows, ("max", MaxRows), ("file", _schema.FileName)));
            }

            var (row, rejection) = ReadRow(csv, mapping, rowNumber, reasons);

            if (row is not null)
            {
                rows.Add(row);
            }
            else if (rejection is not null)
            {
                rejections.Add(rejection);
            }
        }

        return new ReadOutcome(rows, rejections) { Reasons = reasons.Summary() };
    }

    /// <summary>
    /// Empareja los encabezados del archivo con las columnas del contrato,
    /// aceptando los alias. Una columna obligatoria ausente invalida el archivo.
    /// </summary>
    private Dictionary<string, int> MapColumns(string[] headers)
    {
        var mapping = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var column in _schema.Columns)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                if (column.Matches(headers[i]))
                {
                    mapping[column.Name] = i;
                    break;
                }
            }
        }

        var missing = _schema.RequiredColumns
            .Where(c => !mapping.ContainsKey(c.Name))
            .Select(c => c.Name)
            .ToList();

        if (missing.Count > 0)
        {
            // Lo que el archivo trae se devuelve acotado: es contenido del cliente.
            throw new SourceSchemaException(Notice.Of(
                NoticeCodes.CsvMissingColumns,
                ("missing", string.Join(", ", missing)),
                ("found", string.Join(", ", headers.Take(MaxEchoedHeaders).Select(Bounded))),
                ("file", _schema.FileName)));
        }

        return mapping;
    }

    private (Row? Row, SourceRejection? Rejection) ReadRow(
        CsvReader csv,
        Dictionary<string, int> mapping,
        int rowNumber,
        ReasonTally reasons)
    {
        var values = new Dictionary<string, object?>(mapping.Count, StringComparer.Ordinal);
        var errors = new List<Notice>();

        foreach (var column in _schema.Columns)
        {
            if (!mapping.TryGetValue(column.Name, out var index))
            {
                values[column.Name] = null;
                continue;
            }

            var raw = csv.TryGetField<string>(index, out var field) ? field : null;
            var outcome = ColumnParser.Parse(column, raw);

            if (outcome.Ok)
            {
                values[column.Name] = outcome.Value;
            }
            else
            {
                errors.Add(outcome.Error!);
            }
        }

        var key = values.GetValueOrDefault(_schema.KeyColumn) as string
            ?? values.GetValueOrDefault(_schema.KeyColumn)?.ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            var missingKey = Notice.Of(NoticeCodes.RowMissingKey, ("column", _schema.KeyColumn));
            reasons.Add(missingKey, rowNumber);

            return (null, new SourceRejection(rowNumber, missingKey, null));
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                reasons.Add(error, rowNumber);
            }

            // El primero, con cuantos mas hubo: una fila con doce columnas malas no
            // necesita doce lineas en la pantalla de linaje para entenderse.
            var reason = errors.Count == 1 ? errors[0] : errors[0].With("more", errors.Count - 1);

            return (null, new SourceRejection(rowNumber, reason, key));
        }

        return (new Row(rowNumber, values), null);
    }

    private static string Bounded(string header)
        => header.Length > MaxEchoedHeaderLength ? header[..MaxEchoedHeaderLength] + "…" : header;

    /// <summary>
    /// Agrupa los problemas por código, columna y tipo esperado. El valor no entra en la
    /// clave: tres mil filas con tres mil valores distintos en la misma columna son un
    /// solo problema, y el aviso guarda el de la primera como ejemplo.
    /// </summary>
    private sealed class ReasonTally
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private int _order;

        public void Add(Notice notice, int rowNumber)
        {
            var key = string.Join(
                '\u001f',
                notice.Code,
                notice.Args.GetValueOrDefault("column"),
                notice.Args.GetValueOrDefault("expected"));

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry(notice, _order++);
                _entries[key] = entry;
            }

            entry.Rows++;

            if (entry.SampleRows.Count < MaxSampleRows)
            {
                entry.SampleRows.Add(rowNumber);
            }
        }

        public IReadOnlyList<RejectionReason> Summary()
            => [.. _entries.Values
                .OrderByDescending(e => e.Rows)
                .ThenBy(e => e.Order)
                .Take(MaxReasons)
                .Select(e => new RejectionReason(e.Example, e.Rows, [.. e.SampleRows]))];

        private sealed class Entry(Notice example, int order)
        {
            public Notice Example { get; } = example;

            public int Order { get; } = order;

            public int Rows { get; set; }

            public List<int> SampleRows { get; } = [];
        }
    }
}
