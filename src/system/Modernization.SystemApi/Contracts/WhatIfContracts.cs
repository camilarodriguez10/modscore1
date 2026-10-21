using Modernization.WhatIf;
using Modernization.Domain;

namespace Modernization.SystemApi.Contracts;

/// <summary>Peticion de escenario compuesto.</summary>
public sealed record ScenarioRequest(IReadOnlyList<LeverRequest> Levers);

/// <summary>Palanca elegida, con su cantidad si la admite.</summary>
public sealed record LeverRequest(string Key, int? Parameter);

/// <summary>Peticion de busqueda por score objetivo.</summary>
public sealed record TargetRequest(decimal TargetScore);

/// <summary>Efecto de una palanca medido por separado sobre la linea base.</summary>
public sealed record LeverEffectResponse(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter,
    decimal Delta,
    decimal ProjectedScore,
    decimal DenominatorDelta,
    bool IsAlreadyAchieved,
    Notice? NotActionableReason)
{
    public static LeverEffectResponse From(LeverEffect e) => new(
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
}

/// <summary>Catalogo de palancas evaluado sobre una aplicacion.</summary>
public sealed record LeverCatalogResponse(
    ScoreResponse Baseline,
    IReadOnlyList<LeverEffectResponse> Levers,
    decimal CeilingScore,
    decimal Headroom,
    decimal? AutomaticTarget = null)
{
    public static LeverCatalogResponse From(LeverCatalogResult r) => new(
        ScoreResponse.From(r.Baseline),
        [.. r.Levers.Select(LeverEffectResponse.From)],
        r.Ceiling.Score,
        r.Headroom);
}

/// <summary>Resultado de un escenario compuesto.</summary>
public sealed record ScenarioResponse(
    ScoreResponse Baseline,
    ScoreResponse Projected,
    decimal Delta,
    decimal SumOfIndividualDeltas,
    decimal InteractionEffect,
    IReadOnlyList<AppliedLeverResponse> Applied,
    IReadOnlyList<LeverEffectResponse> IndividualEffects)
{
    public static ScenarioResponse From(ScenarioResult r) => new(
        ScoreResponse.From(r.Baseline),
        ScoreResponse.From(r.Projected),
        r.Delta,
        r.SumOfIndividualDeltas,
        r.InteractionEffect,
        [.. r.Applied.Select(a => new AppliedLeverResponse(a.Key, a.Name, a.Action, a.MetricKey, a.PillarKey, a.Parameter))],
        [.. r.IndividualEffects.Select(LeverEffectResponse.From)]);
}

public sealed record AppliedLeverResponse(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter);

/// <summary>Resultado de una busqueda por score objetivo.</summary>
public sealed record TargetResponse(
    decimal Target,
    decimal BaselineScore,
    decimal CeilingScore,
    bool IsReachable,
    bool IsAlreadyMet,
    ScenarioResponse? Result)
{
    public static TargetResponse From(TargetResult r) => new(
        r.Target,
        r.Baseline.Score,
        r.Ceiling.Score,
        r.IsReachable,
        r.IsAlreadyMet,
        r.Result is null ? null : ScenarioResponse.From(r.Result));
}
