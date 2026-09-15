using Modernization.Domain.Rules;
using Modernization.Sources.Database;

namespace Modernization.Sources.ContractTests;

/// <summary>El mismo contrato contra una base SQLite real, con la migracion aplicada.</summary>
public sealed class DatabaseRulesStoreTests : RulesStoreContract, IDisposable
{
    private readonly DatabaseHarness _harness = new(seed: false);
    private readonly DatabaseRulesStore _store;

    public DatabaseRulesStoreTests() => _store = new DatabaseRulesStore(_harness.Contexts);

    protected override IRulesStore Store => _store;

    public void Dispose() => _harness.Dispose();
}
