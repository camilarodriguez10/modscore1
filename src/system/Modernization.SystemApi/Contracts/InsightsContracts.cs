using Modernization.Domain;
using Modernization.Insights;

namespace Modernization.SystemApi.Contracts;

/// <summary>
/// El análisis del portafolio, con la versión de reglas y la carga con que se calculó.
/// </summary>
/// <remarks>
/// Las dos referencias importan: el mismo portafolio con otras reglas responde distinto,
/// y quien lee una respuesta tiene que saber de qué corte de datos habla. Los enums
/// viajan como texto, igual que en el resto del API.
/// </remarks>
public sealed record InsightsResponse(
    string RulesetVersion,
    DateTimeOffset CapturedAt,
    PortfolioHealthResponse Health,
    IReadOnlyList<ModernizationCandidateResponse> Priorities,
    IReadOnlyList<ComplexityClusterResponse> Complexity,
    IReadOnlyList<RiskConcentrationResponse> Risk,
    IReadOnlyList<ObsolescenceDriverResponse> Obsolescence,
    ResilienceGapsResponse Resilience,
    InsightsOptionsResponse Options,
    AnalyticsDocument Definitions)
{
    public static InsightsResponse From(
        string rulesetVersion,
        DateTimeOffset capturedAt,
        PortfolioInsights insights,
        AnalyticsDocument definitions)
    {
        ArgumentNullException.ThrowIfNull(insights);
        ArgumentNullException.ThrowIfNull(definitions);

        return new InsightsResponse(
            rulesetVersion,
            capturedAt,
            PortfolioHealthResponse.From(insights.Health),
            [.. insights.Priorities.Select(ModernizationCandidateResponse.From)],
            [.. insights.Complexity.Select(ComplexityClusterResponse.From)],
            [.. insights.Risk.Select(RiskConcentrationResponse.From)],
            [.. insights.Obsolescence.Select(ObsolescenceDriverResponse.From)],
            ResilienceGapsResponse.From(insights.Resilience),
            InsightsOptionsResponse.From(insights.Options),
            definitions);
    }
}

/// <summary>¿Qué tan sano está el portafolio?</summary>
public sealed record PortfolioHealthResponse(
    int Applications,
    decimal AverageScore,
    decimal MedianScore,
    decimal DataCoverage,
    int CriticalBelowHealthy,
    IReadOnlyList<BandShareResponse> Bands)
{
    public static PortfolioHealthResponse From(PortfolioHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);

        return new PortfolioHealthResponse(
            health.Applications,
            health.AverageScore,
            health.MedianScore,
            health.DataCoverage,
            health.CriticalBelowHealthy,
            [.. health.Bands.Select(b => new BandShareResponse(b.Code, b.Name, b.Applications, b.Share))]);
    }
}

public sealed record BandShareResponse(string Code, string Name, int Applications, decimal Share);

/// <summary>¿Qué modernizar primero? Impacto contra esfuerzo, con su evidencia.</summary>
public sealed record ModernizationCandidateResponse(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    decimal CurrentScore,
    decimal? ProjectedScore,
    decimal Uplift,
    int Weeks,
    decimal BusinessWeight,
    decimal Impact,
    decimal ValuePerWeek,
    string? Roadmap,
    string? RoadmapName,
    IReadOnlyList<Notice> Evidence)
{
    public static ModernizationCandidateResponse From(ModernizationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ModernizationCandidateResponse(
            candidate.EaiCode,
            candidate.ApplicationName,
            candidate.Lob,
            candidate.Country,
            candidate.CurrentScore,
            candidate.ProjectedScore,
            candidate.Uplift,
            candidate.Weeks,
            candidate.BusinessWeight,
            candidate.Impact,
            candidate.ValuePerWeek,
            candidate.Roadmap?.ToString(),
            candidate.Roadmap is { } roadmap ? MapsNames.Display(roadmap) : null,
            candidate.Evidence);
    }
}

