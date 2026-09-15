using Modernization.Domain;
using Modernization.WhatIf;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Criterio de aceptacion de la Fase 5. La regla general, cuando la metrica ya
/// tiene dato, es <c>Δ = 0,8 × peso × (valor′ − valor) ÷ denominador</c>; con un
/// denominador de 80 la constante se reduce a <c>peso × (valor′ − valor) ÷ 100</c>.
/// Cada numero de esta suite se puede verificar a mano con esa formula.
/// </summary>
public sealed class WhatIfTests
{
    private readonly WhatIfEngine _whatIf = new(new ScoringEngine(RulesetCatalog.Default));
    private readonly ScoringEngine _engine = new(RulesetCatalog.Default);

    /// <summary>
    /// EAI 10969 con MFA en "No (with users)". Todo lo demas identico al
    /// dashboard, asi que el denominador sigue siendo 80 y la palanca de MFA
    /// vale exactamente 7 × (100 − 20) ÷ 100 = 5,60 puntos.
    /// </summary>
    private static ApplicationSnapshot WithoutMfa()
        => GoldenFixtures.Eai10969() with { UtilizesMfa = false };

    [Fact]
    public void EnablingMfa_AddsExactlyFivePointSixty()
    {
        var snapshot = WithoutMfa();

        // Linea base: 59,609 − 7,00 + 1,40 = 54,009 sobre 80 => 74,01
        var baseline = _engine.Calculate(snapshot);
        Assert.Equal(54.009m, baseline.Numerator);
        Assert.Equal(80m, baseline.Denominator);
        Assert.Equal(74.01m, baseline.Score);

        var scenario = _whatIf.Evaluate(snapshot, [new LeverSelection("mfa.enable")]);

        Assert.Equal(59.609m, scenario.Projected.Numerator);
        Assert.Equal(79.61m, scenario.Projected.Score);
        Assert.Equal(5.60m, scenario.Delta);
    }

    /// <summary>
    /// Cada palanca vale <c>peso × 80 ÷ 100</c> cuando lleva la metrica de 20 a 100
    /// con el denominador en 80. Son los numeros publicados en el plan.
    /// </summary>
    [Theory]
    [InlineData("sca.remediateHigh", 8, 6.40)]
    [InlineData("kenna.reduce", 8, 6.40)]
    [InlineData("mfa.enable", 7, 5.60)]
    [InlineData("dr.include", 7, 5.60)]
    [InlineData("oss.improve", 10, 8.00)]
    public void LeverGainMatchesItsWeight(string leverKey, int weight, double expectedDelta)
    {
        // Se parte de una aplicacion con todas esas metricas en su peor valor.
        var snapshot = GoldenFixtures.Eai10969() with
        {
            UtilizesMfa = false,
            DisasterRecoveryInScope = false,
            KennaScore = 900,
            OpenSourceScore = 30m,
            ScaFlaws = new ScaFlawCounts { Critical = null, High = 5, Medium = 1, Low = 1 },
        };

        var effect = _whatIf.EvaluateAllLevers(snapshot).Levers.Single(l => l.Key == leverKey);

        Assert.Equal((decimal)expectedDelta, effect.Delta);
        Assert.Equal(0m, effect.DenominatorDelta);

        // La formula del plan, verificada aparte del motor.
        Assert.Equal((decimal)expectedDelta, Math.Round(0.8m * weight * 80m / 80m, 2));
    }

    /// <summary>
    /// Una palanca ya lograda no es una oportunidad: aparece con delta cero y
    /// marcada, en lugar de ensuciar el ranking de quick wins.
    /// </summary>
    [Fact]
    public void AlreadyAchievedLeversReportZeroGain()
    {
        var effect = _whatIf.EvaluateAllLevers(GoldenFixtures.Eai10969()).Levers
            .Single(l => l.Key == "mfa.enable");

        Assert.True(effect.IsAlreadyAchieved);
        Assert.Equal(0m, effect.Delta);
    }

    /// <summary>
    /// El catalogo viene ordenado por retorno: los quick wins primero.
    /// </summary>
    [Fact]
    public void CatalogIsOrderedByGain()
    {
        var levers = _whatIf.EvaluateAllLevers(WithoutMfa()).Levers;

        Assert.Equal(LeverCatalog.Default.Levers.Count, levers.Count);
        Assert.Equal(levers.Select(l => l.Delta).OrderByDescending(d => d), levers.Select(l => l.Delta));
    }

    // ---------------------------------------------------- escenarios compuestos

