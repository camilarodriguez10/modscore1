using Microsoft.EntityFrameworkCore;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.Database;

/// <summary>
/// Adaptador de base de datos. Lee las filas de una carga y las normaliza con el
/// mismo ensamblador que usan Dummy y File.
/// </summary>
/// <remarks>
/// Es la tercera implementacion del mismo puerto y la prueba de que el diseno de la
/// Fase 02 se sostuvo: el motor, el what-if, el roadmap y las ocho pantallas no
/// cambian una linea porque los datos ahora vengan de una base.
/// </remarks>
public sealed class DatabaseSnapshotProvider(IDbContextFactory<ModernizationDbContext> contexts)
    : IApplicationSnapshotProvider
{
    public const string ProviderName = "Database";

    public string Provider => ProviderName;

    public async Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await ReadVersionsAsync(db, cancellationToken).ConfigureAwait(false);
    }

    public Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
        => GetAllAsync(null, cancellationToken);

    public async Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var versions = await ReadVersionsAsync(db, cancellationToken).ConfigureAwait(false);

        if (versions.Count == 0)
        {
            // El estado inicial legitimo del adaptador, igual que un directorio de
            // historia vacio: la pantalla de cargas tiene que seguir funcionando.
            throw new NoSnapshotAvailableException();
        }

        var version = versionId is { Length: > 0 }
            ? versions.SingleOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal))
                ?? throw new SourceSchemaException($"The upload '{versionId}' does not exist.")
            : versions[0];

        var rejections = await db.Rejections
            .AsNoTracking()
            .Where(r => r.VersionId == version.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bySource = rejections
            .GroupBy(r => r.Source, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SourceRejection>)[.. g.Select(r => new SourceRejection(r.RowNumber, NoticeStorage.Deserialize(r.Reason), r.Key))],
                StringComparer.Ordinal);

        return SnapshotAssembler.Assemble(new SnapshotAssembler.Input
        {
            Provider = ProviderName,
            CapturedAt = version.CapturedAt,
            Version = version,
            Eai = await ReadAsync<EaiRecord>(db, version, SourceNames.EaiMaster, "eai_master", bySource, cancellationToken).ConfigureAwait(false),
            ArchitecturalValue = await ReadAsync<ArchitecturalValueRecord>(db, version, SourceNames.ArchitecturalValue, "architectural_value", bySource, cancellationToken).ConfigureAwait(false),
            ProductStrategy = await ReadAsync<ProductStrategyRecord>(db, version, SourceNames.ProductStrategy, "product_strategy", bySource, cancellationToken).ConfigureAwait(false),
            TechDebt = await ReadAsync<TechDebtRecord>(db, version, SourceNames.TechDebt, "tech_debt", bySource, cancellationToken).ConfigureAwait(false),
            ScaFlaws = await ReadAsync<ScaFlawRecord>(db, version, SourceNames.ScaFlaws, "sca_flaw", bySource, cancellationToken).ConfigureAwait(false),
            OssScore = await ReadAsync<OssScoreRecord>(db, version, SourceNames.OssScore, "oss_score", bySource, cancellationToken).ConfigureAwait(false),
            Monitoring = await ReadAsync<MonitoringRecord>(db, version, SourceNames.Monitoring, "monitoring", bySource, cancellationToken).ConfigureAwait(false),
            ApimOnboarding = await ReadAsync<ApimOnboardingRecord>(db, version, SourceNames.ApimOnboarding, "apim_onboarding", bySource, cancellationToken).ConfigureAwait(false),
        });
    }

    // -----------------------------------------------------------------------

    private static async Task<IReadOnlyList<SnapshotVersion>> ReadVersionsAsync(
        ModernizationDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.Versions
            .AsNoTracking()
            .OrderByDescending(v => v.CapturedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Mas reciente primero; la primera es la vigente, igual que en archivos.
        return [.. rows.Select((row, index) => new SnapshotVersion
        {
            Id = row.Id,
            CapturedAt = new DateTimeOffset(row.CapturedAtUtc, TimeSpan.Zero),
            Label = row.Label,
            Provider = ProviderName,
            ApplicationCount = row.ApplicationCount,
            IsCurrent = index == 0,
        })];
    }

    private static async Task<SourceReadResult<TRecord>> ReadAsync<TRecord>(
        ModernizationDbContext db,
        SnapshotVersion version,
        string sourceName,
        string table,
        Dictionary<string, IReadOnlyList<SourceRejection>> rejections,
        CancellationToken cancellationToken)
        where TRecord : class
    {
        var records = await db.Set<TRecord>()
            .AsNoTracking()
            .Where(r => EF.Property<string>(r, ModernizationDbContext.VersionId) == version.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SourceReadResult<TRecord>
        {
            SourceName = sourceName,
            Provider = ProviderName,
            Origin = $"{table}#{version.Id}",
            Records = records,
            Rejections = rejections.TryGetValue(sourceName, out var rows) ? rows : [],
            ExtractedAt = version.CapturedAt,
        };
    }
}
