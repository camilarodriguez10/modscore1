using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modernization.Domain;

namespace Modernization.Sources.Files;

/// <summary>
/// Contratos de columna de los archivos fuente. El adaptador valida contra esto
/// antes de leer una fila: una columna que falta o que cambio de nombre falla con
/// un error accionable en lugar de producir un score silenciosamente incorrecto.
/// </summary>
public sealed record SourceSchemaCatalog
{
    public required string Version { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<SourceSchema> Files { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly Lazy<SourceSchemaCatalog> DefaultCatalog = new(Load);

    public static SourceSchemaCatalog Default => DefaultCatalog.Value;

    private static SourceSchemaCatalog Load()
    {
        const string ResourceName = "source-schemas.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new SourceSchemaException($"No se encontro el recurso embebido '{ResourceName}'.");

        using var reader = new StreamReader(stream);
        var catalog = JsonSerializer.Deserialize<SourceSchemaCatalog>(reader.ReadToEnd(), SerializerOptions)
            ?? throw new SourceSchemaException("El catalogo de esquemas esta vacio.");

        catalog.Validate();
        return catalog;
    }

    public void Validate()
    {
        if (Files.Count == 0)
        {
            throw new SourceSchemaException("El catalogo debe declarar al menos un archivo.");
        }

        foreach (var file in Files)
        {
            if (file.Columns.Count == 0)
            {
                throw new SourceSchemaException($"El archivo '{file.FileName}' no declara columnas.");
            }

            if (!file.Columns.Any(c => string.Equals(c.Name, file.KeyColumn, StringComparison.Ordinal)))
            {
                throw new SourceSchemaException(
                    $"El archivo '{file.FileName}' declara la columna clave '{file.KeyColumn}', "
                    + "que no esta entre sus columnas.");
            }
        }
    }

    public SourceSchema Require(string fileName)
        => Files.SingleOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new SourceSchemaException(
                $"No hay contrato para '{fileName}'. Archivos esperados: "
                + $"{string.Join(", ", Files.Select(f => f.FileName))}.");

    /// <summary>Nombres de archivo aceptados por la carga.</summary>
    public IReadOnlyList<string> ExpectedFileNames => [.. Files.Select(f => f.FileName)];
}

public sealed record SourceSchema
{
    public required string Source { get; init; }
    public required string FileName { get; init; }
    public required string KeyColumn { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }

    public IEnumerable<ColumnSchema> RequiredColumns => Columns.Where(c => c.Required);
}

public sealed record ColumnSchema
{
    public required string Name { get; init; }
    public required ColumnType Type { get; init; }
    public required bool Required { get; init; }

    /// <summary>
    /// Otros nombres con los que la misma columna ha aparecido. Un export de Power
    /// BI escribe "Suma de Invest_Count" donde el sistema origen escribe
    /// "Invest_Count": ambos apuntan al mismo dato.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>True si el encabezado corresponde a esta columna.</summary>
    public bool Matches(string header)
    {
        var trimmed = header.Trim();

        return string.Equals(Name, trimmed, StringComparison.OrdinalIgnoreCase)
            || Aliases.Any(alias => string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

public enum ColumnType
{
    String,
    Int,
    Long,
    Decimal,
    Bool,
    DateTime,
}

/// <summary>Valor crudo convertido segun el tipo declarado, o el motivo del fallo.</summary>
public readonly record struct ParseOutcome(bool Ok, object? Value, Notice? Error)
{
    public static ParseOutcome Empty => new(true, null, null);

    public static ParseOutcome Success(object value) => new(true, value, null);

    public static ParseOutcome Failure(Notice error) => new(false, null, error);
}

/// <summary>
/// Conversion de texto a los tipos declarados, tolerante con lo que los exports
/// reales producen: separadores de miles, comas decimales, TRUE/FALSE, Yes/No.
/// </summary>
public static class ColumnParser
{
    /// <summary>Cuanto del valor original se devuelve en un rechazo.</summary>
    private const int MaxEchoedValue = 64;

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-dd",
        "MM/dd/yyyy HH:mm",
        "MM/dd/yyyy",
    ];

    public static ParseOutcome Parse(ColumnSchema column, string? raw)
    {
        var text = raw?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return ParseOutcome.Empty;
        }

        return column.Type switch
        {
            ColumnType.String => ParseOutcome.Success(text),
            ColumnType.Int => ParseInteger(column, text),
            ColumnType.Long => ParseLong(column, text),
            ColumnType.Decimal => ParseDecimal(column, text),
            ColumnType.Bool => ParseBoolean(column, text),
            ColumnType.DateTime => ParseDateTime(column, text),
            _ => ParseOutcome.Failure(Invalid(column, "unknown", text)),
        };
    }

    private static ParseOutcome ParseInteger(ColumnSchema column, string text)
        => int.TryParse(Clean(text), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? ParseOutcome.Success(value)
            : ParseOutcome.Failure(Invalid(column, "integer", text));

    private static ParseOutcome ParseLong(ColumnSchema column, string text)
        => long.TryParse(Clean(text), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? ParseOutcome.Success(value)
            : ParseOutcome.Failure(Invalid(column, "long", text));

    private static ParseOutcome ParseDecimal(ColumnSchema column, string text)
    {
        var cleaned = Clean(text);

        // Un export en configuracion regional europea escribe "1,75" donde el
        // invariante espera "1.75". Sin coma de miles, la coma es decimal.
        if (cleaned.Contains(',', StringComparison.Ordinal) && !cleaned.Contains('.', StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace(',', '.');
        }
        else
        {
            cleaned = cleaned.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? ParseOutcome.Success(value)
            : ParseOutcome.Failure(Invalid(column, "number", text));
    }

    private static ParseOutcome ParseBoolean(ColumnSchema column, string text) => text.ToUpperInvariant() switch
    {
        "TRUE" or "T" or "YES" or "Y" or "1" or "SI" or "SÍ" => ParseOutcome.Success(true),
        "FALSE" or "F" or "NO" or "N" or "0" => ParseOutcome.Success(false),
        _ => ParseOutcome.Failure(Invalid(column, "boolean", text)),
    };

    private static ParseOutcome ParseDateTime(ColumnSchema column, string text)
    {
        if (DateTimeOffset.TryParseExact(
                text,
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            return ParseOutcome.Success(exact);
        }

        return DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var loose)
            ? ParseOutcome.Success(loose)
            : ParseOutcome.Failure(Invalid(column, "date", text));
    }

    /// <summary>
    /// Un valor que no se pudo convertir, como codigo y datos (ADR 0017). El valor se
    /// acota: es contenido del archivo y termina en la pantalla de linaje.
    /// </summary>
    private static Notice Invalid(ColumnSchema column, string expected, string text)
        => Notice.Of(
            NoticeCodes.RowInvalidValue,
            ("column", column.Name),
            ("expected", expected),
            ("value", text.Length > MaxEchoedValue ? text[..MaxEchoedValue] + "…" : text));

    /// <summary>Quita espacios no separables y simbolos de porcentaje que dejan los exports.</summary>
    private static string Clean(string text)
        => text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Trim();
}

/// <summary>Contrato de esquema invalido, o archivo que no lo cumple.</summary>
/// <remarks>
/// Dos usos distintos. Un archivo que no cumple su contrato lleva un <see cref="Notice"/>
/// que termina en la pantalla de cargas (ADR 0017). Un contrato mal escrito falla al
/// arrancar y su mensaje es para quien opera, asi que sigue siendo texto.
/// </remarks>
public sealed class SourceSchemaException : Exception
{
    public SourceSchemaException(Notice notice) : base(notice?.ToString()) => Notice = notice;

    public SourceSchemaException(Notice notice, Exception innerException)
        : base(notice?.ToString(), innerException) => Notice = notice;

    public SourceSchemaException(string message) : base(message) { }

    public SourceSchemaException(string message, Exception innerException) : base(message, innerException) { }

    public SourceSchemaException() { }

    /// <summary>Que no cumple el archivo, cuando el problema es del archivo y no del contrato.</summary>
    public Notice? Notice { get; }
}
