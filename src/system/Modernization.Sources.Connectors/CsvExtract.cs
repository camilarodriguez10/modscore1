using System.Data.Common;
using System.Globalization;
using System.Text;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.Connectors;

/// <summary>Lo que dejó una extracción: filas y bytes del CSV escrito.</summary>
public sealed record CsvExtractResult(int Rows, long Bytes);

/// <summary>
/// Escribe el resultado de una consulta como el CSV que produciría un export: encabezado con
/// los nombres de columna y valores en forma invariante (ADR 0037).
/// </summary>
/// <remarks>
/// El CSV entra después por el mismo camino que un archivo subido —identificación de la fuente
/// por encabezado, mapping, validación de filas, publicación atómica—, así que una base no
/// tiene reglas propias. Los topes son los mismos que los de una carga.
/// </remarks>
public static class CsvExtract
{
    /// <summary>Tope de columnas: los contratos tienen menos de 30.</summary>
    public const int MaxColumns = 200;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static async Task<CsvExtractResult> WriteAsync(
        string source,
        DbDataReader reader,
        string path,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var columns = Header(source, reader);
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

        await using (stream.ConfigureAwait(false))
        {
            var writer = new StreamWriter(stream, Utf8, 64 * 1024);

            await using (writer.ConfigureAwait(false))
            {
                long bytes = 0;
                var rows = 0;
                var fields = new string[columns.Count];

                bytes += await WriteLineAsync(writer, ColumnMapping.ToCsvLine(columns), source, bytes, maxBytes).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (rows >= CsvSourceReader.MaxRows)
                    {
                        throw TooLarge(source);
                    }

                    for (var i = 0; i < fields.Length; i++)
                    {
                        fields[i] = Format(source, columns[i], reader.IsDBNull(i) ? null : reader.GetValue(i));
                    }

                    bytes += await WriteLineAsync(writer, ColumnMapping.ToCsvLine(fields), source, bytes, maxBytes).ConfigureAwait(false);
                    rows++;
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                return new CsvExtractResult(rows, bytes);
            }
        }
    }

    /// <summary>Un valor de la base como lo escribiría un export, sin depender de la cultura.</summary>
    internal static string Format(string source, string column, object? value)
    {
        var text = value switch
        {
            null or DBNull => string.Empty,
            string s => s,
            char c => c.ToString(),
            bool b => b ? "true" : "false",
            byte or sbyte or short or ushort or int or uint or long or ulong
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            decimal d => Decimal(d),
            double d => XlsxSourceConverter.FormatNumber(d.ToString("R", CultureInfo.InvariantCulture)),
            float f => XlsxSourceConverter.FormatNumber(((double)f).ToString("R", CultureInfo.InvariantCulture)),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
            Guid guid => guid.ToString("D"),
            _ => throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorColumnUnsupported, ("source", source), ("column", column), ("type", value.GetType().Name)),
                $"The column '{column}' of '{source}' has the type {value.GetType().Name}, which a source cannot hold. Convert it to text or a number in the query."),
        };

        if (text.Length > CsvSourceReader.MaxFieldLength)
        {
            throw TooLarge(source);
        }

        return text;
    }

    /// <summary>
    /// Un decimal invariante. "1.234" se escribe "1.2340": en un campo entero el lector lee
    /// "1.234" como miles, igual que con un libro de Excel.
    /// </summary>
    private static string Decimal(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        return XlsxSourceConverter.LooksLikeThousands(text) ? text + "0" : text;
    }

    private static List<string> Header(string source, DbDataReader reader)
    {
        if (reader.FieldCount > MaxColumns)
        {
            throw TooLarge(source);
        }

        var columns = new List<string>(reader.FieldCount);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i)?.Trim() ?? string.Empty;

            if (name.Length == 0 || name.Length > ColumnMapping.MaxHeaderNameLength || name.Any(char.IsControl))
            {
                throw new ConnectorException(
                    Notice.Of(NoticeCodes.ConnectorColumnUnnamed, ("source", source), ("position", i + 1)),
                    $"Column {i + 1} of '{source}' has no usable name. Give every column a name with AS.");
            }

            columns.Add(name);
        }

        return columns;
    }

    private static async Task<int> WriteLineAsync(StreamWriter writer, string line, string source, long written, long maxBytes)
    {
        var bytes = Utf8.GetByteCount(line) + 1;

        if (written + bytes > maxBytes)
        {
            throw TooLarge(source);
        }

        await writer.WriteAsync(line).ConfigureAwait(false);
        await writer.WriteAsync('\n').ConfigureAwait(false);

        return bytes;
    }

    private static ConnectorException TooLarge(string source)
        => new(
            Notice.Of(
                NoticeCodes.ConnectorTooLarge,
                ("source", source),
                ("rows", CsvSourceReader.MaxRows),
                ("megabytes", PublishLimits.MaxFileBytes / (1024 * 1024))),
            $"The source '{source}' exceeds the limits of an upload: {CsvSourceReader.MaxRows} rows, {PublishLimits.MaxFileBytes / (1024 * 1024)} MB, {MaxColumns} columns or {CsvSourceReader.MaxFieldLength} characters per value.");
}
