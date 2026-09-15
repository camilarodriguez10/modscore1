using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;
using Modernization.Sources.Database;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;
using Modernization.Domain;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Una base SQLite en disco con una carga ya publicada por el camino real: los CSV
/// del portafolio dummy entran por el publicador, no por un atajo de inserción.
/// </summary>
/// <remarks>
/// SQLite es un motor de verdad —archivo, transacciones, tipos, índices—, así que
/// la suite de contrato corre contra una base real. El motor de despliegue sigue
/// siendo RFC-07: el modelo es agnóstico y lo que cambia es el paquete.
/// </remarks>
public sealed class DatabaseHarness : IDisposable
{
    private readonly string _root;

    public DatabaseHarness(int retainVersions = 0, bool seed = true)
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "modscore-db-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(_root);

        var options = new DbContextOptionsBuilder<ModernizationDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_root, "modernization.db")}")
            .Options;

        Contexts = new ContextFactory(options);

        using (var db = Contexts.CreateDbContext())
        {
            db.Database.Migrate();
        }

        Provider = new DatabaseSnapshotProvider(Contexts);
        Publisher = new DatabaseSnapshotPublisher(Contexts, SourceSchemaCatalog.Default, retainVersions);

        if (seed)
        {
            Publish("Carga inicial", DummyPortfolio.Generate(applicationCount: 40));
        }
    }

    public IDbContextFactory<ModernizationDbContext> Contexts { get; }

    public DatabaseSnapshotProvider Provider { get; }

    public DatabaseSnapshotPublisher Publisher { get; }

    /// <summary>Publica un portafolio exportándolo a CSV y subiéndolo, como haría una persona.</summary>
    public PublishOutcome Publish(string label, DummyPortfolio? portfolio = null)
    {
        portfolio ??= DummyPortfolio.Generate(applicationCount: 40);

        var directory = Path.Combine(_root, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        CsvSourceExporter.Export(portfolio, directory);

        var files = Directory.EnumerateFiles(directory, "*.csv")
            .Select(path => new IncomingSourceFile(
                Path.GetFileName(path),
                new FileInfo(path).Length,
                () => File.OpenRead(path)))
            .ToList();

        var outcome = Publisher.PublishAsync(files, label, "tests", portfolio.CapturedAt)
            .GetAwaiter().GetResult();

        if (!outcome.Accepted)
        {
            throw new InvalidOperationException("La carga de prueba no se publicó: " + string.Join(" · ", outcome.Errors));
        }

        return outcome;
    }

    /// <summary>Sube un archivo suelto, para los casos de rechazo.</summary>
    public PublishOutcome PublishRaw(string label, params (string Name, string Content)[] files)
    {
        var incoming = files
            .Select(f => new IncomingSourceFile(
                f.Name,
                f.Content.Length,
                () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(f.Content))))
            .ToList();

        return Publisher.PublishAsync(incoming, label, "tests", DateTimeOffset.UtcNow).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Publisher.Dispose();

        // SQLite mantiene el archivo abierto hasta que se liberan los pools.
        SqliteConnectionPool.Clear();

        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // El archivo sigue tomado por el proceso: es un temporal, no importa.
            }
        }
    }

    private sealed class ContextFactory(DbContextOptions<ModernizationDbContext> options)
        : IDbContextFactory<ModernizationDbContext>
    {
        public ModernizationDbContext CreateDbContext() => new(options);
    }
}

internal static class SqliteConnectionPool
{
    public static void Clear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}

/// <summary>El tercer adaptador contra el mismo contrato que Dummy y File.</summary>
public sealed class DatabaseProviderContractTests : SnapshotProviderContract, IDisposable
{
    private readonly DatabaseHarness _harness = new();

    protected override IApplicationSnapshotProvider CreateProvider() => _harness.Provider;

    protected override string ExpectedProviderName => DatabaseSnapshotProvider.ProviderName;

    public void Dispose() => _harness.Dispose();
}

public sealed class DatabaseAdapterTests
{
    private static readonly ScoringEngine Engine = new(RulesetCatalog.Default);

