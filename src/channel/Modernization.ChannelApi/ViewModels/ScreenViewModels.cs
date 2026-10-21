using Modernization.Domain;
namespace Modernization.ChannelApi.ViewModels;

/// <summary>
/// Modelos de vista de la capa Channel. Tienen la forma de una pantalla, no del
/// dominio: agregan, ordenan y calculan lo que la UI necesita para pintar sin
/// hacer aritmetica propia.
/// </summary>
/// <remarks>
/// Ninguna regla de negocio vive aqui. Todo lo que se muestra viene ya calculado
/// por la capa System; esta capa solo lo reordena para la pantalla (ADR 0001).
/// </remarks>
public sealed record SessionView(
    string Name,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsAuthenticated,
    SessionPermissionsView Permissions,
    SessionFeaturesView Features);

/// <summary>
/// Lo que la sesion permite, ya resuelto. La SPA lo usa para no ofrecer lo que el
/// servidor va a negar; la autorizacion real sigue en el servidor.
/// </summary>
public sealed record SessionPermissionsView(bool CanRead, bool FullPortfolio, bool CanUpload, bool CanAdministerRules);

/// <summary>
/// Las pantallas encendidas en la capa Channel (<see cref="ChannelFeatures"/>). La SPA no ofrece
/// las apagadas, y sus endpoints responden 404 igual.
/// </summary>
public sealed record SessionFeaturesView(bool Code, bool PortfolioRoadmap);

// -------------------------------------------------------------- 01 Portfolio

