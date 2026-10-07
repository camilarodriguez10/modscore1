using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// La línea de tiempo del portafolio dummy: las mismas aplicaciones en trimestres
/// anteriores, para comparar score, Currency Score y riesgo en el tiempo.
/// </summary>
public sealed class DummyTimelineTests
{
    private static readonly DummyPortfolio Current = DummyPortfolio.Generate();

    [Fact]
    public void EveryQuarterHasTheSameApplications()
    {
        foreach (var quarters in new[] { 1, 2, 3 })
        {
            var past = DummyTimeline.Rewind(Current, quarters);

            Assert.Equal(Current.Eai.Select(e => e.EaiCode), past.Eai.Select(e => e.EaiCode));
            Assert.Equal(Current.Eai.Select(e => e.ApplicationName), past.Eai.Select(e => e.ApplicationName));
            Assert.Equal(Current.CapturedAt.AddMonths(-3 * quarters), past.CapturedAt);
        }

        Assert.Same(Current, DummyTimeline.Rewind(Current, 0));
    }

    [Fact]
    public void TheSameQuarterIsAlwaysTheSame()
    {
        var first = DummyTimeline.Rewind(Current, 2);
        var second = DummyTimeline.Rewind(DummyPortfolio.Generate(), 2);

        Assert.Equal(first.Eai.Select(e => e.RawTechScore), second.Eai.Select(e => e.RawTechScore));
        Assert.Equal(first.ScaFlaws.Count, second.ScaFlaws.Count);
    }

    [Fact]
    public async Task TheBrdExampleStaysAt7967InEveryQuarter()
    {
        var engine = new ScoringEngine(RulesetCatalog.Default);

        foreach (var quarters in new[] { 1, 2, 3 })
        {
            var snapshot = await new DummySnapshotProvider(DummyTimeline.Rewind(Current, quarters)).GetAllAsync();

            Assert.Equal(80.70m, engine.Calculate(snapshot.Find(DummyPortfolio.ReferenceEaiCode)!).Score);
        }
    }

    /// <summary>
    /// Los flaws no se inventan: un flaw encontrado después de la fecha no existía, y uno
    /// resuelto después seguía abierto.
    /// </summary>
    [Fact]
    public void FlawsFollowTheirOwnDates()
    {
        var past = DummyTimeline.Rewind(Current, 1);
        var at = past.CapturedAt;

        Assert.All(past.ScaFlaws, f => Assert.False(f.FirstFoundDate > at));
        Assert.All(past.ScaFlaws, f => Assert.False(f.ResolvedDate > at));

        var reopened = Current.ScaFlaws.Count(f => f.ResolvedDate > at && !(f.FirstFoundDate > at));
        Assert.True(reopened > 0);
        Assert.Equal(
            Current.ScaFlaws.Count(f => f.FindingStatus == "Open" && !(f.FirstFoundDate > at)) + reopened,
            past.ScaFlaws.Count(f => f.FindingStatus == "Open"));
    }

    /// <summary>Las que se modernizan tenían peor Currency Score antes; las que envejecen, mejor.</summary>
    [Fact]
    public void ApplicationsMoveInBothDirections()
    {
        var past = DummyTimeline.Rewind(Current, 3).Eai.ToDictionary(e => e.EaiCode);
        var pairs = Current.Eai
            .Where(e => e.RawTechScore is > 0m and < 100m && past[e.EaiCode].RawTechScore is > 0m and < 100m)
            .Select(e => (Now: e.RawTechScore!.Value, Then: past[e.EaiCode].RawTechScore!.Value))
            .ToList();

        Assert.Contains(pairs, p => p.Then < p.Now - 5m);
        Assert.Contains(pairs, p => p.Then > p.Now + 5m);
    }

    /// <summary>La línea de tiempo versionada en data/timeline se lee como cuatro cargas.</summary>
    [Fact]
    public async Task TheCommittedTimelineReadsAsFourUploads()
    {
        var directory = Path.Combine(RepositoryRoot(), "data", "timeline");
        var provider = new FileSnapshotProvider(directory);

        var versions = await provider.GetVersionsAsync();

        Assert.Equal(4, versions.Count);
        Assert.Equal(Current.CapturedAt, versions[0].CapturedAt);

        foreach (var version in versions)
        {
            var snapshot = await provider.GetAllAsync(version.Id);
            Assert.Equal(Current.Eai.Count, snapshot.Applications.Count);
            Assert.NotNull(snapshot.Find(DummyPortfolio.ReferenceEaiCode));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ModernizationScore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
