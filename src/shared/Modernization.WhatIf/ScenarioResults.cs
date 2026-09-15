using Modernization.Domain;

namespace Modernization.WhatIf;

/// <summary>
/// Efecto de una palanca aplicada sola sobre la linea base.
/// </summary>
public sealed record LeverEffect
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Action { get; init; }
    public required string MetricKey { get; init; }
    public required string PillarKey { get; init; }
    public required int? Parameter { get; init; }

    /// <summary>Puntos que gana el score si se mueve solo esta palanca.</summary>
    public required decimal Delta { get; init; }

    public required decimal ProjectedScore { get; init; }

    /// <summary>
    /// Cuanto peso entra al calculo que antes no entraba. Una palanca que sube el
    /// denominador no solo mejora el score: mejora cuanta evidencia lo respalda.
    /// </summary>
    public required decimal DenominatorDelta { get; init; }

    /// <summary>La aplicacion ya esta en ese estado. No es una oportunidad.</summary>
    public required bool IsAlreadyAchieved { get; init; }

    /// <summary>
    /// Por que la palanca no se puede mover en esta aplicacion, cuando el motivo
    /// no es que ya este lograda. Null si es accionable.
    /// </summary>
    public Notice? NotActionableReason { get; init; }
}

/// <summary>Catalogo de palancas evaluado sobre una aplicacion concreta.</summary>
public sealed record LeverCatalogResult
{
    public required ScoreBreakdown Baseline { get; init; }

    /// <summary>Ordenadas por ganancia descendente.</summary>
    public required IReadOnlyList<LeverEffect> Levers { get; init; }

    /// <summary>Score maximo con todas las palancas aplicadas.</summary>
    public required ScoreBreakdown Ceiling { get; init; }

    /// <summary>
    /// Palancas que no se pueden mover en esta aplicacion por como llega el dato,
    /// no por estar ya logradas. Explican un techo por debajo de 100.
    /// </summary>
    public IReadOnlyList<LeverEffect> NotActionable =>
        [.. Levers.Where(l => l.NotActionableReason is not null)];

    public decimal Headroom => Math.Round(Ceiling.Score - Baseline.Score, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Palanca efectivamente aplicada en un escenario.</summary>
public sealed record AppliedLever(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter);

/// <summary>Resultado de un escenario compuesto.</summary>
public sealed record ScenarioResult
{
    public required ScoreBreakdown Baseline { get; init; }
    public required ScoreBreakdown Projected { get; init; }
    public required IReadOnlyList<AppliedLever> Applied { get; init; }

    /// <summary>Efecto individual de cada palanca elegida, medido por separado.</summary>
    public required IReadOnlyList<LeverEffect> IndividualEffects { get; init; }

    public decimal Delta => Math.Round(Projected.Score - Baseline.Score, 2, MidpointRounding.AwayFromZero);

    /// <summary>Suma de los efectos individuales.</summary>
    public decimal SumOfIndividualDeltas =>
        Math.Round(IndividualEffects.Sum(e => e.Delta), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Diferencia entre el resultado conjunto y la suma de las partes. Distinta de
    /// cero cuando alguna palanca cambia el denominador: es exactamente el error
    /// que se cometeria sumando deltas en lugar de recalcular.
    /// </summary>
    public decimal InteractionEffect => Math.Round(Delta - SumOfIndividualDeltas, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Resultado de una busqueda por score objetivo.</summary>
public sealed record TargetResult
{
    public required decimal Target { get; init; }
    public required ScoreBreakdown Baseline { get; init; }
    public required ScoreBreakdown Ceiling { get; init; }

    /// <summary>False cuando ni aplicando todas las palancas se alcanza el objetivo.</summary>
    public required bool IsReachable { get; init; }

    /// <summary>Null si el objetivo ya estaba alcanzado o si es inalcanzable.</summary>
    public required ScenarioResult? Result { get; init; }

    public bool IsAlreadyMet => Baseline.Score >= Target;
}
