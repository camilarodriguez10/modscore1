using Modernization.Insights;
using Modernization.Sources.Dummy;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Los repositorios de demostración de la pantalla Code: deterministas, atados al
/// portafolio vigente y hablando el mismo idioma que el catálogo de fin de vida.
/// </summary>
public sealed class SampleCodeInventoryProviderTests
{
    [Fact]
    public async Task TheSameApplicationAlwaysHasTheSameRepositories()
    {
        var snapshots = new DummySnapshotProvider(applicationCount: 80);
        using var first = new SampleCodeInventoryProvider(snapshots);
        using var second = new SampleCodeInventoryProvider(new DummySnapshotProvider(applicationCount: 80));

        var a = await first.GetAsync();
        var b = await second.GetAsync();

        Assert.Equal(
            a.Repositories.Select(r => (r.Name, r.LastCommit, string.Join(',', r.Components.Select(c => c.Product + c.Version)))),
            b.Repositories.Select(r => (r.Name, r.LastCommit, string.Join(',', r.Components.Select(c => c.Product + c.Version)))));
    }

    [Fact]
    public async Task ItSaysItIsSampleDataAndNotConnected()
    {
        using var provider = new SampleCodeInventoryProvider(new DummySnapshotProvider(applicationCount: 40));
        var snapshot = await new DummySnapshotProvider(applicationCount: 40).GetAllAsync();

        var inventory = await provider.GetAsync();

        Assert.Equal("Sample", inventory.Provider);
        Assert.False(inventory.Connected);
        Assert.Equal(snapshot.CapturedAt, inventory.ScannedAt);
    }

    [Fact]
    public async Task EveryRepositoryBelongsToAnApplicationAndHasAUniqueName()
    {
        var snapshot = await new DummySnapshotProvider().GetAllAsync();
        var repositories = SampleCodeInventoryProvider.Generate(
            snapshot.Applications,
            DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime));
        var codes = snapshot.Applications.Select(a => a.EaiCode).ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(repositories);
        Assert.All(repositories, r => Assert.Contains(r.EaiCode, codes));
        Assert.Equal(repositories.Count, repositories.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task MainframeAndSapApplicationsRarelyHaveRepositories()
    {
        var snapshot = await new DummySnapshotProvider().GetAllAsync();
        var repositories = SampleCodeInventoryProvider.Generate(snapshot.Applications, new DateOnly(2026, 9, 26));
        var withRepositories = repositories.Select(r => r.EaiCode).ToHashSet(StringComparer.Ordinal);

        var legacyStacks = snapshot.Applications.Where(a => a.CodingLanguage is "COBOL" or "ABAP").ToList();

        Assert.NotEmpty(legacyStacks);
        Assert.True(legacyStacks.Count(a => withRepositories.Contains(a.EaiCode)) * 3 < legacyStacks.Count);
    }

    /// <summary>
    /// Un producto mal escrito no fallaría en ningún lado: aparecería como "no evaluable".
    /// Todo lo que genera la demostración está en el catálogo, salvo WebSphere, que falta a
    /// propósito para que la pantalla muestre ese caso.
    /// </summary>
    [Fact]
    public async Task EveryGeneratedComponentIsInTheCatalogButTheDeliberateGap()
    {
        var snapshot = await new DummySnapshotProvider().GetAllAsync();
        var repositories = SampleCodeInventoryProvider.Generate(snapshot.Applications, new DateOnly(2026, 9, 26));
        var catalog = EndOfLifeCatalog.Default;

        var products = repositories.SelectMany(r => r.Components).Select(c => c.Product).Distinct(StringComparer.Ordinal).ToList();

        Assert.All(products.Where(p => p != "websphere"), p => Assert.NotNull(catalog.Find(p)));
        Assert.All(
            repositories.SelectMany(r => r.Components).Where(c => c.Product != "websphere" && c.Version != "8.2.14"),
            c => Assert.NotNull(catalog.Match(c.Product, c.Version)));
    }
}
