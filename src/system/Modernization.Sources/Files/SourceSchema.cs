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

    /// <summary>
    /// True si la clave no puede repetirse en el archivo, como el EAI en el maestro. Una
    /// clave repetida deja la última fila y reporta las anteriores: dos aplicaciones con el
    /// mismo EAI romperían el portafolio, y la base de datos lo prohíbe.
    /// </summary>
    public bool UniqueKey { get; init; }

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

    /// <summary>
    /// Prefijos que Power BI y Excel anteponen al exportar un campo resumido: "Suma de
    /// Kenna_Score" es Kenna_Score. Solo los que no cambian el valor cuando hay una fila por
    /// clave; un "Recuento de" no está, porque cuenta filas en lugar de traer el dato.
    /// </summary>
    private static readonly string[] SummaryPrefixes =
    [
        "Suma de ", "Sum of ", "Promedio de ", "Average of ", "Media de ",
        "Mín. de ", "Min. de ", "Min of ", "Máx. de ", "Max. de ", "Max of ",
        "Primero de ", "First ", "Último de ", "Last ",
    ];

    /// <summary>True si el encabezado corresponde a esta columna.</summary>
    public bool Matches(string header) => MatchRank(header) is not null;

    /// <summary>
    /// Qué tan bien corresponde un encabezado a esta columna, o null si no corresponde: 0 es
    /// el nombre exacto, después cada alias en su orden, y al final los mismos nombres con
    /// un prefijo de resumen. Cuando dos columnas del archivo sirven para el mismo campo,
    /// gana la de menor rango: el nombre exacto ya no pierde contra un alias que aparece
    /// antes en el archivo.
    /// </summary>
    public int? MatchRank(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var trimmed = header.Trim();
        var rank = RankOf(trimmed);

        if (rank is not null)
        {
            return rank;
        }

        foreach (var prefix in SummaryPrefixes)
        {
            if (trimmed.Length > prefix.Length && trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && RankOf(trimmed[prefix.Length..].Trim()) is { } prefixed)
            {
                return Aliases.Count + 1 + prefixed;
            }
        }

        return null;
    }

    /// <summary>La mejor columna del archivo para este campo, entre las disponibles; -1 si ninguna.</summary>
    public int BestIndexIn(IReadOnlyList<string> headers, Func<int, bool>? available = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var best = -1;
        var bestRank = int.MaxValue;

        for (var i = 0; i < headers.Count; i++)
        {
            if (available is not null && !available(i))
            {
                continue;
            }

            if (MatchRank(headers[i]) is { } rank && rank < bestRank)
            {
                best = i;
                bestRank = rank;
            }
        }

        return best;
    }

    private int? RankOf(string text)
    {
        if (string.Equals(Name, text, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        for (var i = 0; i < Aliases.Count; i++)
        {
            if (string.Equals(Aliases[i], text, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return null;
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

    /// <summary>
    /// Un identificador —EAI, APP_ID— como texto, normalizado: un export que lo trató como
    /// número lo escribe "14,381" o "14381.0", y así no coincidiría con los otros archivos.
    /// </summary>
    Code,
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
        "d/M/yyyy H:mm",
        "d/M/yyyy H:mm:ss",
        "d/M/yyyy",
        "dd-MM-yyyy HH:mm:ss",
        "dd-MM-yyyy",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd",
        "MM/dd/yyyy HH:mm",
        "MM/dd/yyyy",
    ];

    /// <summary>
    /// Lo que los exports escriben en una celda sin dato. Se lee como vacío —sin dato, que
    /// el BRD excluye del cálculo— en lugar de como un valor malo: un «N/A» en Kenna no dice
    /// que el archivo esté mal, dice que esa aplicación no tiene Kenna.
    /// </summary>
    private static readonly HashSet<string> NoValueMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "N/A", "NA", "#N/A", "N.A.", "NULL", "NONE", "NAN", "-", "--", "—", "–", "?",
        "(blank)", "(en blanco)", "(vacío)", "(vacio)", "blank", "sin dato", "s/d", "n/d", "nd",
        "not applicable", "no aplica", "unknown", "desconocido", "tbd",
    };

    /// <summary>Agrupación de miles bien formada: 1,234 · 12.345.678 · 1 234.</summary>
    private static readonly System.Text.RegularExpressions.Regex ThousandsGrouping = new(
        @"^-?\d{1,3}([.,]\d{3})+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>True si la celda trae uno de los marcadores de «sin dato».</summary>
    public static bool IsNoValue(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) || NoValueMarkers.Contains(text);
    }

    public static ParseOutcome Parse(ColumnSchema column, string? raw)
    {
        ArgumentNullException.ThrowIfNull(column);

        var text = raw?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return ParseOutcome.Empty;
        }

        // Un marcador de «sin dato» es vacío en todo menos en el texto libre, donde podría
        // ser el dato mismo; en una clave tampoco es una clave.
        if (column.Type != ColumnType.String && NoValueMarkers.Contains(text))
        {
            return ParseOutcome.Empty;
        }

        return column.Type switch
        {
            ColumnType.String => ParseOutcome.Success(text),
            ColumnType.Code => ParseOutcome.Success(NormalizeCode(text)),
            ColumnType.Int => ParseInteger(column, text),
            ColumnType.Long => ParseLong(column, text),
            ColumnType.Decimal => ParseDecimal(column, text),
            ColumnType.Bool => ParseBoolean(column, text),
            ColumnType.DateTime => ParseDateTime(column, text),
            _ => ParseOutcome.Failure(Invalid(column, "unknown", text)),
        };
    }

    /// <summary>
    /// Un identificador como texto. Si un export lo escribió como número —"14,381",
    /// "14.381", "14381.0"—, vuelve a los dígitos; cualquier otra forma queda tal cual.
    /// </summary>
    public static string NormalizeCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.Trim();

        // ="00123": la forma en que Excel conserva los ceros a la izquierda.
        if (trimmed.Length > 3 && trimmed.StartsWith("=\"", StringComparison.Ordinal) && trimmed.EndsWith('"'))
        {
            trimmed = trimmed[2..^1];
        }

        if (ThousandsGrouping.IsMatch(trimmed))
        {
            return trimmed.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        }

        var dot = trimmed.IndexOf('.', StringComparison.Ordinal);

        if (dot > 0 && trimmed[..dot].All(char.IsAsciiDigit) && trimmed[(dot + 1)..].All(c => c == '0') && dot < trimmed.Length - 1)
        {
            return trimmed[..dot];
        }

        return trimmed;
    }

    private static ParseOutcome ParseInteger(ColumnSchema column, string text)
        => TryParseNumber(text, integral: true, out var value) && value is >= int.MinValue and <= int.MaxValue
            ? ParseOutcome.Success((int)value)
            : ParseOutcome.Failure(Invalid(column, "integer", text));

    private static ParseOutcome ParseLong(ColumnSchema column, string text)
        => TryParseNumber(text, integral: true, out var value) && value is >= long.MinValue and <= long.MaxValue
            ? ParseOutcome.Success((long)value)
            : ParseOutcome.Failure(Invalid(column, "long", text));

    private static ParseOutcome ParseDecimal(ColumnSchema column, string text)
        => TryParseNumber(text, integral: false, out var value)
            ? ParseOutcome.Success(value)
            : ParseOutcome.Failure(Invalid(column, "number", text));

    /// <summary>
    /// Un número escrito en cualquier configuración regional.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Con los dos separadores, el último es el decimal: "1.234,5" y "1,234.5" son 1234,5.</item>
    /// <item>Con uno solo repetido en grupos de tres —"1,234,567", "12.345.678"— es de miles.</item>
    /// <item>Con uno solo y una vez: en un campo entero, si le siguen tres dígitos es de miles
    /// ("1.234" usuarios son 1234); en todo otro caso es el decimal ("97,67", "1.5").</item>
    /// <item>Un entero acepta decimales en cero ("650,00" es 650), que es como Power BI exporta
    /// un campo resumido; con decimales distintos de cero no es un entero y se rechaza.</item>
    /// </list>
    /// Antes el entero se leía con el formato invariante y "650,00" daba 65000 sin aviso.
    /// </remarks>
    private static bool TryParseNumber(string text, bool integral, out decimal value)
    {
        value = 0;
        var cleaned = Clean(text);

        if (cleaned.Length == 0)
        {
            return false;
        }

        var commas = cleaned.Count(c => c == ',');
        var dots = cleaned.Count(c => c == '.');

        if (commas > 0 && dots > 0)
        {
            var decimalIsComma = cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.');
            cleaned = decimalIsComma
                ? cleaned.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : cleaned.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (commas + dots > 0)
        {
            var separator = commas > 0 ? ',' : '.';
            var count = commas + dots;
            var grouped = ThousandsGrouping.IsMatch(cleaned);

            if (count > 1)
            {
                if (!grouped)
                {
                    return false;
                }

                cleaned = cleaned.Replace(separator.ToString(), string.Empty, StringComparison.Ordinal);
            }
            else if (integral && grouped)
            {
                cleaned = cleaned.Replace(separator.ToString(), string.Empty, StringComparison.Ordinal);
            }
            else if (separator == ',')
            {
                cleaned = cleaned.Replace(',', '.');
            }
        }

        if (!decimal.TryParse(
                cleaned,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out value))
        {
            return false;
        }

        return !integral || value == decimal.Truncate(value);
    }

    private static ParseOutcome ParseBoolean(ColumnSchema column, string text) => text.ToUpperInvariant() switch
    {
        "TRUE" or "T" or "YES" or "Y" or "1" or "SI" or "SÍ" or "VERDADERO" => ParseOutcome.Success(true),
        "FALSE" or "F" or "NO" or "N" or "0" or "FALSO" => ParseOutcome.Success(false),
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

    /// <summary>
    /// Quita lo que los exports agregan alrededor del número: espacios (también los no
    /// separables, que algunas configuraciones usan como separador de miles), porcentaje y
    /// símbolo de moneda.
    /// </summary>
    private static string Clean(string text)
    {
        var cleaned = new System.Text.StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character) && character is not ('%' or '$' or '€'))
            {
                cleaned.Append(character);
            }
        }

        return cleaned.ToString();
    }
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