    /// <summary>
    /// Cuando ninguna palanca aporta dato nuevo, el conjunto si equivale a la suma
    /// de las partes. Es el caso facil, y sirve de control del caso dificil.
    /// </summary>
    [Fact]
    public void CompositeScenarioWithoutNewData_EqualsSumOfParts()
    {
        var snapshot = GoldenFixtures.Eai10969() with
        {
            UtilizesMfa = false,
            DisasterRecoveryInScope = false,
        };

        var scenario = _whatIf.Evaluate(
            snapshot,
            [new LeverSelection("mfa.enable"), new LeverSelection("dr.include")]);

        Assert.Equal(11.20m, scenario.Delta);
        Assert.Equal(11.20m, scenario.SumOfIndividualDeltas);
        Assert.Equal(0m, scenario.InteractionEffect);
    }

    /// <summary>
    /// El caso dificil, y la razon por la que el motor recalcula desde cero: una
    /// palanca que aporta dato donde no lo habia sube el denominador, y entonces
    /// el resultado conjunto deja de ser la suma de los individuales.
    /// </summary>
    [Fact]
    public void CompositeScenarioAddingNewData_DiffersFromSumOfParts()
    {
        // Sin dato de SCA Critical (peso 10) ni de Tech Debt (peso 10):
        // el denominador arranca en 80 y ambas palancas lo suben.
        var snapshot = GoldenFixtures.Eai10969() with { UtilizesMfa = false };

        var scenario = _whatIf.Evaluate(
            snapshot,
            [
                new LeverSelection("mfa.enable"),
                new LeverSelection("sca.remediateCritical"),
                new LeverSelection("techDebt.clear"),
            ]);

        // El denominador pasa de 80 a 100: dos metricas mas entran al calculo.
        Assert.Equal(80m, scenario.Baseline.Denominator);
        Assert.Equal(100m, scenario.Projected.Denominator);

        Assert.NotEqual(scenario.SumOfIndividualDeltas, scenario.Delta);
        Assert.NotEqual(0m, scenario.InteractionEffect);
    }

    [Fact]
    public void RemediatingMissingScaAlsoRaisesTheDenominator()
    {
        var effect = _whatIf.EvaluateAllLevers(GoldenFixtures.Eai10969()).Levers
            .Single(l => l.Key == "sca.remediateCritical");

        // La metrica pasa de "sin dato" a "None": el peso 10 entra al calculo.
        Assert.Equal(10m, effect.DenominatorDelta);
        Assert.True(effect.Delta > 0);
    }

    // -------------------------------------------------------- objetivo inverso

