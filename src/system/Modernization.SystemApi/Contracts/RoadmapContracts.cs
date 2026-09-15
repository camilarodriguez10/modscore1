using Modernization.Roadmap;
using Modernization.Domain;

namespace Modernization.SystemApi.Contracts;

/// <summary>Plan futuro de una aplicacion segun su disposition y su roadmap.</summary>
public sealed record RoadmapPlanResponse(
    string EaiCode,
    string ApplicationName,
    string? Disposition,
    string? DispositionDefinition,
    string? Roadmap,
    string? RoadmapDefinition,
    string? LeverRationale,
    decimal? RoadmapMappedScore,
    bool LeavesPortfolio,
    decimal BaselineScore,
    decimal BaselineWeight,
    decimal? ProjectedScore,
    decimal ProjectedDelta,
    ScenarioResponse? Projection,
    IReadOnlyList<MilestoneResponse> Milestones,
    IReadOnlyList<AlertResponse> Alerts,
    DateOnly StartDate,
    DateOnly? CompletionDate,
    int TotalWeeks)
{
    public static RoadmapPlanResponse From(ApplicationRoadmapPlan p) => new(
        p.EaiCode,
        p.ApplicationName,
        p.Disposition?.ToString(),
        p.DispositionDefinition?.Definition,
        p.RoadmapDefinition?.Name,
        p.RoadmapDefinition?.Definition,
        p.RoadmapDefinition?.LeverRationale,
        p.RoadmapDefinition?.MappedScore,
        p.LeavesPortfolio,
        p.Baseline.EffectiveScore,
        p.Baseline.Denominator,
        p.Projected?.Projected.EffectiveScore,
        p.ProjectedDelta,
        p.Projected is null ? null : ScenarioResponse.From(p.Projected),
        [.. p.Milestones.Select(m => new MilestoneResponse(m.Order, m.Name, m.Owner, m.Weeks, m.Start, m.End))],
        [.. p.Alerts.Select(a => new AlertResponse(
            a.Rule.Key,
            a.Rule.Severity.ToString(),
            a.Rule.Title,
            a.Rule.Detail,
            a.Evidence))],
        p.StartDate,
        p.CompletionDate,
        p.TotalWeeks);
}

public sealed record MilestoneResponse(
    int Order,
    string Name,
    string Owner,
    int Weeks,
    DateOnly Start,
    DateOnly End);

public sealed record AlertResponse(
    string RuleKey,
    string Severity,
    string Title,
    string Detail,
    Notice Evidence);

/// <summary>Vista de portafolio: cuantas aplicaciones hay en cada camino.</summary>
public sealed record PortfolioRoadmapResponse(
    int TotalApplications,
    int WithoutDisposition,
    int LeavingPortfolio,
    IReadOnlyList<DispositionSummaryResponse> Dispositions,
    IReadOnlyList<RoadmapSummaryResponse> Roadmaps,
    IReadOnlyList<AlertSummaryResponse> Alerts,
    string Grouping,
    IReadOnlyList<RoadmapGroupResponse> Groups)
{
    public static PortfolioRoadmapResponse From(PortfolioRoadmapSummary s) => new(
        s.TotalApplications,
        s.WithoutDisposition,
        s.LeavingPortfolio,
        [.. s.Dispositions.Select(d => new DispositionSummaryResponse(
            d.Disposition.ToString(), d.Name, d.Definition, d.Count, d.AverageScore))],
        [.. s.Roadmaps.Select(r => new RoadmapSummaryResponse(
            r.Roadmap.ToString(),
            r.Name,
            r.Disposition.ToString(),
            r.Definition,
            r.Count,
            r.AverageScore,
            r.AverageProjectedScore,
            r.TotalWeeks,
            r.RemovesFromPortfolio))],
        [.. s.Alerts.Select(a => new AlertSummaryResponse(
            a.RuleKey, a.Severity.ToString(), a.Title, a.Count, a.SampleEaiCodes))],
        s.Grouping.ToString(),
        [.. s.Groups.Select(g => new RoadmapGroupResponse(
            g.Key,
            g.Name,
            g.Count,
            g.AverageScore,
            g.AverageProjectedScore,
            g.AverageDelta,
            g.LeavingPortfolio,
            g.WithoutDisposition,
            g.AverageWeeks,
            g.Alerts,
            [.. g.Mix.Select(m => new RoadmapMixResponse(m.Roadmap, m.Name, m.Count))]))]);
}

/// <summary>
/// Un grupo del portafolio con su plan agregado. La clave es la que entienden los
/// filtros, para que hacer clic en un grupo acote el resto de las pantallas.
/// </summary>
public sealed record RoadmapGroupResponse(
    string Key,
    string Name,
    int Count,
    decimal AverageScore,
    decimal? AverageProjectedScore,
    decimal? AverageDelta,
    int LeavingPortfolio,
    int WithoutDisposition,
    int AverageWeeks,
    int Alerts,
    IReadOnlyList<RoadmapMixResponse> Mix);

public sealed record RoadmapMixResponse(string Roadmap, string Name, int Count);

public sealed record DispositionSummaryResponse(
    string Disposition,
    string Name,
    string Definition,
    int Count,
    decimal AverageScore);

public sealed record RoadmapSummaryResponse(
    string Roadmap,
    string Name,
    string Disposition,
    string Definition,
    int Count,
    decimal AverageScore,
    decimal? AverageProjectedScore,
    int TotalWeeks,
    bool RemovesFromPortfolio);

public sealed record AlertSummaryResponse(
    string RuleKey,
    string Severity,
    string Title,
    int Count,
    IReadOnlyList<string> SampleEaiCodes);
