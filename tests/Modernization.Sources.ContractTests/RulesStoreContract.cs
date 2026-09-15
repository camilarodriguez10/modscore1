using System.Globalization;
using Modernization.Domain.Rules;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Lo que cualquier almacen de reglas tiene que cumplir. Cada adaptador hereda la
/// suite: memoria, archivos y base de datos se comportan igual o una prueba lo dice.
/// </summary>
public abstract class RulesStoreContract
{
    protected abstract IRulesStore Store { get; }

    protected static StoredRulesVersion Version(string id, string? basedOn = null) => new()
    {
        Id = id,
        BasedOn = basedOn,
        PublishedAt = new DateTimeOffset(2026, 9, 12, 15, 30, 0, TimeSpan.Zero),
        PublishedBy = "tests",
        Reason = "Rebalance risk weights",
        Ruleset = "{\"version\":\"" + id + "\"}",
        Levers = "{\"levers\":[]}",
        Roadmap = "{\"version\":\"" + id + "\"}",
        Changes = "[]",
        Impact = string.Empty,
    };

    [Fact]
    public async Task AnEmptyStoreHasNoVersions()
    {
        Assert.Null(await Store.GetLatestAsync());
        Assert.Empty(await Store.ListAsync(10));
    }

    [Fact]
    public async Task APublicationBecomesTheLatestAndReadsBackIntact()
    {
        var first = Version("v1.4", "v1.3");

        await Store.PublishAsync(first, expectedLatestId: null);
        await Store.PublishAsync(Version("v1.5", "v1.4"), expectedLatestId: "v1.4");

        Assert.Equal("v1.5", (await Store.GetLatestAsync())?.Id);
        Assert.Equal(["v1.5", "v1.4"], (await Store.ListAsync(10)).Select(v => v.Id));
        Assert.Equal(first, await Store.GetAsync("v1.4"));
    }

    /// <summary>
    /// Concurrencia optimista: un borrador preparado sobre una version que ya no es la
    /// ultima no pisa lo que se publico entre medio.
    /// </summary>
    [Fact]
    public async Task APublicationPreparedOnAnOlderVersionIsAConflict()
    {
        await Store.PublishAsync(Version("v1.4"), expectedLatestId: null);

        await Assert.ThrowsAsync<RulesStoreConflictException>(() => Store.PublishAsync(Version("v1.5"), expectedLatestId: null));
        await Assert.ThrowsAsync<RulesStoreConflictException>(() => Store.PublishAsync(Version("v1.4"), expectedLatestId: "v1.4"));

        Assert.Single(await Store.ListAsync(10));
    }

    [Fact]
    public async Task VersionsOrderByNumberNotByText()
    {
        await Store.PublishAsync(Version("v1.9"), expectedLatestId: null);
        await Store.PublishAsync(Version("v1.10"), expectedLatestId: "v1.9");

        Assert.Equal("v1.10", (await Store.GetLatestAsync())?.Id);
    }

    /// <summary>En el almacen de archivos un identificador es un directorio: su forma se valida antes.</summary>
    [Theory]
    [InlineData("../v1.4")]
    [InlineData("v1.4/..")]
    [InlineData("V1.4")]
    [InlineData("v1")]
    public async Task MalformedIdentifiersAreNeverLookedUpOrWritten(string id)
    {
        Assert.Null(await Store.GetAsync(id));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.PublishAsync(Version(id), expectedLatestId: null));
    }
}

public sealed class InMemoryRulesStoreTests : RulesStoreContract
{
    protected override IRulesStore Store { get; } = new InMemoryRulesStore();
}

public sealed class FileRulesStoreTests : RulesStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-rules-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private readonly FileRulesStore _store;

    public FileRulesStoreTests() => _store = new FileRulesStore(_root);

    protected override IRulesStore Store => _store;

    /// <summary>
    /// Las reglas viven por defecto dentro del directorio de cargas. El proveedor de
    /// archivos no puede confundir ese directorio con una carga.
    /// </summary>
    [Fact]
    public async Task APublicationLeavesOnlyItsVersionAndUploadsIgnoreIt()
    {
        var history = Path.Combine(_root, "history");
        using var store = new FileRulesStore(Path.Combine(history, FileRulesStore.DefaultDirectoryName));

        await store.PublishAsync(Version("v1.4"), expectedLatestId: null);

        Assert.Equal(
            ["v1.4"],
            Directory.EnumerateDirectories(Path.Combine(history, FileRulesStore.DefaultDirectoryName)).Select(Path.GetFileName));
        Assert.Empty(await new FileSnapshotProvider(history).GetVersionsAsync());
    }

    /// <summary>Una copia movida a mano bajo otro nombre no se sirve como si fuera esa version.</summary>
    [Fact]
    public async Task ACopyUnderAnotherNameIsNotServedAsThatVersion()
    {
        await _store.PublishAsync(Version("v1.4"), expectedLatestId: null);

        Directory.Move(Path.Combine(_root, "v1.4"), Path.Combine(_root, "v1.7"));

        Assert.Null(await _store.GetAsync("v1.7"));
        Assert.Null(await _store.GetLatestAsync());
    }

    public void Dispose()
    {
        _store.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
