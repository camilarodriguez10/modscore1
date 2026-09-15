using System.Text;
using Modernization.Domain;

namespace Modernization.Sources.Files;

/// <summary>
/// Decide a que fuente corresponde un archivo entrante.
/// </summary>
/// <remarks>
/// Hasta la Fase 09 esto se resolvia por el nombre, lo que obligaba a renombrar
/// cada export antes de subirlo. Desde la Fase 10 se resuelve <b>por la firma del
/// encabezado</b>: cada contrato ya declara sus columnas y sus alias, asi que un
/// archivo se reconoce por lo que trae dentro.
/// <para>
/// Esto no relaja ningun control, lo aprieta. Lo que se escribe en disco sigue
/// siendo el nombre canonico del contrato y el nombre del cliente sigue sin usarse
/// jamas como ruta (ADR 0010); reconocer por contenido es mas estricto que confiar
/// en una cadena que el cliente controla por completo.
/// </para>
/// <para>
/// El orden de resolucion es deliberado: <b>asignacion explicita</b> del usuario,
/// luego <b>firma del encabezado</b>, y solo si el encabezado no distingue, el
/// nombre. Cuando nada resuelve, se devuelve el encabezado leido para que la
/// interfaz pueda preguntar en lugar de rechazar sin explicacion.
/// </para>
/// </remarks>
public sealed class SourceFileMatcher(SourceSchemaCatalog? catalog = null)
{
    /// <summary>
    /// Minimo de columnas del contrato que un encabezado debe traer para
    /// identificarlo. Con una sola —siempre la clave, que casi todas comparten— el
    /// archivo no esta identificado, esta adivinado.
    /// </summary>
    private const int MinimumSignatureColumns = 2;

    /// <summary>Cuantas columnas del encabezado se devuelven al usuario.</summary>
    private const int MaxReportedColumns = 40;

    /// <summary>Largo maximo de un nombre de columna devuelto.</summary>
    private const int MaxColumnNameLength = 64;

    private readonly SourceSchemaCatalog _catalog = catalog ?? SourceSchemaCatalog.Default;

    /// <summary>
    /// Resuelve la fuente de un archivo ya escrito en disco. El encabezado se lee
    /// del archivo, no del nombre.
    /// </summary>
    public SourceFileMatch MatchFile(string path, string clientFileName, string? assignedFileName)
    {
        using var reader = new StreamReader(path);

        return MatchReader(reader, clientFileName, assignedFileName);
    }

