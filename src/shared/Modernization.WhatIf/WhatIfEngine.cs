using Modernization.Domain;
using Modernization.Scoring.Engine;

namespace Modernization.WhatIf;

/// <summary>
/// Motor de escenarios. Reutiliza <see cref="ScoringEngine"/> como libreria en el
/// mismo proceso, no por red: el score base y el simulado se calculan con el
/// mismo codigo, bit a bit. Si el what-if invocara al scoring por HTTP podrian
/// divergir; asi es imposible (ADR 0001).
/// </summary>
/// <remarks>
/// Cada escenario recalcula desde cero en lugar de sumar deltas. La diferencia
/// importa: una palanca que aporta dato a una metrica que no lo tenia sube tambien
/// el denominador, y sumar deltas individuales daria un numero equivocado.
/// </remarks>
public sealed class WhatIfEngine
{
    /// <summary>Tope de palancas por escenario. Evita peticiones abusivas.</summary>
    public const int MaxLeversPerScenario = 20;

    /// <summary>
    /// Tope de productos que un plan mueve con una palanca de cantidad. Los conteos reales
    /// pasan de cien (el caso del reporte tiene 520 por modernizar); esto solo acota la búsqueda.
    /// </summary>
    public const int MaxPlannedProducts = 100_000;

    private readonly ScoringEngine _engine;
    private readonly LeverCatalog _levers;

    /// <summary>Con las palancas de fabrica, construidas sobre las reglas del motor.</summary>
    public WhatIfEngine(ScoringEngine engine)
        : this(engine, new LeverCatalog((engine ?? throw new ArgumentNullException(nameof(engine))).Ruleset))
    {
    }

    /// <summary>
    /// Con un catalogo concreto. Motor y catalogo tienen que salir de las mismas
    /// reglas: los objetivos de las palancas se leen de las bandas del ruleset.
    /// </summary>
    public WhatIfEngine(ScoringEngine engine, LeverCatalog levers)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(levers);

