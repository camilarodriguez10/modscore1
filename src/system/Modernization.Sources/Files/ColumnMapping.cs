using System.Text;
using Modernization.Domain;
using Modernization.Domain.Sources;

namespace Modernization.Sources.Files;

/// <summary>Cómo quedó decidido qué columna del archivo carga un campo.</summary>
public enum ColumnMatch
{
    /// <summary>El archivo trae una columna con el nombre del campo.</summary>
    Name,

    /// <summary>El archivo trae una columna con uno de los alias del contrato.</summary>
    Alias,

    /// <summary>Una persona eligió la columna antes de cargar.</summary>
    Manual,

    /// <summary>Se tomó del mapping de la carga vigente, porque el archivo trae esa columna.</summary>
    Remembered,

    /// <summary>Una persona indicó que el archivo no trae el campo.</summary>
    Excluded,

    /// <summary>Ninguna columna del archivo corresponde al campo.</summary>
    Missing,
}

/// <summary>Un campo del contrato y la columna del archivo que lo carga.</summary>
public sealed record ResolvedColumn(ColumnSchema Column, int? Index, string? Header, ColumnMatch How);

/// <summary>
/// Qué columna del archivo carga cada campo de una fuente, con los problemas que impiden
/// cargarlo así.
/// </summary>
public sealed record ColumnResolution(
    SourceSchema Schema,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ResolvedColumn> Columns,
    IReadOnlyList<Notice> Problems)
{
    public bool IsValid => Problems.Count == 0;

    /// <summary>True si alguna decisión no sale del nombre o de los alias.</summary>
    public bool HasDecisions => Columns.Any(c => c.How is ColumnMatch.Manual or ColumnMatch.Remembered or ColumnMatch.Excluded);

    /// <summary>Columnas del archivo que no cargan ningún campo.</summary>
    public IReadOnlyList<string> UnusedHeaders
    {
        get
        {
            var used = Columns.Where(c => c.Index is not null).Select(c => c.Index!.Value).ToHashSet();
            return [.. Headers.Where((_, index) => !used.Contains(index))];
        }
    }

    /// <summary>Lo que queda registrado con la carga.</summary>
    public SourceColumnMap ToColumnMap()
        => new(Schema.FileName, [.. Columns.Select(c => new ColumnAssignment(
            c.Column.Name,
            c.Header,
            c.How is ColumnMatch.Manual or ColumnMatch.Remembered or ColumnMatch.Excluded))]);

    /// <summary>
    /// El encabezado con el que se guarda el archivo: cada columna elegida pasa a llamarse
    /// como su campo, y toda otra columna que el lector reconocería por nombre o alias se
    /// marca para que no cargue nada. Así el lector de siempre lee exactamente este mapping,
    /// hoy y cuando se vuelva a leer la versión.
    /// </summary>
    public IReadOnlyList<string> RewrittenHeaders()
    {
        var result = Headers.ToArray();
        var chosen = Columns
            .Where(c => c.Index is not null)
            .ToDictionary(c => c.Index!.Value, c => c.Column.Name);

        for (var index = 0; index < result.Length; index++)
        {
            if (chosen.TryGetValue(index, out var name))
            {
                result[index] = name;
            }
            else if (Schema.Columns.Any(column => column.Matches(Headers[index])))
            {
                result[index] = ColumnMapping.NotLoadedPrefix + Headers[index];
            }
        }

        return result;
    }
}

/// <summary>
/// El mapping de columnas previo a la carga: qué columna del archivo carga cada campo.
/// </summary>
/// <remarks>
/// Hasta aquí un campo se reconocía solo por su nombre o por los alias del contrato, y
/// un export con otro nombre de columna fallaba o, peor, cargaba otra columna que
/// coincidía por alias. Ahora quien sube los archivos puede decidirlo antes de publicar.
/// <para>
/// El mapping no cambia el lector: se aplica <b>reescribiendo el encabezado</b> con los
/// nombres canónicos antes de leer. Así el archivo que queda guardado dice por sí mismo
/// qué columna cargó cada campo, y releer una versión vieja no depende del mapping de hoy.
/// </para>
/// </remarks>
public static class ColumnMapping
{
    /// <summary>Tope de columnas de un encabezado. Los contratos tienen menos de 30.</summary>
    public const int MaxHeaderColumns = 300;

    /// <summary>Largo máximo del nombre de una columna.</summary>
    public const int MaxHeaderNameLength = 256;

