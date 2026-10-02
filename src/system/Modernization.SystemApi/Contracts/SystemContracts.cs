using Modernization.Domain;
using Modernization.Domain.Sources;

namespace Modernization.SystemApi.Contracts;

/// <summary>
/// Contratos de la capa System. Son explicitos y no reutilizan las entidades de
/// dominio: un cambio interno del modelo no puede alterar el formato del cable
/// sin que alguien lo decida aqui.
/// </summary>
/// <remarks>
/// Son canonicos, no orientados a pantalla. Darles forma de vista es trabajo de
/// la capa Channel (ADR 0001).
/// </remarks>
public sealed record ApplicationResponse(
    string EaiCode,
    string ApplicationName,
    string? ApplicationStatus,
    string? Cio,
    string? Cre,
    string? Lob,
    string? SubLob,
    string? Region,
    string? Country,
    bool? CriticalApplication,
    bool? InternetFacing,
    string? CloudIndicator,
    string? CloudType,
    string? CloudVendor,
    string? CodingLanguage,
    string? PrimaryBusinessCapability,
    string? RecoveryTimeObjectiveRange,
    string? RecoveryPointObjectiveRange,
    int? UserCount,
    string? Disposition,
    string? Roadmap,
    string? Provider,
    DateTimeOffset CapturedAt)
{
    public static ApplicationResponse From(ApplicationSnapshot s) => new(
        s.EaiCode,
        s.ApplicationName,
        s.ApplicationStatus,
        s.Cio,
        s.Cre,
        s.Lob,
        s.SubLob,
        s.Region,
        s.Country,
        s.CriticalApplicationIndicator,
        s.InternetFacing,
        s.CloudIndicator,
        s.CloudType,
        s.CloudVendor,
        s.CodingLanguage,
        s.PrimaryBusinessCapability,
        s.RecoveryTimeObjectiveRange,
        s.RecoveryPointObjectiveRange,
        s.UserCount,
        s.Disposition is { } d ? MapsNames.Display(d) : null,
        s.Roadmap is { } r ? MapsNames.Display(r) : null,
        s.Provider,
        s.CapturedAt);
}

/// <summary>Desglose completo del score, con todo lo necesario para reproducirlo a mano.</summary>
public sealed record ScoreResponse(
    string EaiCode,
    string ApplicationName,
    string RulesetVersion,
    decimal Numerator,
    decimal Denominator,
    decimal Score,
    decimal EffectiveScore,
    OverrideResponse? Override,
    IReadOnlyList<PillarResponse> Pillars,
    IReadOnlyList<MetricResponse> Metrics,
    DateTimeOffset CalculatedAt)
{
    public static ScoreResponse From(ScoreBreakdown b) => new(
        b.EaiCode,
        b.ApplicationName,
        b.RulesetVersion,
        b.Numerator,
        b.Denominator,
        b.Score,
        b.EffectiveScore,
        b.Override is null ? null : new OverrideResponse(b.Override.Value, b.Override.Reason, b.Override.AppliedBy, b.Override.AppliedAt),
        [.. b.Pillars.Select(PillarResponse.From)],
        [.. b.Metrics.Select(MetricResponse.From)],
        b.CalculatedAt);
}

public sealed record OverrideResponse(decimal Value, string Reason, string AppliedBy, DateTimeOffset AppliedAt);

public sealed record PillarResponse(
    string Key,
    string Name,
    decimal TotalWeight,
    decimal AvailableWeight,
    decimal Numerator,
    decimal? Score,
    IReadOnlyList<string> MetricKeys)
{
    public static PillarResponse From(PillarBreakdown p) =>
        new(p.Key, p.Name, p.TotalWeight, p.AvailableWeight, p.Numerator, p.Score, p.MetricKeys);
}

public sealed record MetricResponse(
    string MetricKey,
    string Name,
    string PillarKey,
    Notice Display,
    decimal? MappedScore,
    decimal Weight,
    decimal? WeightedScore,
    bool IsIncluded,
    bool IsInformational,
    Notice? Exclusion,
    string RuleApplied,
    ProvenanceResponse Provenance)
{
    public static MetricResponse From(MetricEvaluation m) => new(
        m.MetricKey,
        m.Name,
        m.PillarKey,
        m.Display,
        m.MappedScore,
        m.Weight,
        m.WeightedScore,
        m.IsIncluded,
        m.IsInformational,
        m.Exclusion,
        m.RuleApplied,
        new ProvenanceResponse(m.Provenance.System, m.Provenance.Report, m.Provenance.Field, m.Provenance.Provider, m.Provenance.ExtractedAt));
}

