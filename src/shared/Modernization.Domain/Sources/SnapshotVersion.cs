namespace Modernization.Domain.Sources;

/// <summary>
/// Una carga de datos identificada en el tiempo. El portafolio no es una foto
/// unica: cada extraccion de las fuentes produce una version, y comparar
/// versiones es lo que convierte un score en una tendencia.
/// </summary>
/// <remarks>
/// En la fase de archivos, una version es un directorio con los CSV de esa carga.
/// En la fase de base de datos pasa a ser una fila de una tabla de snapshots, con
/// las mismas propiedades. La interfaz no cambia.
/// </remarks>
public sealed record SnapshotVersion
{
    /// <summary>Identificador estable y ordenable, con forma de fecha invertida.</summary>
    public required string Id { get; init; }

    /// <summary>Momento al que corresponden los datos.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Como llamarla en pantalla, p. ej. "Carga 0809" o "Q3 2026".</summary>
    public required string Label { get; init; }

    /// <summary>Adaptador que la produjo: Dummy, File o Database.</summary>
    public required string Provider { get; init; }

    public required int ApplicationCount { get; init; }

    /// <summary>La version activa: la mas reciente, salvo que se pida otra.</summary>
    public required bool IsCurrent { get; init; }
}

/// <summary>
/// Punto de una serie temporal de score. Es lo minimo para dibujar la evolucion
/// sin traerse el portafolio completo de cada version.
/// </summary>
public sealed record ScoreHistoryPoint
{
    public required string VersionId { get; init; }
    public required string Label { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Null cuando la aplicacion no existia en esa version.</summary>
    public required decimal? Score { get; init; }

    /// <summary>Peso disponible en esa version: explica un salto sin merito real.</summary>
    public required decimal? AvailableWeight { get; init; }

    /// <summary>Currency Score de CMT en esa version. Null sin dato.</summary>
    public decimal? Currency { get; init; }

    /// <summary>Risk Score (0-100, mas alto es peor) en esa version. Null sin pilar de riesgo.</summary>
    public decimal? RiskScore { get; init; }
}