    /// <summary>Largo máximo de la línea de encabezado que se acepta para revisar el mapping.</summary>
    public const int MaxHeaderLineLength = 64 * 1024;

    /// <summary>Prefijo de una columna que el mapping deja fuera: ya no coincide con ningún campo.</summary>
    public const string NotLoadedPrefix = "(not loaded) ";

    /// <summary>
    /// Resuelve el mapping de un archivo. <paramref name="manual"/> son las decisiones de
    /// una persona (campo a columna, o a null si el archivo no lo trae); <paramref name="remembered"/>,
    /// las de la carga vigente, que valen solo si el archivo todavía trae esa columna.
    /// </summary>
    public static ColumnResolution Resolve(
        SourceSchema schema,
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string?>? manual = null,
        SourceColumnMap? remembered = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(headers);

        var problems = new List<Notice>();
        var decided = new Dictionary<string, (int? Index, string? Header, ColumnMatch How)>(StringComparer.Ordinal);
        var taken = new Dictionary<int, string>();

        foreach (var (key, value) in manual ?? new Dictionary<string, string?>())
        {
            var column = schema.Columns.FirstOrDefault(c => string.Equals(c.Name, key?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (column is null)
            {
                problems.Add(Notice.Of(NoticeCodes.MappingUnknownColumn, ("column", Bounded(key))));
                continue;
            }

            var header = value?.Trim();

            if (string.IsNullOrEmpty(header))
            {
                if (column.Required)
                {
                    problems.Add(Notice.Of(NoticeCodes.MappingRequiredExcluded, ("column", column.Name)));
                }
                else
                {
                    decided[column.Name] = (null, null, ColumnMatch.Excluded);
                }

                continue;
            }

            var index = IndexOf(headers, header);

            if (index < 0)
            {
                problems.Add(Notice.Of(NoticeCodes.MappingHeaderNotInFile, ("column", column.Name), ("header", Bounded(header))));
                continue;
            }

            if (!taken.TryAdd(index, column.Name))
            {
                problems.Add(Notice.Of(NoticeCodes.MappingHeaderTwice, ("header", Bounded(headers[index]))));
                continue;
            }

            decided[column.Name] = (index, headers[index], ColumnMatch.Manual);
        }

        // Lo recordado de la carga vigente, solo donde nadie decidió y el archivo trae la
        // columna: un mapping viejo nunca apunta a una columna que ya no existe.
        if (remembered is not null && string.Equals(remembered.FileName, schema.FileName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var assignment in remembered.Columns.Where(a => a.Manual))
            {
                var column = schema.Columns.FirstOrDefault(c => string.Equals(c.Name, assignment.Column, StringComparison.Ordinal));

                if (column is null || decided.ContainsKey(column.Name))
                {
                    continue;
                }

                if (assignment.Header is null)
                {
                    if (!column.Required)
                    {
                        decided[column.Name] = (null, null, ColumnMatch.Excluded);
                    }

                    continue;
                }

                var index = IndexOf(headers, assignment.Header);

                if (index >= 0 && taken.TryAdd(index, column.Name))
                {
                    decided[column.Name] = (index, headers[index], ColumnMatch.Remembered);
                }
            }
        }

        var resolved = new List<ResolvedColumn>(schema.Columns.Count);

        foreach (var column in schema.Columns)
        {
            if (decided.TryGetValue(column.Name, out var decision))
            {
                resolved.Add(new ResolvedColumn(column, decision.Index, decision.Header, decision.How));
                continue;
            }

            // Igual que el lector: la primera columna, en orden, que coincide por nombre o
            // alias; salvo las que ya carga otro campo.
            var index = -1;

            for (var i = 0; i < headers.Count; i++)
            {
                if (!taken.ContainsKey(i) && column.Matches(headers[i]))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                resolved.Add(new ResolvedColumn(column, null, null, ColumnMatch.Missing));
                continue;
            }

            taken[index] = column.Name;
            var how = string.Equals(column.Name, headers[index].Trim(), StringComparison.OrdinalIgnoreCase)
                ? ColumnMatch.Name
                : ColumnMatch.Alias;

            resolved.Add(new ResolvedColumn(column, index, headers[index], how));
        }

        var missing = resolved
            .Where(c => c.Column.Required && c.Index is null)
            .Select(c => c.Column.Name)
            .ToList();

        if (missing.Count > 0 && problems.All(p => p.Code != NoticeCodes.MappingRequiredExcluded))
        {
            problems.Add(Notice.Of(NoticeCodes.CsvMissingColumns, ("missing", string.Join(", ", missing)), ("found", string.Empty)));
        }

        return new ColumnResolution(schema, headers, resolved, problems);
    }

    /// <summary>
    /// Copia el archivo con el encabezado reescrito según el mapping. El resto del archivo
    /// pasa tal cual.
    /// </summary>
    /// <exception cref="SourceSchemaException">
    /// El encabezado ocupa más de una línea (un nombre de columna con salto de línea): no
    /// se puede reescribir sin reinterpretar el archivo, y se rechaza en vez de adivinar.
    /// </exception>
    public static void Rewrite(TextReader source, TextWriter destination, ColumnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var rewritten = Open(source, resolution);
        var buffer = new char[81920];
        int read;

        while ((read = rewritten.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// El mismo archivo con el encabezado reescrito, como lector: el adaptador de base de
    /// datos lo lee sin escribirlo en disco ni cargarlo entero en memoria. El lector que
    /// devuelve es dueño de <paramref name="source"/>.
    /// </summary>
    /// <exception cref="SourceSchemaException">El encabezado ocupa más de una línea.</exception>
    public static TextReader Open(TextReader source, ColumnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolution);

        var firstLine = source.ReadLine();

        if (firstLine is null
            || !CsvSourceReader.ReadHeader(new StringReader(firstLine)).SequenceEqual(resolution.Headers, StringComparer.Ordinal))
        {
            // El lector es de quien llama hasta que se devuelve: si no se devuelve, se
            // cierra aqui, o el archivo queda tomado y no se puede borrar.
            source.Dispose();
            throw new SourceSchemaException(Notice.Of(NoticeCodes.MappingHeaderMultiline, ("file", resolution.Schema.FileName)));
        }

        return new RewrittenReader(ToCsvLine(resolution.RewrittenHeaders()) + "\n", source);
    }

    /// <summary>
    /// Lee el encabezado de una línea que mandó el navegador para revisar el mapping. Solo
    /// llegan los nombres de las columnas, nunca filas de datos.
    /// </summary>
    public static IReadOnlyList<string> ParseHeaderLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader));
        }

        if (line.Length > MaxHeaderLineLength)
        {
            throw new SourceSchemaException(TooLarge());
        }

        // Solo la primera línea: si llegó más, el resto no se mira.
        var end = line.IndexOfAny(['\r', '\n']);
        var headers = CsvSourceReader.ReadHeader(new StringReader(end < 0 ? line : line[..end]));

        if (headers.Count > MaxHeaderColumns || headers.Any(h => h.Length > MaxHeaderNameLength))
        {
            throw new SourceSchemaException(TooLarge());
        }

        return headers;
    }