    [Fact]
    public void TargetSearchFindsASetThatReachesTheGoal()
    {
        var snapshot = WithoutMfa();

        var result = _whatIf.ReachTarget(snapshot, 90m);

        Assert.True(result.IsReachable);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.Projected.Score >= 90m);
        Assert.NotEmpty(result.Result.Applied);
    }

    /// <summary>
    /// El conjunto devuelto es minimo: quitar cualquier palanca deja el objetivo
    /// sin alcanzar. Sin la poda, el greedy devolveria palancas de sobra.
    /// </summary>
    [Fact]
    public void TargetSearchReturnsAMinimalSet()
    {
        var snapshot = WithoutMfa();
        var result = _whatIf.ReachTarget(snapshot, 90m);

        Assert.NotNull(result.Result);
        var chosen = result.Result.Applied.Select(a => new LeverSelection(a.Key, a.Parameter)).ToList();

        for (var i = 0; i < chosen.Count; i++)
        {
            var without = chosen.Where((_, index) => index != i).ToList();
            var reduced = _whatIf.Evaluate(snapshot, without);

            Assert.True(
                reduced.Projected.Score < 90m,
                $"Quitar '{chosen[i].Key}' sigue alcanzando el objetivo: el conjunto no era minimo.");
        }
    }

    [Fact]
    public void TargetAlreadyMetNeedsNoLevers()
    {
        var result = _whatIf.ReachTarget(GoldenFixtures.Eai10969(), 70m);

        Assert.True(result.IsAlreadyMet);
        Assert.Null(result.Result);
    }

    /// <summary>
    /// El catalogo cubre las trece metricas puntuadas, asi que una aplicacion cuyo
    /// dato llega con el detalle por producto puede llegar a 100: lo que le falta
    /// a su score es trabajo, no imposibilidad.
    /// </summary>
    [Fact]
    public void ApplicationWithProductDetailCanReachOneHundred()
    {
        var result = _whatIf.ReachTarget(GoldenFixtures.Eai14381(), 100m);

        Assert.True(result.IsReachable);
        Assert.Equal(100m, result.Ceiling.Score);
    }

    /// <summary>
    /// Cuando la fuente entrega el compuesto ya calculado en lugar de los conteos,
    /// ninguna palanca puede moverlo: no hay productos que reclasificar. El techo
    /// queda por debajo de 100 y el motivo se reporta, en vez de dejar al usuario
    /// preguntandose por que la palanca no hace nada.
    /// </summary>
    [Fact]
    public void ProvidedCompositeCannotBeSimulated()
    {
        // EAI 10969 trae Architectural Value y Product Strategy ya compuestos.
        var catalog = _whatIf.EvaluateAllLevers(GoldenFixtures.Eai10969());

        var blocked = catalog.NotActionable.Select(l => l.Key).ToList();
        Assert.Contains("archValue.modernize", blocked);
        Assert.Contains("productStrategy.invest", blocked);
        Assert.All(catalog.NotActionable, l => Assert.NotNull(l.NotActionableReason));

        // 100 − 10 − 10 + 7,572 + 8,837 = 96,409 sobre 100 => 97,13
        Assert.Equal(97.13m, catalog.Ceiling.Score);

        var result = _whatIf.ReachTarget(GoldenFixtures.Eai10969(), 100m);
        Assert.False(result.IsReachable);
        Assert.Null(result.Result);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(101)]
    public void TargetOutsideTheModelScaleIsRejected(int target)
    {
        var ex = Assert.Throws<WhatIfException>(
            () => _whatIf.ReachTarget(GoldenFixtures.Eai10969(), target));

        Assert.Contains("20", ex.Message, StringComparison.Ordinal);
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- techo alcanzable

    [Fact]
    public void CeilingAppliesEveryLever()
    {
        var catalog = _whatIf.EvaluateAllLevers(WithoutMfa());

        Assert.True(catalog.Ceiling.Score > catalog.Baseline.Score);
        Assert.Equal(catalog.Ceiling.Score - catalog.Baseline.Score, catalog.Headroom);

        // Con todas las palancas aplicadas, todo el peso entra al calculo — aunque
        // el score no llegue a 100 porque hay compuestos que no se pueden simular.
        Assert.Equal(100m, catalog.Ceiling.Denominator);
    }

    // ----------------------------------------------------------- parametrizadas

    [Fact]
    public void ModernizingProductsMovesTheWorstOnesFirst()
    {
        var snapshot = GoldenFixtures.Eai10969() with
        {
            ArchitecturalValue = new ArchitecturalValue
            {
                ModernCount = 10,
                HeritageCount = 4,
                ObsoleteCount = 3,
                NotApprovedForUseCount = 3,
            },
        };

        // Modernizar 3 productos toca primero los obsoletos, que puntuan 20.
        var scenario = _whatIf.Evaluate(snapshot, [new LeverSelection("archValue.modernize", 3)]);

        Assert.True(scenario.Delta > 0);

        var before = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "archValue");
        var after = scenario.Projected.Metrics.Single(m => m.MetricKey == "archValue");
        Assert.True(after.MappedScore > before.MappedScore);
    }

    [Fact]
    public void ParameterizedLeverRejectsAnInvalidQuantity()
    {
        var ex = Assert.Throws<WhatIfException>(() =>
            _whatIf.Evaluate(GoldenFixtures.Eai10969(), [new LeverSelection("archValue.modernize", 0)]));

        Assert.Contains("archValue.modernize", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownLeverIsRejectedWithTheValidOptions()
    {
        var ex = Assert.Throws<WhatIfException>(() =>
            _whatIf.Evaluate(GoldenFixtures.Eai10969(), [new LeverSelection("palanca.inventada")]));

        // Lista las opciones validas, pero no repite lo que envio el cliente: ese
        // mensaje termina en una respuesta HTTP.
        Assert.DoesNotContain("palanca.inventada", ex.Message, StringComparison.Ordinal);
        Assert.Contains("mfa.enable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioRejectsTooManyLevers()
    {
        var many = Enumerable
            .Repeat(new LeverSelection("mfa.enable"), WhatIfEngine.MaxLeversPerScenario + 1)
            .ToList();

        Assert.Throws<WhatIfException>(() => _whatIf.Evaluate(GoldenFixtures.Eai10969(), many));
    }

    // --------------------------------------------------------------- invariantes

    /// <summary>Un escenario nunca muta el snapshot real: opera sobre una copia.</summary>
    [Fact]
    public void ScenarioDoesNotMutateTheSnapshot()
    {
        var snapshot = WithoutMfa();

        _whatIf.Evaluate(snapshot, [new LeverSelection("mfa.enable"), new LeverSelection("techDebt.clear")]);

        Assert.False(snapshot.UtilizesMfa);
        Assert.Null(snapshot.TechDebtScore);
        Assert.Equal(74.01m, _engine.Calculate(snapshot).Score);
    }

    [Fact]
    public void NoLeverEverLowersTheScore()
    {
        var snapshot = WithoutMfa();

        Assert.All(
            _whatIf.EvaluateAllLevers(snapshot).Levers,
            effect => Assert.True(effect.Delta >= 0m, $"'{effect.Key}' bajo el score en {effect.Delta}."));
    }
}