    /// <summary>
    /// Lo que la Fase 09 tiene que demostrar: el mismo dato, leído desde una base,
    /// puntúa exactamente igual que leído desde archivos.
    /// </summary>
    [Fact]
    public async Task TheReferenceApplicationScoresTheSameFromTheDatabase()
    {
        using var harness = new DatabaseHarness();

        var snapshot = await harness.Provider.GetAllAsync();
        var reference = snapshot.Find("14381");

        Assert.NotNull(reference);
        Assert.Equal(79.67m, Engine.Calculate(reference).Score);
    }

    [Fact]
    public async Task EachPublicationAddsAVersionAndTheNewestIsCurrent()
    {
        using var harness = new DatabaseHarness();

        harness.Publish("Segunda carga", DummyPortfolio.Generate(
            applicationCount: 25,
            capturedAt: DateTimeOffset.UtcNow.AddDays(1)));

        var versions = await harness.Provider.GetVersionsAsync();

        Assert.Equal(2, versions.Count);
        Assert.True(versions[0].IsCurrent);
        Assert.Equal(25, versions[0].ApplicationCount);

        // La anterior sigue consultable, con su propio portafolio.
        var previous = await harness.Provider.GetAllAsync(versions[1].Id);
        Assert.Equal(40, previous.Applications.Count);
    }

    /// <summary>
    /// Lo que la fase de archivos no podía dar: retención declarativa. Con archivos
    /// hay que borrar directorios a mano.
    /// </summary>
    [Fact]
    public async Task RetentionKeepsOnlyTheNewestVersions()
    {
        using var harness = new DatabaseHarness(retainVersions: 2);

        harness.Publish("Segunda", DummyPortfolio.Generate(applicationCount: 30, capturedAt: DateTimeOffset.UtcNow.AddDays(1)));
        harness.Publish("Tercera", DummyPortfolio.Generate(applicationCount: 20, capturedAt: DateTimeOffset.UtcNow.AddDays(2)));

        var versions = await harness.Provider.GetVersionsAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(["Tercera", "Segunda"], versions.Select(v => v.Label));

        // Y las filas de la carga retirada tampoco quedan.
        await using var db = await harness.Contexts.CreateDbContextAsync();
        var total = await db.Eai.CountAsync();
        Assert.Equal(50, total);
    }

    /// <summary>
    /// Una fila rechazada tiene que sobrevivir a la publicación: con archivos se
    /// recalcula al leer, pero aquí el archivo ya no existe.
    /// </summary>
    [Fact]
    public async Task RejectedRowsStayVisibleInTheLineage()
    {
        using var harness = new DatabaseHarness();

        var outcome = harness.PublishRaw(
            "Con una fila mala",
            ("eai_master.csv", "EAI_CODE,APPLICATION_NAME\n1001,Buena\n,Sin codigo\n"));

        Assert.True(outcome.Accepted);

        var snapshot = await harness.Provider.GetAllAsync();
        var master = snapshot.Sources.Single(s => s.SourceName == "EAI Master");

        Assert.Equal(1, master.RecordCount);
        Assert.Equal(1, master.RejectedCount);
        Assert.Single(master.Rejections);
    }

    [Fact]
    public async Task AnEmptyDatabaseReportsThatThereIsNoData()
    {
        using var harness = new DatabaseHarness(seed: false);

        Assert.Empty(await harness.Provider.GetVersionsAsync());
        await Assert.ThrowsAsync<NoSnapshotAvailableException>(() => harness.Provider.GetAllAsync());
    }

    [Fact]
    public async Task AnUploadWithoutTheMasterIsRejectedAndChangesNothing()
    {
        using var harness = new DatabaseHarness();

        var outcome = harness.PublishRaw(
            "Sin maestro",
            ("product_strategy.csv", "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,1\n"));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Errors, e => e.Code == NoticeCodes.UploadMissingMaster);

        // El portafolio vigente sigue siendo el de antes: la transacción no ocurrió.
        var versions = await harness.Provider.GetVersionsAsync();
        Assert.Single(versions);
    }

    [Fact]
    public async Task TheSchemaIsUpToDateAfterMigrating()
    {
        using var harness = new DatabaseHarness(seed: false);

        await using var db = await harness.Contexts.CreateDbContextAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