public sealed record ProvenanceResponse(
    string System,
    string Report,
    string Field,
    string? Provider,
    DateTimeOffset? ExtractedAt);

/// <summary>Estado de una fuente tras la carga. Alimenta la pantalla de linaje.</summary>
public sealed record SourceStatusResponse(
    string SourceName,
    string Provider,
    string Origin,
    int RecordCount,
    int RejectedCount,
    decimal CoveragePercent,
    int MatchedApplications,
    DateTimeOffset ExtractedAt,
    IReadOnlyList<RejectionResponse> Rejections)
{
    public static SourceStatusResponse From(SourceStatus s, decimal coverage) => new(
        s.SourceName,
        s.Provider,
        s.Origin,
        s.RecordCount,
        s.RejectedCount,
        coverage,
        s.MatchedApplications,
        s.ExtractedAt,
        // Se acotan: un archivo desalineado puede generar miles y la respuesta
        // no deberia crecer sin limite.
        [.. s.Rejections.Take(50).Select(r => new RejectionResponse(r.RowNumber, r.Reason, r.Key))]);
}

public sealed record RejectionResponse(int RowNumber, Notice Reason, string? Key);

/// <summary>Definicion del ruleset activo, para la pantalla de administracion.</summary>
public sealed record RulesetResponse(
    string Version,
    string? Description,
    decimal Floor,
    decimal Range,
    IReadOnlyList<RulesetPillarResponse> Pillars,
    IReadOnlyList<RulesetMetricResponse> Metrics);

public sealed record RulesetPillarResponse(string Key, string Name, decimal NominalWeight);

public sealed record RulesetMetricResponse(
    string Key,
    string Name,
    string Pillar,
    decimal Weight,
    string SourceSystem,
    string SourceReport,
    string SourceField,
    string MappingKind,
    string? Note);

/// <summary>Pagina de resultados. Toda coleccion se pagina; ninguna devuelve todo.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>Una carga de datos identificada en el tiempo.</summary>
public sealed record VersionResponse(
    string Id,
    string Label,
    DateTimeOffset CapturedAt,
    string Provider,
    int ApplicationCount,
    bool IsCurrent)
{
    public static VersionResponse From(Domain.Sources.SnapshotVersion v) =>
        new(v.Id, v.Label, v.CapturedAt, v.Provider, v.ApplicationCount, v.IsCurrent);
}

/// <summary>Punto de la serie temporal de score.</summary>
public sealed record HistoryPointResponse(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? AvailableWeight)
{
    public static HistoryPointResponse From(Domain.Sources.ScoreHistoryPoint p) =>
        new(p.VersionId, p.Label, p.CapturedAt, p.Score, p.AvailableWeight);
}

/// <summary>Resultado de una carga, archivo por archivo.</summary>
public sealed record UploadResponse(
    bool Accepted,
    VersionResponse? Version,
    int ApplicationCount,
    IReadOnlyList<UploadFileResponse> Files,
    IReadOnlyList<Notice> Errors)
{
    public static UploadResponse From(Domain.Sources.PublishOutcome o) => new(
        o.Accepted,
        o.Version is null ? null : VersionResponse.From(o.Version),
        o.ApplicationCount,
        [.. o.Files.Select(f => new UploadFileResponse(
            f.ClientFileName,
            f.CanonicalFileName,
            f.Source,
            f.Bytes,
            f.RowCount,
            f.RejectedCount,
            f.Error,
            f.DetectedColumns,
            f.Candidates,
            [.. f.RejectionReasons.Select(r => new RejectionReasonResponse(r.Reason, r.Rows, r.SampleRows))],
            [.. f.IgnoredValues.Select(r => new RejectionReasonResponse(r.Reason, r.Rows, r.SampleRows))]))],
        o.Errors);
}

public sealed record UploadFileResponse(
    string ClientFileName,
    string? CanonicalFileName,
    string Source,
    long Bytes,
    int RowCount,
    int RejectedCount,
    Notice? Error,

    // El encabezado leido y las fuentes posibles, cuando no se pudo identificar:
    // es lo que la interfaz necesita para preguntar en lugar de solo rechazar.
    IReadOnlyList<string> DetectedColumns,
    IReadOnlyList<string> Candidates,

    // Por qué quedaron filas fuera, agrupado por problema y acotado por el lector.
    IReadOnlyList<RejectionReasonResponse>? RejectionReasons = null,

    // Valores de campos opcionales que quedaron sin dato; esas filas sí entraron.
    IReadOnlyList<RejectionReasonResponse>? IgnoredValues = null);