    private static Notice TooLarge()
        => Notice.Of(NoticeCodes.MappingHeaderTooLarge, ("max", MaxHeaderColumns), ("maxLength", MaxHeaderLineLength));

    private static int IndexOf(IReadOnlyList<string> headers, string header)
    {
        var wanted = header.Trim();

        for (var i = 0; i < headers.Count; i++)
        {
            if (string.Equals(headers[i].Trim(), wanted, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Bounded(string? text)
    {
        var value = text ?? string.Empty;
        return value.Length > 64 ? value[..64] + "…" : value;
    }

    /// <summary>El encabezado nuevo y, detrás, el resto del archivo tal cual.</summary>
    private sealed class RewrittenReader(string header, TextReader rest) : TextReader
    {
        private int _position;

        public override int Peek() => _position < header.Length ? header[_position] : rest.Peek();

        public override int Read() => _position < header.Length ? header[_position++] : rest.Read();

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position >= header.Length)
            {
                return rest.Read(buffer, index, count);
            }

            var copied = Math.Min(count, header.Length - _position);
            header.CopyTo(_position, buffer, index, copied);
            _position += copied;

            return copied;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                rest.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>Una línea CSV con comillas donde hacen falta.</summary>
    private static string ToCsvLine(IReadOnlyList<string> fields)
    {
        var line = new StringBuilder();

        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                line.Append(',');
            }

            var field = fields[i];
            var needsQuotes = field.AsSpan().IndexOfAny(",\"\r\n") >= 0
                || (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1])));

            if (needsQuotes)
            {
                line.Append('"').Append(field.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                line.Append(field);
            }
        }

        return line.ToString();
    }
}
