import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

edit(os.path.join(root, r"src\shared\Modernization.WhatIf\ScenarioLever.cs"), [
    # Marcar explicitamente cuando una palanca compuesta no es accionable.
    ("""    /// <summary>Cuantos productos mover, para las palancas que lo admiten.</summary>
    public int? DefaultParameter { get; init; }

    public bool IsParameterized => DefaultParameter is not null;""",
     """    /// <summary>Cuantos productos mover, para las palancas que lo admiten.</summary>
    public int? DefaultParameter { get; init; }

    /// <summary>
    /// Motivo por el que la palanca no se puede mover en esta aplicacion, mas alla
    /// de que ya este lograda. Null cuando si es accionable.
    /// </summary>
    public Func<ApplicationSnapshot, string?>? NotActionableReason { get; init; }

    public bool IsParameterized => DefaultParameter is not null;"""),

    ("""            Apply = static (s, count) => ModernizeProducts(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ArchitecturalValue is null
                || (s.ArchitecturalValue.ObsoleteCount is null or 0
                    && s.ArchitecturalValue.NotApprovedForUseCount is null or 0
                    && s.ArchitecturalValue.HeritageCount is null or 0),""",
     """            Apply = static (s, count) => ModernizeProducts(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ArchitecturalValue is null
                || s.ArchitecturalValue.ProvidedValue is not null
                || (s.ArchitecturalValue.ObsoleteCount is null or 0
                    && s.ArchitecturalValue.NotApprovedForUseCount is null or 0
                    && s.ArchitecturalValue.HeritageCount is null or 0),
            NotActionableReason = static s => s.ArchitecturalValue?.ProvidedValue is not null
                ? "CMT entrega el valor compuesto ya calculado, sin el detalle por producto: "
                  + "no hay conteos que reclasificar."
                : null,"""),

    ("""            Apply = static (s, count) => ReclassifyToInvest(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ProductStrategy is null
                || (s.ProductStrategy.EliminateCount is null or 0
                    && s.ProductStrategy.MigrateCount is null or 0
                    && s.ProductStrategy.TolerateCount is null or 0),""",
     """            Apply = static (s, count) => ReclassifyToInvest(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ProductStrategy is null
                || s.ProductStrategy.ProvidedValue is not null
                || (s.ProductStrategy.EliminateCount is null or 0
                    && s.ProductStrategy.MigrateCount is null or 0
                    && s.ProductStrategy.TolerateCount is null or 0),
            NotActionableReason = static s => s.ProductStrategy?.ProvidedValue is not null
                ? "MTS entrega el valor compuesto ya calculado, sin el detalle por producto: "
                  + "no hay conteos que reclasificar."
                : null,"""),
])

edit(os.path.join(root, r"src\shared\Modernization.WhatIf\ScenarioResults.cs"), [
    ("""    /// <summary>La aplicacion ya esta en ese estado. No es una oportunidad.</summary>
    public required bool IsAlreadyAchieved { get; init; }""",
     """    /// <summary>La aplicacion ya esta en ese estado. No es una oportunidad.</summary>
    public required bool IsAlreadyAchieved { get; init; }

    /// <summary>
    /// Por que la palanca no se puede mover en esta aplicacion, cuando el motivo
    /// no es que ya este lograda. Null si es accionable.
    /// </summary>
    public string? NotActionableReason { get; init; }"""),

    ("""    /// <summary>Score maximo con todas las palancas aplicadas.</summary>
    public required ScoreBreakdown Ceiling { get; init; }""",
     """    /// <summary>Score maximo con todas las palancas aplicadas.</summary>
    public required ScoreBreakdown Ceiling { get; init; }

    /// <summary>
    /// Palancas que no se pueden mover en esta aplicacion por como llega el dato,
    /// no por estar ya logradas. Explican un techo por debajo de 100.
    /// </summary>
    public IReadOnlyList<LeverEffect> NotActionable =>
        [.. Levers.Where(l => l.NotActionableReason is not null)];"""),
])

edit(os.path.join(root, r"src\shared\Modernization.WhatIf\WhatIfEngine.cs"), [
    ("""        if (lever.IsAlreadyAchieved(snapshot))
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
            };
        }""",
     """        if (lever.IsAlreadyAchieved(snapshot))
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
        }"""),
])

# El test refleja la realidad del modelo, con los dos casos separados.
edit(os.path.join(root, r"tests\Modernization.Scoring.Engine.Tests\WhatIfTests.cs"), [
    ("""    /// <summary>
    /// El catalogo cubre las trece metricas puntuadas, asi que el techo de
    /// cualquier aplicacion con dato es 100. Dicho de otro modo: ninguna
    /// aplicacion del portafolio esta estructuralmente limitada — todo lo que le
    /// falta a un score es trabajo, no imposibilidad.
    /// </summary>
    [Fact]
    public void EveryApplicationCanReachOneHundred()
    {
        foreach (var snapshot in new[] { WithoutMfa(), GoldenFixtures.Eai14381() })
        {
            var result = _whatIf.ReachTarget(snapshot, 100m);

            Assert.True(result.IsReachable);
            Assert.Equal(100m, result.Ceiling.Score);
        }
    }""",

     """    /// <summary>
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
        Assert.All(catalog.NotActionable, l => Assert.NotEmpty(l.NotActionableReason!));

        // 100 − 10 − 10 + 7,572 + 8,837 = 96,409 sobre 100 => 97,13
        Assert.Equal(97.13m, catalog.Ceiling.Score);

        var result = _whatIf.ReachTarget(GoldenFixtures.Eai10969(), 100m);
        Assert.False(result.IsReachable);
        Assert.Null(result.Result);
    }"""),

    ("""    [Fact]
    public void CeilingAppliesEveryLever()
    {
        var catalog = _whatIf.EvaluateAllLevers(WithoutMfa());

        Assert.True(catalog.Ceiling.Score > catalog.Baseline.Score);
        Assert.Equal(catalog.Ceiling.Score - catalog.Baseline.Score, catalog.Headroom);

        // Con todas las palancas aplicadas, todo el peso entra al calculo.
        Assert.Equal(100m, catalog.Ceiling.Denominator);
    }""",

     """    [Fact]
    public void CeilingAppliesEveryLever()
    {
        var catalog = _whatIf.EvaluateAllLevers(WithoutMfa());

        Assert.True(catalog.Ceiling.Score > catalog.Baseline.Score);
        Assert.Equal(catalog.Ceiling.Score - catalog.Baseline.Score, catalog.Headroom);

        // Con todas las palancas aplicadas, todo el peso entra al calculo — aunque
        // el score no llegue a 100 porque hay compuestos que no se pueden simular.
        Assert.Equal(100m, catalog.Ceiling.Denominator);
    }"""),
])
