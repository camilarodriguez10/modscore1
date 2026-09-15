namespace Modernization.Domain.Sources;

/// <summary>
/// Entrega el portafolio completo ya normalizado al modelo canonico.
/// El resto del sistema depende de esta interfaz, no de los readers: cambiar de
/// Dummy a File o a Database es cambiar que implementacion se registra en el
/// contenedor de dependencias.
/// </summary>
public interface IApplicationSnapshotProvider
{
    /// <summary>Adaptador activo: Dummy, File o Database.</summary>
    string Provider { get; }

    /// <summary>
    /// Versiones disponibles, de la mas reciente a la mas antigua. Un adaptador
    /// que no maneje historia devuelve una sola.
    /// </summary>
    Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Portafolio en una version. Null o vacio significa la version actual.
    /// </summary>
    Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default);

    /// <summary>Portafolio en la version actual.</summary>
    Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
        => GetAllAsync(null, cancellationToken);
}

/// <summary>
/// Portafolio cargado mas el estado de cada fuente que lo alimento.
/// </summary>
public sealed record SnapshotSet
{
    public required string Provider { get; init; }

    public required IReadOnlyList<ApplicationSnapshot> Applications { get; init; }

    /// <summary>Estado por fuente, para la pantalla de Data Lineage.</summary>
    public required IReadOnlyList<SourceStatus> Sources { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Version a la que corresponde este portafolio.</summary>
    public required SnapshotVersion Version { get; init; }

    public ApplicationSnapshot? Find(string eaiCode)
        => Applications.FirstOrDefault(a => string.Equals(a.EaiCode, eaiCode, StringComparison.Ordinal));

    /// <summary>
    /// Cobertura de una fuente: que porcentaje del portafolio recibio dato de ella.
    /// Una cobertura baja explica por que muchos scores tienen el denominador reducido.
    /// </summary>
    public decimal CoverageOf(string sourceName)
    {
        if (Applications.Count == 0)
        {
            return 0m;
        }

        var source = Sources.FirstOrDefault(s => string.Equals(s.SourceName, sourceName, StringComparison.Ordinal));
        return source is null
            ? 0m
            : Math.Round((decimal)source.MatchedApplications / Applications.Count * 100m, 1);
    }
}