public sealed record PortfolioView(
    int TotalApplications,
    decimal AverageScore,
    decimal MedianScore,
    IReadOnlyList<ScoreBandView> Distribution,
    IReadOnlyList<GroupSummaryView> ByLob,
    IReadOnlyList<GroupSummaryView> ByCountry,
    IReadOnlyList<GroupSummaryView> ByCio,
    IReadOnlyList<GroupSummaryView> ByDisposition,
    IReadOnlyList<GroupSummaryView> ByRoadmap,
    IReadOnlyList<GroupSummaryView> ByCre,
    IReadOnlyList<GroupSummaryView> ByRegion,
    IReadOnlyList<GroupSummaryView> ByStatus,
    IReadOnlyList<GroupSummaryView> ByCapability,
    IReadOnlyList<PortfolioRowView> Applications,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PortfolioRowView(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    string? Cio,
    string? Cre,
    bool? CriticalApplication,
    string? Disposition,
    string? Roadmap,
    decimal Score,
    string Band,
    decimal AvailableWeight);

/// <summary>Tramo de score. El color lo elige la UI; el nombre del tramo, esta capa.</summary>
public sealed record ScoreBandView(string Band, string Range, int Count, decimal Percent);

/// <summary>
/// Un agregado del portafolio. La clave es tambien el valor del filtro: hacer clic
/// en "Mexico" envia exactamente esa clave de vuelta, sin traducir nada.
/// </summary>
public sealed record GroupSummaryView(string Key, int Count, decimal AverageScore);

// --------------------------------------------------------- 02 Score Overview

public sealed record ScoreOverviewView(
    ApplicationHeaderView Application,
    decimal Score,
    string Band,
    string RulesetVersion,
    decimal Numerator,
    decimal Denominator,
    IReadOnlyList<PillarCardView> Pillars,
    OverrideView? Override,
    DateTimeOffset CalculatedAt);

public sealed record ApplicationHeaderView(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? SubLob,
    string? Country,
    string? Region,
    string? Cio,
    string? Cre,
    bool? CriticalApplication,
    string? Disposition,
    string? Roadmap,
    string? CodingLanguage,
    int? UserCount);

/// <summary>
/// Tarjeta de pilar del dashboard v1.3: valor crudo, x peso, ponderado, y el peso
/// disponible frente al nominal.
/// </summary>
public sealed record PillarCardView(
    string Key,
    string Name,
    decimal? Score,
    decimal TotalWeight,
    decimal AvailableWeight,
    decimal Numerator,
    int MetricsIncluded,
    int MetricsExcluded,
    IReadOnlyList<PillarMetricView> Metrics);

public sealed record PillarMetricView(
    string MetricKey,
    string Name,
    Notice RawValue,
    decimal? MappedScore,
    decimal Weight,
    decimal? WeightedScore,
    bool IsIncluded);

public sealed record OverrideView(decimal Value, string Reason, string AppliedBy, DateTimeOffset AppliedAt);

// ------------------------------------------------------- 03 Como se calculo

/// <summary>
/// Pantalla de explicabilidad. Es el requisito 4 del encargo y la razon por la
/// que el motor produce un desglose completo en lugar de solo un numero.
/// </summary>
public sealed record ExplainView(
    ApplicationHeaderView Application,
    FormulaView Formula,
    IReadOnlyList<WaterfallStepView> Waterfall,
    IReadOnlyList<ExplainMetricView> Included,
    IReadOnlyList<ExcludedMetricView> Excluded,
    IReadOnlyList<ExplainMetricView> Informational,
    DateTimeOffset CalculatedAt);

/// <summary>La formula del BRD con los numeros reales ya sustituidos.</summary>
public sealed record FormulaView(
    string Expression,
    decimal Floor,
    decimal Range,
    decimal Numerator,
    decimal Denominator,
    decimal Score,
    string RulesetVersion,
    IReadOnlyList<Notice> Steps);

/// <summary>Aporte acumulado de cada metrica, ordenado de mayor a menor.</summary>
public sealed record WaterfallStepView(
    string MetricKey,
    string Name,
    string PillarKey,
    decimal WeightedScore,
    decimal Cumulative,
    decimal PointsOfFinalScore);

public sealed record ExplainMetricView(
    string MetricKey,
    string Name,
    string PillarKey,
    Notice RawValue,
    string RuleApplied,
    decimal? MappedScore,
    decimal Weight,
    decimal? WeightedScore,
    ProvenanceView Provenance);

public sealed record ExcludedMetricView(
    string MetricKey,
    string Name,
    string PillarKey,
    decimal Weight,
    Notice Reason,
    ProvenanceView Provenance);

public sealed record ProvenanceView(
    string System,
    string Report,
    string Field,
    string? Provider,
    DateTimeOffset? ExtractedAt);

// ------------------------------------------------------------ 07 Data Lineage

public sealed record LineageView(
    string Provider,
    int TotalApplications,
    IReadOnlyList<LineageSourceView> Sources,
    DateTimeOffset CapturedAt,

    // La carga que se esta mirando: el linaje es por carga, no del sistema.
    string VersionId,
    string VersionLabel);

public sealed record LineageSourceView(
    string SourceName,
    string Origin,
    int RecordCount,
    int MatchedApplications,
    decimal CoveragePercent,
    int RejectedCount,
    IReadOnlyList<LineageRejectionView> Rejections,
    DateTimeOffset ExtractedAt);

public sealed record LineageRejectionView(int RowNumber, Notice Reason, string? Key);

// ------------------------------------------------------------- 04 What-If Lab

/// <summary>
/// Pantalla del What-If Lab. Es el requisito 5 del encargo: que sube el score y
/// cuanto, expresado en acciones que un equipo puede ejecutar de verdad.
/// </summary>
public sealed record WhatIfLabView(
    ApplicationHeaderView Application,
    decimal BaselineScore,
    decimal BaselineWeight,
    decimal CeilingScore,
    decimal Headroom,
    IReadOnlyList<LeverView> QuickWins,
    IReadOnlyList<LeverView> Actionable,
    IReadOnlyList<LeverView> AlreadyAchieved,
    IReadOnlyList<LeverView> NotActionable);

public sealed record LeverView(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter,
    decimal Delta,
    decimal ProjectedScore,
    decimal WeightAdded,
    bool IsAlreadyAchieved,
    Notice? NotActionableReason);

/// <summary>Resultado de simular un conjunto de palancas.</summary>
public sealed record ScenarioView(
    decimal BaselineScore,
    decimal ProjectedScore,
    decimal Delta,
    decimal BaselineWeight,
    decimal ProjectedWeight,
    decimal SumOfIndividualDeltas,
    decimal InteractionEffect,
    IReadOnlyList<AppliedLeverView> Applied,
    IReadOnlyList<LeverView> IndividualEffects,
    Notice? InteractionNote);

public sealed record AppliedLeverView(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter);

/// <summary>Resultado de pedir un score objetivo.</summary>
public sealed record TargetView(
    decimal Target,
    decimal BaselineScore,
    decimal CeilingScore,
    bool IsReachable,
    bool IsAlreadyMet,
    ScenarioView? Scenario,
    Notice Explanation);

// ---------------------------------------------------------------- 05 Roadmap

/// <summary>
/// Plan futuro de una aplicacion. Es el requisito 6 del encargo: que le va a
/// pasar a esta aplicacion, por que pasos y con que resultado.
/// </summary>
public sealed record RoadmapPlanView(
    ApplicationHeaderView Application,
    string? Disposition,
    string? DispositionDefinition,
    string? Roadmap,
    string? RoadmapDefinition,
    string? LeverRationale,
    bool LeavesPortfolio,
    decimal BaselineScore,
    string BaselineBand,
    decimal BaselineWeight,
    decimal? ProjectedScore,
    string? ProjectedBand,
    decimal ProjectedDelta,
    IReadOnlyList<AppliedLeverView> DeliveredBy,
    IReadOnlyList<MilestoneView> Milestones,
    IReadOnlyList<AlertView> Alerts,
    DateOnly StartDate,
    DateOnly? CompletionDate,
    int TotalWeeks,
    Notice Outlook);

public sealed record MilestoneView(
    int Order,
    string Name,
    string Owner,
    int Weeks,
    DateOnly Start,
    DateOnly End,
    decimal SharePercent);

public sealed record AlertView(
    string RuleKey,
    string Severity,
    string Title,
    string Detail,
    Notice Evidence);

/// <summary>Pantalla 06 — el portafolio visto por su plan futuro.</summary>
public sealed record PortfolioRoadmapView(
    int TotalApplications,
    int WithoutDisposition,
    int LeavingPortfolio,
    IReadOnlyList<DispositionGroupView> Dispositions,
    IReadOnlyList<AlertSummaryView> Alerts,

    // Por que dimension se agrupo, y el portafolio partido por ella.
    string GroupBy,
    IReadOnlyList<RoadmapGroupView> Groups);

/// <summary>
/// Un grupo del portafolio con su plan agregado: donde esta hoy, a donde llegaria y
/// cuanto de el se apaga en el camino.
/// </summary>
public sealed record RoadmapGroupView(
    string Key,
    string Name,
    int Count,
    decimal SharePercent,
    decimal AverageScore,
    string Band,
    decimal? ProjectedScore,
    string? ProjectedBand,
    decimal? Delta,
    int LeavingPortfolio,
    int WithoutDisposition,
    int AverageWeeks,
    int Alerts,
    IReadOnlyList<RoadmapMixView> Mix);

/// <summary>De que roadmaps esta hecho un grupo. Es lo que explica su promedio.</summary>
public sealed record RoadmapMixView(string Roadmap, string Name, int Count);

/// <summary>
/// Una disposition con sus roadmaps dentro. MAPS solo define siete pares validos,
/// asi que agrupar por disposition dice mas que una matriz 3x7 casi vacia.
/// </summary>
public sealed record DispositionGroupView(
    string Disposition,
    string Name,
    string Definition,
    int Count,
    decimal SharePercent,
    decimal AverageScore,
    IReadOnlyList<RoadmapRowView> Roadmaps);

public sealed record RoadmapRowView(
    string Roadmap,
    string Name,
    string Definition,
    int Count,
    decimal AverageScore,
    decimal? AverageProjectedScore,
    decimal? AverageDelta,
    int TotalWeeks,
    bool RemovesFromPortfolio);

public sealed record AlertSummaryView(
    string RuleKey,
    string Severity,
    string Title,
    int Count,
    IReadOnlyList<string> SampleEaiCodes);

// ------------------------------------------------------------- 08 Cargas

/// <summary>Una carga de datos identificada en el tiempo.</summary>
public sealed record VersionView(
    string Id,
    string Label,
    DateTimeOffset CapturedAt,
    int ApplicationCount,
    bool IsCurrent);

/// <summary>Punto de la evolucion del score.</summary>
public sealed record HistoryPointView(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? AvailableWeight,
    decimal? DeltaFromPrevious);

/// <summary>
/// Pantalla de cargas: que archivos se esperan, que versiones existen y como
/// evoluciona el portafolio.
/// </summary>
public sealed record IngestionView(
    bool UploadEnabled,
    string Provider,
    Notice? DisabledReason,
    IReadOnlyList<ExpectedFileView> ExpectedFiles,
    IReadOnlyList<VersionView> Versions,
    IReadOnlyList<HistoryPointView> PortfolioHistory,
    long MaxFileBytes,
    long MaxTotalBytes);

public sealed record ExpectedFileView(
    string Source,
    string FileName,
    string? Description,
    string KeyColumn,
    IReadOnlyList<string> RequiredColumns,
    int OptionalColumnCount);

/// <summary>Resultado de una carga, archivo por archivo.</summary>
public sealed record UploadResultView(
    bool Accepted,
    VersionView? Version,
    int ApplicationCount,
    IReadOnlyList<UploadFileView> Files,
    IReadOnlyList<Notice> Errors,
    Notice Summary);

public sealed record UploadFileView(
    string FileName,
    string? CanonicalFileName,
    string Source,
    long Bytes,
    int RowCount,
    int RejectedCount,
    Notice? Error,

    // El encabezado leido y las fuentes posibles, cuando no se pudo identificar.
    IReadOnlyList<string> DetectedColumns,
    IReadOnlyList<string> Candidates,

    // Por qué quedaron filas fuera, agrupado por problema.
    IReadOnlyList<UploadRejectionView> RejectionReasons,

    // Valores de campos opcionales que quedaron sin dato; esas filas sí entraron.
    IReadOnlyList<UploadRejectionView> IgnoredValues);

/// <summary>
/// Un problema de las filas rechazadas de un archivo: el aviso de la primera fila que lo
/// tuvo, cuántas filas lo tienen y los números de las primeras.
/// </summary>
public sealed record UploadRejectionView(Notice Reason, int Rows, IReadOnlyList<int> SampleRows);

/// <summary>
/// La pestaña Mapping: de dónde sale cada campo de la base, fuente por fuente. <c>Recorded</c>
/// es false si la carga vigente es anterior al registro del mapping.
/// </summary>
public sealed record MappingCatalogView(bool Recorded, IReadOnlyList<MappingSourceView> Sources);

public sealed record MappingSourceView(
    string Source,
    string FileName,
    string? Description,
    string KeyColumn,
    IReadOnlyList<MappingCatalogFieldView> Fields);

/// <summary>
/// Un campo: los nombres de columna que se reconocen solos y la columna que lo cargó en la
/// carga vigente (null los dos si no quedó registrado).
/// </summary>
public sealed record MappingCatalogFieldView(
    string Column,
    string Type,
    bool Required,
    bool IsKey,
    IReadOnlyList<string> AcceptedNames,
    string? CurrentHeader,
    bool? CurrentManual,
    IReadOnlyList<string> Feeds);

/// <summary>El mapping de columnas propuesto antes de cargar, archivo por archivo.</summary>
public sealed record MappingReviewView(IReadOnlyList<MappingFileView> Files);

/// <summary>
/// Un archivo en la revisión del mapping: la fuente reconocida (o por qué no) y qué columna
/// cargaría cada campo. <c>Headers</c> son las columnas del archivo, para elegir.
/// </summary>
public sealed record MappingFileView(
    string FileName,
    string? CanonicalFileName,
    string Source,
    string? Description,
    Notice? Problem,
    IReadOnlyList<string> Candidates,
    IReadOnlyList<string> Headers,
    IReadOnlyList<MappingFieldView> Fields,
    IReadOnlyList<string> UnusedHeaders,
    IReadOnlyList<Notice> Problems);

/// <summary>
/// Un campo de la base y la columna del archivo que lo cargaría. <c>Match</c>: name,
/// alias, manual, remembered, excluded o missing. <c>Feeds</c>: métricas que lo usan.
/// </summary>
public sealed record MappingFieldView(
    string Column,
    string Type,
    bool Required,
    bool IsKey,
    string? Header,
    string Match,
    IReadOnlyList<string> Feeds);
