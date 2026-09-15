using Modernization.Domain.Rules;
using Modernization.Rules;
using Modernization.SystemApi.Services;

namespace Modernization.Api.Tests;

/// <summary>
/// Un almacen de reglas caido no puede dejar sin scores al portafolio: se sigue
/// puntuando con las reglas vigentes y se dice.
/// </summary>
public sealed class RulesStoreFailureTests
{
    [Fact]
    public async Task AStoreThatStopsResponding_KeepsTheActiveRules()
    {
        var store = new FlakyRulesStore();
        await store.Inner.PublishAsync(Version("v1.4"), expectedLatestId: null);

        using var rules = new RulesService(store, refreshInterval: TimeSpan.Zero);
        Assert.Equal("v1.4", (await rules.GetCurrentAsync()).Id);

        store.Failing = true;

        Assert.Equal("v1.4", (await rules.GetCurrentAsync()).Id);
    }

    [Fact]
    public async Task ListingWithTheStoreDown_ShowsTheActiveAndTheBuiltInVersions()
    {
        var store = new FlakyRulesStore();
        await store.Inner.PublishAsync(Version("v1.4"), expectedLatestId: null);

        using var rules = new RulesService(store, refreshInterval: TimeSpan.Zero);
        await rules.GetCurrentAsync();
        store.Failing = true;

        var versions = await rules.ListAsync();

        Assert.Equal(["v1.4", RulesModel.BuiltIn.Id], versions.Select(v => v.Id));
    }

    [Fact]
    public async Task ReadingAnOldVersionWithTheStoreDown_SaysSoInsteadOfNotFound()
    {
        var store = new FlakyRulesStore();
        await store.Inner.PublishAsync(Version("v1.4"), expectedLatestId: null);
        await store.Inner.PublishAsync(Version("v1.5"), expectedLatestId: "v1.4");

        using var rules = new RulesService(store, refreshInterval: TimeSpan.Zero);
        await rules.GetCurrentAsync();
        store.Failing = true;

        await Assert.ThrowsAsync<RulesStoreUnavailableException>(() => rules.GetVersionAsync("v1.4"));
    }

    [Fact]
    public async Task AVersionAlreadyRead_IsServedWithoutTheStore()
    {
        var store = new FlakyRulesStore();
        await store.Inner.PublishAsync(Version("v1.4"), expectedLatestId: null);
        await store.Inner.PublishAsync(Version("v1.5"), expectedLatestId: "v1.4");

        using var rules = new RulesService(store, refreshInterval: TimeSpan.Zero);
        Assert.NotNull(await rules.GetVersionAsync("v1.4"));

        // Una version publicada no cambia nunca: recordarla es seguro.
        store.Failing = true;

        Assert.Equal("v1.4", (await rules.GetVersionAsync("v1.4"))?.Id);
    }

    private static StoredRulesVersion Version(string id)
        => RulesModel.BuiltIn.WithInfo(new RulesVersionInfo
        {
            Id = id,
            BasedOn = RulesModel.BuiltIn.Id,
            PublishedAt = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
            PublishedBy = "admin-1",
            Reason = "Store failure fixture",
            Changes = [],
        }).ToStored();

    /// <summary>Almacen en memoria que se puede apagar.</summary>
    private sealed class FlakyRulesStore : IRulesStore
    {
        public InMemoryRulesStore Inner { get; } = new();

        public bool Failing { get; set; }

        public string Kind => "Flaky";

        public Task<StoredRulesVersion?> GetLatestAsync(CancellationToken cancellationToken = default)
            => Guard(() => Inner.GetLatestAsync(cancellationToken));

        public Task<StoredRulesVersion?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Guard(() => Inner.GetAsync(id, cancellationToken));

        public Task<IReadOnlyList<StoredRulesVersion>> ListAsync(int max, CancellationToken cancellationToken = default)
            => Guard(() => Inner.ListAsync(max, cancellationToken));

        public Task PublishAsync(StoredRulesVersion version, string? expectedLatestId, CancellationToken cancellationToken = default)
            => Guard(async () =>
            {
                await Inner.PublishAsync(version, expectedLatestId, cancellationToken);
                return true;
            });

        private Task<T> Guard<T>(Func<Task<T>> action)
            => Failing ? Task.FromException<T>(new IOException("The volume is not mounted.")) : action();
    }
}
