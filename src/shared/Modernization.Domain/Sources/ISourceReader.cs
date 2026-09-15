namespace Modernization.Domain.Sources;

/// <summary>
/// Puerto de lectura de una fuente. Es el unico punto por el que entran datos al
/// sistema, y la razon por la que los requisitos 8 y 9 no tocan el motor de
/// scoring: Dummy, File y Database implementan esta misma interfaz.
/// </summary>
/// <typeparam name="TRecord">
/// Registro con la forma de la fuente, no del dominio. La traduccion al modelo
/// canonico ocurre despues, en el ensamblador.
/// </typeparam>
public interface ISourceReader<TRecord>
{
    /// <summary>Nombre logico de la fuente, estable entre adaptadores.</summary>
    string SourceName { get; }

    /// <summary>Adaptador que sirve el dato: Dummy, File o Database.</summary>
    string Provider { get; }

    Task<SourceReadResult<TRecord>> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de leer una fuente. Lleva las filas validas y las rechazadas por
/// separado: una fila mal formada no puede desaparecer en silencio ni tumbar la
/// carga completa.
/// </summary>
public sealed record SourceReadResult<TRecord>
{
    public required string SourceName { get; init; }

    public required string Provider { get; init; }

    /// <summary>De donde salio: ruta de archivo, tabla o semilla del generador.</summary>
    public required string Origin { get; init; }

    public required IReadOnlyList<TRecord> Records { get; init; }

    /// <summary>Filas descartadas, con el motivo. Vacio cuando la fuente esta sana.</summary>
    public required IReadOnlyList<SourceRejection> Rejections { get; init; }

    public required DateTimeOffset ExtractedAt { get; init; }

    public SourceStatus ToStatus(int matchedApplications) => new()
    {
        SourceName = SourceName,
        Provider = Provider,
        Origin = Origin,
        RecordCount = Records.Count,
        RejectedCount = Rejections.Count,
        Rejections = Rejections,
        ExtractedAt = ExtractedAt,
        MatchedApplications = matchedApplications,
    };
}

/// <summary>Fila descartada durante la lectura, con el motivo como codigo y datos (ADR 0017).</summary>
/// <param name="RowNumber">Numero de fila en el origen, empezando en 1.</param>
/// <param name="Reason">Que fallo, en terminos accionables.</param>
/// <param name="Key">Identificador de la fila si se pudo leer.</param>
public sealed record SourceRejection(int RowNumber, Notice Reason, string? Key = null);

/// <summary>
/// Estado de una fuente tras la carga. Alimenta la pantalla de Data Lineage:
/// que se leyo, cuanto se rechazo y a cuantas aplicaciones alcanzo.
/// </summary>
public sealed record SourceStatus
{
    public required string SourceName { get; init; }
    public required string Provider { get; init; }
    public required string Origin { get; init; }
    public required int RecordCount { get; init; }
    public required int RejectedCount { get; init; }
    public required IReadOnlyList<SourceRejection> Rejections { get; init; }
    public required DateTimeOffset ExtractedAt { get; init; }

    /// <summary>Cuantas aplicaciones del portafolio recibieron dato de esta fuente.</summary>
    public required int MatchedApplications { get; init; }
}
