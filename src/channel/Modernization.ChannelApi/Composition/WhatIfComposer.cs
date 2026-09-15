using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;
using Modernization.Domain;

namespace Modernization.ChannelApi.Composition;

/// <summary>
/// Arma la pantalla del What-If Lab. Como el resto de la capa Channel, no calcula
/// ningun score: ordena, agrupa y etiqueta lo que la capa System ya simulo.
/// </summary>
public sealed class WhatIfComposer(SystemApiClient system)
{
    /// <summary>Cuantas palancas se destacan como quick wins.</summary>
    private const int QuickWinCount = 3;

    public async Task<WhatIfLabView?> ComposeLabAsync(string eaiCode, CancellationToken cancellationToken = default)
    {
        var applicationTask = system.GetApplicationAsync(eaiCode, cancellationToken);
        var catalogTask = system.GetLeversAsync(eaiCode, cancellationToken);

        await Task.WhenAll(applicationTask, catalogTask).ConfigureAwait(false);

        var application = await applicationTask.ConfigureAwait(false);
        var catalog = await catalogTask.ConfigureAwait(false);

        if (application is null || catalog is null)
        {
            return null;
        }

        var actionable = catalog.Levers
            .Where(l => !l.IsAlreadyAchieved && l.Delta > 0)
            .ToList();

        return new WhatIfLabView(
            ScreenComposer.HeaderOf(application),
            catalog.Baseline.EffectiveScore,
            catalog.Baseline.Denominator,
            catalog.CeilingScore,
            catalog.Headroom,
            [.. actionable.Take(QuickWinCount).Select(ToLever)],
            [.. actionable.Select(ToLever)],
            [.. catalog.Levers.Where(l => l.IsAlreadyAchieved && l.NotActionableReason is null).Select(ToLever)],
            [.. catalog.Levers.Where(l => l.NotActionableReason is not null).Select(ToLever)]);
    }

    public async Task<ScenarioView?> ComposeScenarioAsync(
        string eaiCode,
        IReadOnlyList<LeverRequest> levers,
        CancellationToken cancellationToken = default)
    {
        var result = await system.PostScenarioAsync(eaiCode, levers, cancellationToken).ConfigureAwait(false);

        return result is null ? null : ToScenario(result);
    }

    public async Task<TargetView?> ComposeTargetAsync(
        string eaiCode,
        decimal targetScore,
        CancellationToken cancellationToken = default)
    {
        var result = await system.PostTargetAsync(eaiCode, targetScore, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return null;
        }

        return new TargetView(
            result.Target,
            result.BaselineScore,
            result.CeilingScore,
            result.IsReachable,
            result.IsAlreadyMet,
            result.Result is null ? null : ToScenario(result.Result),
            Explain(result));
    }

    // -----------------------------------------------------------------------

    private static ScenarioView ToScenario(ScenarioResponse r) => new(
        r.Baseline.EffectiveScore,
        r.Projected.EffectiveScore,
        r.Delta,
        r.Baseline.Denominator,
        r.Projected.Denominator,
        r.SumOfIndividualDeltas,
        r.InteractionEffect,
        [.. r.Applied.Select(a => new AppliedLeverView(a.Key, a.Name, a.Action, a.MetricKey, a.PillarKey, a.Parameter))],
        [.. r.IndividualEffects.Select(ToLever)],
        InteractionNote(r));

    private static LeverView ToLever(LeverEffectResponse e) => new(
        e.Key,
        e.Name,
        e.Action,
        e.MetricKey,
        e.PillarKey,
        e.Parameter,
        e.Delta,
        e.ProjectedScore,
        e.DenominatorDelta,
        e.IsAlreadyAchieved,
        e.NotActionableReason);

    /// <summary>
    /// Cuando el resultado conjunto no coincide con la suma de las partes, el
    /// usuario merece saber por que en vez de sospechar del calculo.
    /// </summary>
    private static Notice? InteractionNote(ScenarioResponse r)
    {
        if (r.InteractionEffect == 0m)
        {
            return null;
        }

        var added = r.Projected.Denominator - r.Baseline.Denominator;

        return added > 0
            ? Notice.Of(
                NoticeCodes.ScenarioWeightAdded,
                ("from", r.Baseline.Denominator),
                ("to", r.Projected.Denominator),
                ("sum", r.SumOfIndividualDeltas),
                ("delta", r.Delta))
            : Notice.Of(NoticeCodes.ScenarioInteraction, ("effect", r.InteractionEffect));
    }

    private static Notice Explain(TargetResponse r)
    {
        if (r.IsAlreadyMet)
        {
            return Notice.Of(NoticeCodes.TargetAlreadyMet, ("score", r.BaselineScore));
        }

        if (!r.IsReachable)
        {
            return Notice.Of(NoticeCodes.TargetUnreachable, ("target", r.Target), ("ceiling", r.CeilingScore));
        }

        return Notice.Of(
            NoticeCodes.TargetReachable,
            ("count", r.Result?.Applied.Count ?? 0),
            ("baseline", r.BaselineScore),
            ("projected", r.Result?.Projected.EffectiveScore));
    }
}
