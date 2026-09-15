using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;
using Modernization.Domain;

namespace Modernization.ChannelApi.Composition;

/// <summary>
/// Arma las dos pantallas de plan futuro. Como el resto de la capa Channel, no
/// planifica nada: ordena, agrupa y redacta lo que la capa System ya proyecto.
/// </summary>
public sealed class RoadmapComposer(SystemApiClient system)
{
    public async Task<RoadmapPlanView?> ComposePlanAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
    {
        var applicationTask = system.GetApplicationAsync(eaiCode, cancellationToken);
        var planTask = system.GetRoadmapPlanAsync(eaiCode, cancellationToken);

        await Task.WhenAll(applicationTask, planTask).ConfigureAwait(false);

        var application = await applicationTask.ConfigureAwait(false);
        var plan = await planTask.ConfigureAwait(false);

        if (application is null || plan is null)
        {
            return null;
        }

        return new RoadmapPlanView(
            ScreenComposer.HeaderOf(application),
            plan.Disposition,
            plan.DispositionDefinition,
            plan.Roadmap,
            plan.RoadmapDefinition,
            plan.LeverRationale,
            plan.LeavesPortfolio,
            plan.BaselineScore,
            ScreenComposer.BandOfScore(plan.BaselineScore),
            plan.BaselineWeight,
            plan.ProjectedScore,
            plan.ProjectedScore is { } projected ? ScreenComposer.BandOfScore(projected) : null,
            plan.ProjectedDelta,
            [.. (plan.Projection?.Applied ?? []).Select(a =>
                new AppliedLeverView(a.Key, a.Name, a.Action, a.MetricKey, a.PillarKey, a.Parameter))],
            BuildMilestones(plan),
            [.. plan.Alerts.Select(a => new AlertView(a.RuleKey, a.Severity, a.Title, a.Detail, a.Evidence))],
            plan.StartDate,
            plan.CompletionDate,
            plan.TotalWeeks,
            Outlook(plan));
    }

    /// <summary>
    /// El plan del portafolio filtrado, agrupado por la dimension pedida. El reparto
    /// por disposition sigue viniendo siempre: es el mapa de MAPS y no depende de por
    /// donde se este mirando.
    /// </summary>
    public async Task<PortfolioRoadmapView> ComposePortfolioAsync(
        ApplicationQuery? query = null,
        string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await system.GetPortfolioRoadmapAsync(query, groupBy, cancellationToken).ConfigureAwait(false);

        var groups = summary.Dispositions
            .Select(disposition => new DispositionGroupView(
                disposition.Disposition,
                disposition.Name,
                disposition.Definition,
                disposition.Count,
                Share(disposition.Count, summary.TotalApplications),
                disposition.AverageScore,
                [.. summary.Roadmaps
                    .Where(r => r.Disposition == disposition.Disposition)
                    .Select(r => new RoadmapRowView(
                        r.Roadmap,
                        r.Name,
                        r.Definition,
                        r.Count,
                        r.AverageScore,
                        r.AverageProjectedScore,
                        r.AverageProjectedScore is { } projected
                            ? Math.Round(projected - r.AverageScore, 2, MidpointRounding.AwayFromZero)
                            : null,
                        r.TotalWeeks,
                        r.RemovesFromPortfolio))
                    .OrderByDescending(r => r.Count)]))
            .OrderByDescending(g => g.Count)
            .ToList();

        return new PortfolioRoadmapView(
            summary.TotalApplications,
            summary.WithoutDisposition,
            summary.LeavingPortfolio,
            groups,
            [.. summary.Alerts.Select(a =>
                new AlertSummaryView(a.RuleKey, a.Severity, a.Title, a.Count, a.SampleEaiCodes))],
            summary.Grouping,
            [.. summary.Groups.Select(g => new RoadmapGroupView(
                g.Key,
                g.Name,
                g.Count,
                Share(g.Count, summary.TotalApplications),
                g.AverageScore,
                ScreenComposer.BandOfScore(g.AverageScore),
                g.AverageProjectedScore,
                g.AverageProjectedScore is { } projected ? ScreenComposer.BandOfScore(projected) : null,
                g.AverageDelta,
                g.LeavingPortfolio,
                g.WithoutDisposition,
                g.AverageWeeks,
                g.Alerts,
                [.. g.Mix.Select(m => new RoadmapMixView(m.Roadmap, m.Name, m.Count))]))]);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Los hitos con su peso relativo, para poder dibujar el timeline sin que el
    /// front tenga que dividir duraciones.
    /// </summary>
    private static IReadOnlyList<MilestoneView> BuildMilestones(RoadmapPlanResponse plan)
    {
        if (plan.Milestones.Count == 0)
        {
            return [];
        }

        var total = Math.Max(1, plan.TotalWeeks);

        return [.. plan.Milestones.Select(m => new MilestoneView(
            m.Order,
            m.Name,
            m.Owner,
            m.Weeks,
            m.Start,
            m.End,
            Math.Round((decimal)m.Weeks / total * 100m, 2, MidpointRounding.AwayFromZero)))];
    }

    /// <summary>
    /// Una frase que resume el plan. La lee alguien que abre la pantalla sin
    /// contexto y necesita entender en cinco segundos que le va a pasar a esta
    /// aplicacion.
    /// </summary>
    private static Notice Outlook(RoadmapPlanResponse plan)
    {
        if (plan.Roadmap is null)
        {
            return Notice.Of(NoticeCodes.OutlookNoRoadmap);
        }

        // Retire y Consolidate no proyectan mejora: su score deja de existir.
        if (plan.LeavesPortfolio)
        {
            return Notice.Of(NoticeCodes.OutlookLeavesPortfolio, ("roadmap", plan.Roadmap), ("weeks", plan.TotalWeeks));
        }

        if (plan.ProjectedScore is not { } projected)
        {
            return Notice.Of(NoticeCodes.OutlookNoScoreEffect, ("roadmap", plan.Roadmap));
        }

        return Notice.Of(
            NoticeCodes.OutlookProjected,
            ("roadmap", plan.Roadmap),
            ("baseline", plan.BaselineScore),
            ("projected", projected),
            ("delta", plan.ProjectedDelta),
            ("weeks", plan.TotalWeeks));
    }

    private static decimal Share(int count, int total)
        => total == 0 ? 0m : Math.Round((decimal)count / total * 100m, 1, MidpointRounding.AwayFromZero);
}
