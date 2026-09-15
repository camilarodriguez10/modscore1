using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Domain;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// El adaptador Dummy corriendo contra el contrato compartido, mas lo que solo
/// aplica a datos sinteticos: determinismo por semilla y cobertura de casos borde.
/// </summary>
public sealed class DummySnapshotProviderTests : SnapshotProviderContract
{
    protected override IApplicationSnapshotProvider CreateProvider() => new DummySnapshotProvider();

    protected override string ExpectedProviderName => "Dummy";

    [Fact]
    public async Task GeneratesTheRequestedNumberOfApplications()
    {
        var snapshot = await new DummySnapshotProvider(applicationCount: 42).GetAllAsync();

        Assert.Equal(42, snapshot.Applications.Count);
    }

    [Fact]
    public async Task SameSeedProducesIdenticalData()
    {
        var first = await new DummySnapshotProvider(seed: 7, applicationCount: 60).GetAllAsync();
        var second = await new DummySnapshotProvider(seed: 7, applicationCount: 60).GetAllAsync();

        Assert.Equal(
            first.Applications.Select(a => a.ApplicationName),
            second.Applications.Select(a => a.ApplicationName));
    }

    [Fact]
    public async Task DifferentSeedsProduceDifferentData()
    {
        var first = await new DummySnapshotProvider(seed: 7, applicationCount: 60).GetAllAsync();
        var second = await new DummySnapshotProvider(seed: 8, applicationCount: 60).GetAllAsync();

        Assert.NotEqual(
            first.Applications.Select(a => a.EaiCode),
            second.Applications.Select(a => a.EaiCode));
    }

    /// <summary>
    /// Las filas cuyo EAI no existe en el maestro deben aparecer como rechazos.
    /// Un archivo desalineado con el inventario es un problema real de datos y la
    /// pantalla de linaje tiene que poder mostrarlo.
    /// </summary>
    [Fact]
    public async Task ReportsOrphanRowsAsRejections()
    {
        var snapshot = await CreateProvider().GetAllAsync();
        var oss = snapshot.Sources.Single(s => s.SourceName == "OSS Score");

        Assert.NotEmpty(oss.Rejections);
        Assert.All(oss.Rejections, r => Assert.Equal(NoticeCodes.RowUnknownEai, r.Reason.Code));
    }

    /// <summary>
    /// Ninguna fuente debe cubrir el 100% salvo el maestro: un portafolio sin
    /// huecos no ejercitaria la exclusion de metricas sin dato, que es la parte
    /// mas delicada de la formula.
    /// </summary>
    [Fact]
    public async Task EverySecondarySourceHasPartialCoverage()
    {
        var snapshot = await CreateProvider().GetAllAsync();

        foreach (var source in snapshot.Sources.Where(s => s.SourceName != "EAI Master"))
        {
            var coverage = snapshot.CoverageOf(source.SourceName);
            Assert.InRange(coverage, 40m, 99.9m);
        }
    }

    [Fact]
    public async Task PortfolioSpreadsAcrossTheScoreRange()
    {
        var engine = new ScoringEngine(RulesetCatalog.Default);
        var snapshot = await CreateProvider().GetAllAsync();

        var scores = snapshot.Applications.Select(a => engine.Calculate(a).Score).ToList();

        Assert.True(scores.Max() - scores.Min() > 25m, "El portafolio dummy deberia cubrir un rango amplio de scores.");
    }

    [Fact]
    public async Task PortfolioCoversEveryDispositionAndRoadmap()
    {
        var apps = (await CreateProvider().GetAllAsync()).Applications;

        Assert.Equal(3, apps.Select(a => a.Disposition).Where(d => d is not null).Distinct().Count());
        Assert.Equal(7, apps.Select(a => a.Roadmap).Where(r => r is not null).Distinct().Count());
    }

    [Fact]
    public async Task PortfolioCoversEveryApiExposureType()
    {
        var apps = (await CreateProvider().GetAllAsync()).Applications;

        Assert.Equal(4, apps.Select(a => a.NativeApi).Where(a => a is not null).Distinct().Count());
    }
}
