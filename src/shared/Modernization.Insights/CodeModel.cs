namespace Modernization.Insights;

/// <summary>
/// La pantalla Code sobre el portafolio filtrado: obsolescencia y riesgo según lo que
/// declaran los repositorios de cada aplicación (ADR 0026).
/// </summary>
/// <remarks><c>AsOf</c> es la fecha contra la que se evalúa cada fin de vida.</remarks>
public sealed record CodePortfolio(
    DateOnly AsOf,
    CodeKpis Kpis,
    IReadOnlyList<CodeStatusShare> Statuses,
    IReadOnlyList<CodeLifecycleRow> Lifecycle,
    IReadOnlyList<CodeLibraryRow> Libraries,
    IReadOnlyList<CodeLevelShare> RiskLevels,
    IReadOnlyList<CodeApplication> Applications);

/// <summary>
/// Los indicadores. Los componentes se cuentan por uso: el mismo Node 14 en tres
/// repositorios son tres componentes, porque son tres cosas que actualizar.
/// </summary>
public sealed record CodeKpis(
    int Applications,
    int ApplicationsWithRepositories,
    int Repositories,
    int Components,
    int EolComponents,
    int EolApplications,
    int ApproachingComponents,
    int ApproachingApplications,
    int UnknownComponents,
    int CriticalAlerts,
    int HighAlerts,
    int VulnerableApplications,
    int FlaggedLibraries,
    int HighRiskApplications,
    int MismatchApplications);

/// <summary>Cuántos componentes hay en cada estado y en cuántas aplicaciones aparece al menos uno.</summary>
public sealed record CodeStatusShare(string Status, int Components, int Applications);

/// <summary>Un ciclo de un producto en uso, con sus fechas y dónde aparece.</summary>
/// <remarks>
/// Sin coincidencia en el catálogo, <c>Cycle</c> es la versión tal como la declara el
/// repositorio y el estado es <c>unknown</c>.
/// </remarks>
public sealed record CodeLifecycleRow(
    string Product,
    string ProductName,
    string Kind,
    string Cycle,
    string? CycleLabel,
    DateOnly? Eol,
    DateOnly? SupportEnds,
    DateOnly? ExtendedSupportEnds,
    bool Discontinued,
    string Status,
    int? MonthsToEol,
    int Repositories,
    int Applications,
    string? SourceUrl);

/// <summary>Una biblioteca con algún hallazgo: obsoleta, desactualizada o con alertas.</summary>
public sealed record CodeLibraryRow(
    string Ecosystem,
    string Name,
    IReadOnlyList<string> Versions,
    string? Latest,
    int? MajorsBehind,
    bool Deprecated,
    bool Outdated,
    int Critical,
    int High,
    int Repositories,
    int Applications);

/// <summary>Aplicaciones por nivel de riesgo de código; <c>none</c> son las que no tienen repositorios.</summary>
public sealed record CodeLevelShare(string Level, int Applications, decimal Share);

/// <summary>Una aplicación vista desde su código.</summary>
/// <remarks>
/// <c>RiskIndex</c> y <c>RiskLevel</c> son null sin repositorios: no es riesgo bajo, es
/// que no se ve. <c>Mismatch</c> dice cuándo el código y el Currency Score de CMT no
/// cuentan la misma historia.
/// </remarks>
public sealed record CodeApplication(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    bool Critical,
    decimal? Currency,
    bool Legacy,
    int Repositories,
    int Components,
    int EolComponents,
    int ApproachingComponents,
    int UnknownComponents,
    int CriticalAlerts,
    int HighAlerts,
    int DeprecatedLibraries,
    int OutdatedLibraries,
    int StaleRepositories,
    decimal? RiskIndex,
    string? RiskLevel,
    string? WorstStatus,
    string? Mismatch);

/// <summary>Una aplicación con sus repositorios y de dónde sale cada punto de su índice.</summary>
public sealed record CodeApplicationDetail(
    CodeApplication Application,
    IReadOnlyList<CodeRiskFactor> Factors,
    IReadOnlyList<CodeRepository> Repositories);

/// <summary>Un hallazgo del índice: cuántas veces aparece, cuánto vale cada una y el total.</summary>
public sealed record CodeRiskFactor(string Key, int Count, decimal Points, decimal Total);

public sealed record CodeRepository(
    string Name,
    string DefaultBranch,
    string? PrimaryLanguage,
    DateOnly? LastCommit,
    bool Archived,
    bool Stale,
    IReadOnlyList<CodeComponent> Components,
    IReadOnlyList<CodeDependency> Dependencies);

public sealed record CodeComponent(
    string Product,
    string ProductName,
    string Kind,
    string Version,
    string DetectedIn,
    string? Cycle,
    string? CycleLabel,
    DateOnly? Eol,
    DateOnly? SupportEnds,
    DateOnly? ExtendedSupportEnds,
    int? MonthsToEol,
    string Status,
    string? SourceUrl);

public sealed record CodeDependency(
    string Ecosystem,
    string Name,
    string Version,
    string? Latest,
    int? MajorsBehind,
    bool Direct,
    bool Deprecated,
    bool Outdated,
    int Critical,
    int High,
    int Medium,
    int Low);

/// <summary>Las claves de los hallazgos, en el orden en que se explican.</summary>
public static class CodeFactors
{
    public const string EolComponent = "eolComponent";
    public const string ApproachingComponent = "approachingComponent";
    public const string CriticalVulnerability = "criticalVulnerability";
    public const string HighVulnerability = "highVulnerability";
    public const string DeprecatedLibrary = "deprecatedLibrary";
    public const string OutdatedLibrary = "outdatedLibrary";
    public const string StaleRepository = "staleRepository";
}

/// <summary>Cuándo el código y el inventario no coinciden.</summary>
public static class CodeMismatch
{
    /// <summary>CMT la da por vigente, pero su código declara componentes sin soporte.</summary>
    public const string HiddenObsolescence = "hiddenObsolescence";

    /// <summary>CMT la da por legada, pero su código no declara ningún componente vencido.</summary>
    public const string InventoryBehind = "inventoryBehind";
}