/// <summary>¿Dónde reducir complejidad? Una concentración, con las aplicaciones que la forman.</summary>
public sealed record ComplexityClusterResponse(
    string Kind,
    string Dimension,
    int Applications,
    int Users,
    decimal AverageScore,
    IReadOnlyList<string> EaiCodes,
    Notice Evidence)
{
    public static ComplexityClusterResponse From(ComplexityCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        return new ComplexityClusterResponse(
            cluster.Kind.ToString(),
            cluster.Dimension,
            cluster.Applications,
            cluster.Users,
            cluster.AverageScore,
            cluster.EaiCodes,
            cluster.Evidence);
    }
}

/// <summary>¿Dónde se concentra el riesgo tecnológico?</summary>
public sealed record RiskConcentrationResponse(
    string Grouping,
    string Dimension,
    int Applications,
    int CriticalFlaws,
    int HighFlaws,
    int WeakVulnerabilityManagement,
    int ObsoleteComponents,
    int InternetFacingCritical,
    decimal AverageScore,
    decimal RiskIndex)
{
    public static RiskConcentrationResponse From(RiskConcentration risk)
    {
        ArgumentNullException.ThrowIfNull(risk);

        return new RiskConcentrationResponse(
            risk.Grouping,
            risk.Dimension,
            risk.Applications,
            risk.CriticalFlaws,
            risk.HighFlaws,
            risk.WeakVulnerabilityManagement,
            risk.ObsoleteComponents,
            risk.InternetFacingCritical,
            risk.AverageScore,
            risk.RiskIndex);
    }
}

/// <summary>¿Qué causa la obsolescencia? Puntos perdidos por métrica.</summary>
public sealed record ObsolescenceDriverResponse(
    string MetricKey,
    string Name,
    string PillarKey,
    decimal LostPoints,
    decimal WeightWithoutData,
    int ApplicationsAffected,
    decimal Share)
{
    public static ObsolescenceDriverResponse From(ObsolescenceDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return new ObsolescenceDriverResponse(
            driver.MetricKey,
            driver.Name,
            driver.PillarKey,
            driver.LostPoints,
            driver.WeightWithoutData,
            driver.ApplicationsAffected,
            driver.Share);
    }
}

/// <summary>¿Dónde están los huecos de resiliencia?</summary>
public sealed record ResilienceGapsResponse(
    int CriticalWithoutDisasterRecovery,
    int CriticalWithoutMonitoring,
    int WithLowOperationalScore,
    int WithoutRecoveryObjectives,
    IReadOnlyList<ResilienceGapApplicationResponse> Applications)
{
    public static ResilienceGapsResponse From(ResilienceGaps gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);

        return new ResilienceGapsResponse(
            gaps.CriticalWithoutDisasterRecovery,
            gaps.CriticalWithoutMonitoring,
            gaps.WithLowOperationalScore,
            gaps.WithoutRecoveryObjectives,
            [.. gaps.Applications.Select(a => new ResilienceGapApplicationResponse(
                a.EaiCode,
                a.ApplicationName,
                a.Lob,
                a.Country,
                a.Critical,
                a.Score,
                a.RecoveryTimeObjective,
                a.RecoveryPointObjective,
                a.Gaps))]);
    }
}

public sealed record ResilienceGapApplicationResponse(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    bool Critical,
    decimal Score,
    string? RecoveryTimeObjective,
    string? RecoveryPointObjective,
    IReadOnlyList<Notice> Gaps);

/// <summary>Los umbrales con que se calculó, para que la pantalla los muestre.</summary>
public sealed record InsightsOptionsResponse(
    decimal HealthyScore,
    int SignificantUserBase,
    int DuplicateThreshold,
    int TopCount)
{
    public static InsightsOptionsResponse From(InsightsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new InsightsOptionsResponse(
            options.HealthyScore,
            options.SignificantUserBase,
            options.DuplicateThreshold,
            options.TopCount);
    }
}