    /// <summary>
    /// Resuelve la fuente leyendo el encabezado de un lector. Un archivo que ni
    /// siquiera tiene encabezado no es un fallo del servicio: es un archivo que no
    /// se puede identificar, y se reporta como tal.
    /// </summary>
    public SourceFileMatch MatchReader(TextReader reader, string clientFileName, string? assignedFileName)
    {
        IReadOnlyList<string> headers;

        try
        {
            headers = CsvSourceReader.ReadHeader(reader);
        }
        catch (SourceSchemaException ex)
        {
            return SourceFileMatch.Unresolved(
                [],
                _catalog.ExpectedFileNames,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)));
        }

        return Match(headers, clientFileName, assignedFileName);
    }

    /// <summary>Resuelve la fuente a partir del encabezado ya leido.</summary>
    public SourceFileMatch Match(
        IReadOnlyList<string> headers,
        string clientFileName,
        string? assignedFileName)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var reported = Reported(headers);

        // 1. Lo que el usuario asigno a mano gana, pero solo contra la lista blanca:
        //    un valor inventado se rechaza en lugar de convertirse en una ruta.
        if (!string.IsNullOrWhiteSpace(assignedFileName))
        {
            var assigned = _catalog.Files.FirstOrDefault(
                f => string.Equals(f.FileName, assignedFileName.Trim(), StringComparison.OrdinalIgnoreCase));

            return assigned is not null
                ? SourceFileMatch.Resolved(assigned, reported)
                : SourceFileMatch.Unresolved(
                    reported,
                    _catalog.ExpectedFileNames,
                    Notice.Of(NoticeCodes.MatchAssignmentUnknown));
        }

        // 2. La firma del encabezado.
        var scored = Score(headers);
        var viable = scored.Where(c => c.IsViable).ToList();

        if (viable.Count == 1)
        {
            return SourceFileMatch.Resolved(viable[0].Schema, reported);
        }

        // 3. Empate o nada: el nombre desempata entre los candidatos que quedaron
        //    en pie, y si no hubo ninguno, decide solo.
        var pool = viable.Count > 1 ? viable.Select(c => c.Schema).ToList() : _catalog.Files;
        var byName = MatchByName(clientFileName, pool);

        if (byName is not null)
        {
            return SourceFileMatch.Resolved(byName, reported);
        }

        return SourceFileMatch.Unresolved(
            reported,
            viable.Count > 1 ? [.. viable.Select(c => c.Schema.FileName)] : _catalog.ExpectedFileNames,
            viable.Count > 1 ? Notice.Of(NoticeCodes.MatchAmbiguous) : Notice.Of(NoticeCodes.MatchUnrecognized));
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Puntua cada contrato contra el encabezado. Un contrato es viable si estan
    /// todas sus columnas obligatorias y aporta firma suficiente; entre los viables
    /// gana el que reconoce mas columnas y deja menos sin reconocer.
    /// </summary>
    private List<Candidate> Score(IReadOnlyList<string> headers)
    {
        var candidates = new List<Candidate>(_catalog.Files.Count);

        foreach (var schema in _catalog.Files)
        {
            var matched = schema.Columns.Count(column => headers.Any(column.Matches));
            var required = schema.RequiredColumns.Count(column => headers.Any(column.Matches));
            var unknown = headers.Count(header => !schema.Columns.Any(c => c.Matches(header)));

            candidates.Add(new Candidate(
                schema,
                matched,
                unknown,
                required == schema.RequiredColumns.Count() && matched >= MinimumSignatureColumns));
        }

        if (candidates.All(c => !c.IsViable))
        {
            return candidates;
        }

        // El mejor gana solo si es estrictamente mejor: un empate es ambiguedad, y
        // elegir por el orden del catalogo seria elegir al azar con cara seria.
        var ranked = candidates
            .Where(c => c.IsViable)
            .OrderByDescending(c => c.Matched)
            .ThenBy(c => c.Unknown)
            .ToList();

        if (ranked.Count == 1 || ranked[0].Matched > ranked[1].Matched || ranked[0].Unknown < ranked[1].Unknown)
        {
            return [.. candidates.Select(c => c with { IsViable = c.Schema == ranked[0].Schema })];
        }

        // Empate real: sobreviven los empatados en la cima para que el nombre elija.
        var top = ranked.Where(c => c.Matched == ranked[0].Matched && c.Unknown == ranked[0].Unknown).ToList();

        return [.. candidates.Select(c => c with { IsViable = top.Any(t => t.Schema == c.Schema) })];
    }

    /// <summary>
    /// Empareja por nombre, como antes de la Fase 10: acepta prefijos de fecha
    /// ("0809 tech_debt.csv") porque asi llegan los exports reales.
    /// </summary>
    private static SourceSchema? MatchByName(string clientFileName, IReadOnlyList<SourceSchema> pool)
    {
        var normalized = Path.GetFileName(clientFileName ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var schema in pool)
        {
            var expected = schema.FileName.ToLowerInvariant();
            var stem = Path.GetFileNameWithoutExtension(expected);

            if (normalized == expected
                || normalized.EndsWith("/" + expected, StringComparison.Ordinal)
                || normalized.Contains(stem, StringComparison.Ordinal))
            {
                return schema;
            }
        }

        return null;
    }

    /// <summary>
    /// El encabezado que se le devuelve al usuario, acotado y limpio: es contenido
    /// que llega del cliente y termina en una pantalla.
    /// </summary>
    private static IReadOnlyList<string> Reported(IReadOnlyList<string> headers)
        => [.. headers.Take(MaxReportedColumns).Select(Clean)];

    /// <summary>
    /// Un nombre de columna listo para mostrarse: sin caracteres de control, que
    /// son lo que convierte un texto en una inyeccion de terminal o de log, y
    /// acotado para que un encabezado fabricado no infle la respuesta.
    /// </summary>
    private static string Clean(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var buffer = new StringBuilder(Math.Min(text.Length, MaxColumnNameLength));

        foreach (var character in text)
        {
            if (buffer.Length >= MaxColumnNameLength)
            {
                break;
            }

            buffer.Append(char.IsControl(character) ? ' ' : character);
        }

        return buffer.ToString();
    }

    private sealed record Candidate(SourceSchema Schema, int Matched, int Unknown, bool IsViable);
}

/// <summary>
/// Resultado de identificar un archivo. Cuando no se resuelve, lleva el encabezado
/// leido y las fuentes posibles: es lo que la interfaz necesita para preguntar.
/// </summary>
public sealed record SourceFileMatch(
    SourceSchema? Schema,
    IReadOnlyList<string> DetectedColumns,
    IReadOnlyList<string> Candidates,
    Notice? Problem)
{
    public static SourceFileMatch Resolved(SourceSchema schema, IReadOnlyList<string> detected)
        => new(schema, detected, [], null);

    public static SourceFileMatch Unresolved(
        IReadOnlyList<string> detected,
        IReadOnlyList<string> candidates,
        Notice problem)
        => new(null, detected, candidates, problem);
}