        _engine = engine;
        _levers = levers;
    }

    /// <summary>Las palancas con que simula este motor.</summary>
    public LeverCatalog Levers => _levers;

    /// <summary>
    /// Evalua cada palanca por separado sobre la linea base. Es el catalogo de
    /// oportunidades ordenado por retorno.
    /// </summary>
    public LeverCatalogResult EvaluateAllLevers(ApplicationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var baseline = _engine.Calculate(snapshot);
        var effects = new List<LeverEffect>(_levers.Levers.Count);

        foreach (var lever in _levers.Levers)
        {
            effects.Add(EvaluateSingle(snapshot, baseline, lever, lever.DefaultParameter));
        }

        return new LeverCatalogResult
        {
            Baseline = baseline,
            // Mayor ganancia primero; a igual ganancia, orden estable por clave.
            Levers = [.. effects
                .OrderByDescending(e => e.Delta)
                .ThenBy(e => e.Key, StringComparer.Ordinal)],
            Ceiling = ApplyAll(snapshot, baseline),
        };
    }

    /// <summary>
    /// Aplica un conjunto de palancas a la vez y recalcula. El delta resultante no
    /// tiene por que ser la suma de los individuales.
    /// </summary>
    public ScenarioResult Evaluate(ApplicationSnapshot snapshot, IReadOnlyList<LeverSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selections);

        if (selections.Count > MaxLeversPerScenario)
        {
            throw new WhatIfException(
                "too-many-levers",
                $"A scenario accepts up to {MaxLeversPerScenario} levers; {selections.Count} were received.");
        }

        var baseline = _engine.Calculate(snapshot);
        var projectedSnapshot = snapshot;
        var applied = new List<AppliedLever>(selections.Count);

        foreach (var selection in selections)
        {
            var lever = _levers.Require(selection.Key);
            var parameter = Validate(lever, selection.Parameter);

            projectedSnapshot = lever.Apply(projectedSnapshot, parameter);
            applied.Add(new AppliedLever(lever.Key, lever.Name, lever.Action, lever.MetricKey, lever.PillarKey, parameter));
        }

        var projected = _engine.Calculate(projectedSnapshot);

        return new ScenarioResult
        {
            Baseline = baseline,
            Projected = projected,
            Applied = applied,
            // El efecto individual de cada palanca elegida, para explicar la
            // diferencia entre la suma de las partes y el resultado conjunto.
            IndividualEffects = [.. selections.Select(selection =>
            {
                var lever = _levers.Require(selection.Key);
                return EvaluateSingle(snapshot, baseline, lever, Validate(lever, selection.Parameter));
            })],
        };
    }

    /// <summary>
    /// El plan hacia un score objetivo. Es el mismo para el laboratorio y para el plan
    /// automático de cada aplicación (2026-10-21): pedir 90 en el what-if da exactamente el
    /// <i>future plan</i>.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Cada palanca que suma, sola y a su máximo —las de cantidad, con todos los
    /// productos que pueden mover—, de mayor a menor ganancia.</item>
    /// <item>Greedy verificado con el motor: se agregan en ese orden hasta alcanzar el
    /// objetivo. El greedy elige el orden; el motor decide si basta.</item>
    /// <item>Poda: al recalcular en conjunto una palanca puede dejar de hacer falta, y se
    /// quita.</item>
    /// <item>Las de cantidad bajan a la mínima que sostiene el objetivo.</item>
    /// </list>
    /// Si ni con todas se alcanza, el plan son todas las que suman, a su máximo: el mejor
    /// resultado posible, y el mismo en las dos pantallas.
    /// </remarks>
    public TargetResult ReachTarget(ApplicationSnapshot snapshot, decimal targetScore)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // El modelo normaliza a 20-100. Un objetivo fuera de esa escala no es una
        // busqueda dificil: es una entrada invalida.
        if (targetScore is < 20m or > 100m)
        {
            throw new WhatIfException(
                "invalid-target",
                "The target score must be between 20 and 100.");
        }

        var baseline = _engine.Calculate(snapshot);
        var ceiling = ApplyAll(snapshot, baseline);

        if (baseline.Score >= targetScore)
        {
            return new TargetResult
            {
                Target = targetScore,
                Baseline = baseline,
                Ceiling = ceiling,
                IsReachable = true,
                Result = null,
            };
        }

        var candidates = Candidates(snapshot, baseline);
        var chosen = new List<LeverSelection>();
        var reached = false;

        foreach (var candidate in candidates)
        {
            chosen.Add(candidate);

            if (Reaches(snapshot, chosen, targetScore))
            {
                reached = true;
                break;
            }
        }

        if (!reached)
        {
            // Ni con todas: el mejor plan posible son todas las que suman, a su máximo.
            return new TargetResult
            {
                Target = targetScore,
                Baseline = baseline,
                Ceiling = ceiling,
                IsReachable = false,
                Result = Evaluate(snapshot, candidates),
            };
        }

        // Poda: quitar cada palanca y quedarse sin ella si el objetivo se mantiene.
        for (var i = chosen.Count - 1; i >= 0; i--)
        {
            var without = chosen.Where((_, index) => index != i).ToList();

            if (without.Count > 0 && Reaches(snapshot, without, targetScore))
            {
                chosen = without;
            }
        }

        // Las de cantidad, a la mínima que sostiene el objetivo: «modernizar 2 productos» es
        // una acción; «todos los que se pueda» no siempre hace falta.
        for (var i = 0; i < chosen.Count; i++)
        {
            if (chosen[i].Parameter is not { } most || most <= 1)
            {
                continue;
            }

            var (low, high) = (1, most);

            while (low < high)
            {
                var middle = (low + high) / 2;
                var trial = chosen.ToList();
                trial[i] = chosen[i] with { Parameter = middle };

                if (Reaches(snapshot, trial, targetScore))
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            chosen[i] = chosen[i] with { Parameter = low };
        }

        return new TargetResult
        {
            Target = targetScore,
            Baseline = baseline,
            Ceiling = ceiling,
            IsReachable = true,
            Result = Evaluate(snapshot, chosen),
        };
    }

    /// <summary>
    /// Las palancas que suman solas, a su máximo, de mayor a menor ganancia; a igual
    /// ganancia, por clave, para que el plan sea siempre el mismo.
    /// </summary>
    private List<LeverSelection> Candidates(ApplicationSnapshot snapshot, ScoreBreakdown baseline)
    {
        var scored = new List<(LeverSelection Selection, decimal Delta)>();

        foreach (var lever in _levers.Levers)
        {
            if (lever.IsAlreadyAchieved(snapshot))
            {
                continue;
            }

            int? parameter = lever.IsParameterized
                ? Math.Clamp(lever.Capacity?.Invoke(snapshot) ?? lever.DefaultParameter ?? 1, 1, MaxPlannedProducts)
                : null;

            var delta = _engine.Calculate(lever.Apply(snapshot, parameter)).Score - baseline.Score;

            if (delta > 0m)
            {
                scored.Add((new LeverSelection(lever.Key, parameter), delta));
            }
        }

        return [.. scored
            .OrderByDescending(c => c.Delta)
            .ThenBy(c => c.Selection.Key, StringComparer.Ordinal)
            .Select(c => c.Selection)];
    }

    private bool Reaches(ApplicationSnapshot snapshot, IReadOnlyList<LeverSelection> selections, decimal targetScore)
    {
        var projected = snapshot;

        foreach (var selection in selections)
        {
            projected = _levers.Require(selection.Key).Apply(projected, selection.Parameter);
        }

        return _engine.Calculate(projected).Score >= targetScore;
    }

    // -----------------------------------------------------------------------

    private LeverEffect EvaluateSingle(
        ApplicationSnapshot snapshot,
        ScoreBreakdown baseline,
        ScenarioLever lever,
        int? parameter)
    {
        if (lever.IsAlreadyAchieved(snapshot))
        {
            return new LeverEffect
            {
                Key = lever.Key,
                Name = lever.Name,
                Action = lever.Action,
                MetricKey = lever.MetricKey,
                PillarKey = lever.PillarKey,
                Parameter = parameter,
                Delta = 0m,
                ProjectedScore = baseline.Score,
                DenominatorDelta = 0m,
                IsAlreadyAchieved = true,
                NotActionableReason = lever.NotActionableReason?.Invoke(snapshot),
            };
        }

        var projected = _engine.Calculate(lever.Apply(snapshot, parameter));

        return new LeverEffect
        {
            Key = lever.Key,
            Name = lever.Name,
            Action = lever.Action,
            MetricKey = lever.MetricKey,
            PillarKey = lever.PillarKey,
            Parameter = parameter,
            Delta = Round(projected.Score - baseline.Score),
            ProjectedScore = projected.Score,
            // Si sube, la palanca aporto dato donde no lo habia: no solo mejora
            // el score, tambien mejora cuanta evidencia lo respalda.
            DenominatorDelta = projected.Denominator - baseline.Denominator,
            IsAlreadyAchieved = false,
        };
    }

    /// <summary>Techo alcanzable: todas las palancas encendidas aplicadas a la vez.</summary>
    private ScoreBreakdown ApplyAll(ApplicationSnapshot snapshot, ScoreBreakdown baseline)
    {
        var projected = snapshot;

        foreach (var lever in _levers.Levers)
        {
            // Las parametrizadas se aplican al maximo posible para el techo.
            var parameter = lever.IsParameterized ? int.MaxValue : (int?)null;
            projected = lever.Apply(projected, parameter);
        }

        return ReferenceEquals(projected, snapshot) ? baseline : _engine.Calculate(projected);
    }

    private static int? Validate(ScenarioLever lever, int? parameter)
    {
        if (!lever.IsParameterized)
        {
            return null;
        }

        var value = parameter ?? lever.DefaultParameter;

        if (value is null or < 1)
        {
            throw new WhatIfException(
                "invalid-parameter",
                $"The lever '{lever.Key}' needs a quantity of at least 1.");
        }

        return value;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
