import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# Validar el rango del objetivo: fuera de la escala del modelo no es una busqueda,
# es una entrada invalida.
edit(os.path.join(root, r"src\shared\Modernization.WhatIf\WhatIfEngine.cs"), [
    ("""    public TargetResult ReachTarget(ApplicationSnapshot snapshot, decimal targetScore)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var baseline = engine.Calculate(snapshot);""",
     """    public TargetResult ReachTarget(ApplicationSnapshot snapshot, decimal targetScore)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // El modelo normaliza a 20-100. Un objetivo fuera de esa escala no es una
        // busqueda dificil: es una entrada invalida.
        if (targetScore is < 20m or > 100m)
        {
            throw new WhatIfException(
                $"El score objetivo debe estar entre 20 y 100; se recibio {targetScore}.");
        }

        var baseline = engine.Calculate(snapshot);"""),
])

# El test: el techo es 100 porque el catalogo cubre todas las metricas puntuadas.
edit(os.path.join(root, r"tests\Modernization.Scoring.Engine.Tests\WhatIfTests.cs"), [
    ("""    [Fact]
    public void UnreachableTargetIsReportedAsSuch()
    {
        // El techo de esta aplicacion esta muy por debajo de 100.
        var snapshot = GoldenFixtures.Eai14381() with { ArchitecturalValue = null, ProductStrategy = null };

        var result = _whatIf.ReachTarget(snapshot, 100m);

        Assert.False(result.IsReachable);
        Assert.Null(result.Result);
        Assert.True(result.Ceiling.Score < 100m);
    }""",

     """    /// <summary>
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
    }"""),
])
