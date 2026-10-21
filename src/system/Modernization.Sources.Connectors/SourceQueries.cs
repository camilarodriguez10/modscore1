using System.Text.RegularExpressions;
using Modernization.Domain;

namespace Modernization.Sources.Connectors;

/// <summary>
/// La consulta de cada fuente, validada por forma (ADR 0037).
/// </summary>
/// <remarks>
/// La consulta la escribe quien despliega, no quien usa el sitio, y corre con una cuenta de
/// solo lectura dentro de una transacción que nunca se confirma: esos son los controles de
/// verdad. Esto agrega uno más barato, por si una cuenta tiene más permisos de los debidos:
/// una sola sentencia, que empieza por <c>SELECT</c> o <c>WITH</c>, sin <c>INTO</c> ni
/// <c>FOR UPDATE</c>, y un nombre de tabla que es solo un identificador.
/// </remarks>
public static partial class SourceQueries
{
    /// <summary>Tope del texto de una consulta.</summary>
    public const int MaxQueryLength = 20_000;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$#]*(\.[A-Za-z_][A-Za-z0-9_$#]*){0,2}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"^\s*(SELECT|WITH)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 100)]
    private static partial Regex StartsAsRead();

    [GeneratedRegex(@"\bINTO\b|\bFOR\s+UPDATE\b|--|/\*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
    private static partial Regex Forbidden();

    /// <summary>La sentencia que se ejecuta para una fuente.</summary>
    /// <exception cref="ConnectorException">La fuente no tiene consulta válida.</exception>
    public static string StatementFor(string source, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var hasTable = !string.IsNullOrWhiteSpace(options.Table);
        var hasQuery = !string.IsNullOrWhiteSpace(options.Query);

        if (hasTable == hasQuery)
        {
            throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorQueryInvalid, ("source", source)),
                $"The source '{source}' needs exactly one of Table or Query.");
        }

        if (hasTable)
        {
            var table = options.Table!.Trim();

            if (!Identifier().IsMatch(table))
            {
                throw new ConnectorException(
                    Notice.Of(NoticeCodes.ConnectorQueryInvalid, ("source", source)),
                    $"The Table of '{source}' must be a plain name such as SCHEMA.VIEW_NAME.");
            }

            return "SELECT * FROM " + table;
        }

        var query = options.Query!.Trim();

        if (query.EndsWith(';'))
        {
            query = query[..^1].TrimEnd();
        }

        if (query.Length > MaxQueryLength
            || !StartsAsRead().IsMatch(query)
            || query.Contains(';', StringComparison.Ordinal)
            || Forbidden().IsMatch(query))
        {
            throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorQueryInvalid, ("source", source)),
                $"The Query of '{source}' must be a single read-only SELECT statement, without comments, INTO or FOR UPDATE.");
        }

        return query;
    }
}
