using Modernization.Insights;

namespace Modernization.SystemApi.Contracts;

/// <summary>
/// El dashboard ejecutivo del portafolio filtrado, con lo que hace falta para leerlo:
/// las reglas del score, la version de las definiciones del dashboard y si todavia
/// son provisionales, la carga, la tendencia por carga y que fuentes estan conectadas.
/// </summary>
public sealed record DashboardResponse(
    string RulesetVersion,
    string DefinitionsVersion,
    string DefinitionsStatus,
    DateTimeOffset CapturedAt,
    IReadOnlyList<DashboardTrendPoint> Trend,
    IReadOnlyList<DashboardSource> Sources,
    PortfolioDashboard Dashboard)
{
    /// <summary>
    /// Las fuentes que cita la propuesta del dashboard. Las conectadas son las que la
    /// plataforma ya lee; las otras son las que harian falta para responder sin
    /// aproximaciones (DQ-04, DQ-05, DQ-06). Se dice con esas palabras, no se oculta.
    /// </summary>
    public static IReadOnlyList<DashboardSource> KnownSources { get; } =
    [
        new("eai", true),
        new("maps", true),
        new("cmt", true),
        new("mts", true),
        new("sca", true),
        new("rpt", true),
        new("monitoring", true),
        new("tlm", false),
        new("architecturalAssessments", false),
        new("arb", false),
        new("resiliency", false),
    ];
}

/// <summary>
/// La salud del portafolio filtrado en una carga. <c>HealthScore</c> y
/// <c>AtRiskShare</c> son null cuando ninguna aplicacion de esa carga pasa el filtro.
/// </summary>
public sealed record DashboardTrendPoint(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    int Applications,
    decimal? HealthScore,
    decimal? AtRiskShare);

/// <summary>Una fuente que usa el dashboard, conectada o no.</summary>
public sealed record DashboardSource(string Key, bool Connected);
