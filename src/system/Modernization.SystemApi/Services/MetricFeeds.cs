namespace Modernization.SystemApi.Services;

/// <summary>
/// Qué métrica del ruleset usa un campo de una fuente, a partir del campo de origen que
/// declara cada métrica (<c>source.field</c>). Es lo que la pantalla de mapping muestra
/// como destino de cada valor: no solo la columna de la base, también para qué sirve.
/// </summary>
/// <remarks>
/// El ruleset nombra el campo como lo escribe el BRD, no siempre igual que el contrato:
/// <c>SVR_DSC = 1-Critical</c> es un filtro sobre <c>SVR_DSC</c>, e
/// <c>Invest/Tolerate/Eliminate/Migrate counts</c> son cuatro columnas <c>*_Count</c>.
/// Se reconocen esas tres formas y nada más: una coincidencia dudosa no se muestra.
/// </remarks>
public static class MetricFeeds
{
    private const string CountsSuffix = "counts";
    private const string CountSuffix = "_Count";

    public static bool Uses(string? field, string column)
    {
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(column))
        {
            return false;
        }

        if (string.Equals(Normalize(field), Normalize(column), StringComparison.Ordinal))
        {
            return true;
        }

        // "SVR_DSC = 1-Critical": el campo, un espacio y un filtro.
        if (field.StartsWith(column + " ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // "Invest/Tolerate/Eliminate/Migrate counts" usa Invest_Count, Tolerate_Count...
        if (field.EndsWith(CountsSuffix, StringComparison.OrdinalIgnoreCase)
            && column.EndsWith(CountSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var stem = Normalize(column[..^CountSuffix.Length]);

            return field[..^CountsSuffix.Length]
                .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(part => string.Equals(Normalize(part), stem, StringComparison.Ordinal));
        }

        return false;
    }

    /// <summary>Solo letras y dígitos, en mayúsculas: "Kenna Score" y "Kenna_Score" son lo mismo.</summary>
    private static string Normalize(string text)
        => string.Concat(text.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