/// <summary>Un problema de las filas rechazadas: aviso de ejemplo, cuántas filas y las primeras.</summary>
public sealed record RejectionReasonResponse(Notice Reason, int Rows, IReadOnlyList<int> SampleRows);

/// <summary>
/// Pedido de revisión del mapping antes de cargar. Solo viaja el encabezado de cada
/// archivo: el navegador lee la primera línea y ninguna fila de datos sale del equipo.
/// </summary>
public sealed record MappingPreviewRequest(IReadOnlyList<MappingPreviewFileRequest>? Files);

/// <param name="FileName">Nombre del archivo en el equipo; solo se muestra.</param>
/// <param name="Header">La primera línea del archivo.</param>
/// <param name="AssignedFileName">Fuente elegida a mano, si el encabezado no alcanza.</param>
/// <param name="Columns">Decisiones ya tomadas: campo a columna del archivo, o a null.</param>
public sealed record MappingPreviewFileRequest(
    string? FileName,
    string? Header,
    string? AssignedFileName,
    IReadOnlyDictionary<string, string?>? Columns);

public sealed record MappingPreviewResponse(IReadOnlyList<MappingPreviewFileResponse> Files);

/// <summary>El mapping propuesto para un archivo, campo por campo.</summary>
public sealed record MappingPreviewFileResponse(
    string FileName,
    string? CanonicalFileName,
    string Source,
    string? Description,
    Notice? Problem,
    IReadOnlyList<string> Candidates,
    IReadOnlyList<string> Headers,
    IReadOnlyList<MappingFieldResponse> Fields,
    IReadOnlyList<string> UnusedHeaders,
    IReadOnlyList<Notice> Problems);

/// <summary>
/// De dónde sale cada campo de la base, fuente por fuente, sin elegir archivos: los nombres
/// de columna que acepta cada campo y la columna que lo cargó en la carga vigente.
/// <c>Recorded</c> es false si la carga vigente es anterior al registro del mapping.
/// </summary>
public sealed record MappingCatalogResponse(bool Recorded, IReadOnlyList<MappingSourceResponse> Sources);

public sealed record MappingSourceResponse(
    string Source,
    string FileName,
    string? Description,
    string KeyColumn,
    IReadOnlyList<MappingCatalogFieldResponse> Fields);

/// <summary>
/// Un campo de una fuente. <c>AcceptedNames</c> son el nombre y los alias que se reconocen
/// solos; <c>CurrentHeader</c> y <c>CurrentManual</c>, la columna que lo cargó en la carga
/// vigente y si la eligió una persona (null los dos si no quedó registrado).
/// </summary>
public sealed record MappingCatalogFieldResponse(
    string Column,
    string Type,
    bool Required,
    bool IsKey,
    IReadOnlyList<string> AcceptedNames,
    string? CurrentHeader,
    bool? CurrentManual,
    IReadOnlyList<string> Feeds);

/// <summary>
/// Un campo de la base y la columna del archivo que lo cargaría. <c>Match</c> dice cómo
/// se decidió —name, alias, manual, remembered, excluded o missing— y <c>Feeds</c>, qué
/// métricas del ruleset vigente usan el campo.
/// </summary>
public sealed record MappingFieldResponse(
    string Column,
    string Type,
    bool Required,
    bool IsKey,
    string? Header,
    string Match,
    IReadOnlyList<string> Feeds);

/// <summary>
/// Que adaptador esta activo y si admite cargas. Lo declara la capa System: no
/// se deduce de los datos, porque sin ninguna carga no hay datos de donde
/// deducirlo — y ese es justamente el momento en que la carga tiene que estar
/// disponible.
/// </summary>
public sealed record IngestionCapabilitiesResponse(string Provider, bool UploadEnabled);

/// <summary>Un archivo que la carga espera, con sus columnas obligatorias.</summary>
public sealed record ExpectedFileResponse(
    string Source,
    string FileName,
    string? Description,
    string KeyColumn,
    IReadOnlyList<string> RequiredColumns,
    IReadOnlyList<string> OptionalColumns);
