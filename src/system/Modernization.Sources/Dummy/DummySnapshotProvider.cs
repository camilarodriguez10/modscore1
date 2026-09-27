using System.Globalization;
using Modernization.Domain.Sources;

namespace Modernization.Sources.Dummy;

/// <summary>
/// Reader en memoria. Sirve registros ya generados, sin rechazos: los datos
/// sinteticos son validos por construccion. Los adaptadores de archivo y de base
/// de datos implementaran esta misma interfaz y si podran rechazar filas.
/// </summary>
internal sealed class DummySourceReader<TRecord>(
    string sourceName,
    string origin,
    IReadOnlyList<TRecord> records,
    DateTimeOffset extractedAt) : ISourceReader<TRecord>
{
    public const string ProviderName = "Dummy";

    public string SourceName => sourceName;

    public string Provider => ProviderName;

    public Task<SourceReadResult<TRecord>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new SourceReadResult<TRecord>
        {
            SourceName = sourceName,
            Provider = ProviderName,
            Origin = origin,
            Records = records,
            Rejections = [],
            ExtractedAt = extractedAt,
        });
    }
}

/// <summary>
/// Adaptador Dummy: genera el portafolio sintetico y lo normaliza con el mismo
/// ensamblador que usaran File y Database.
/// </summary>
/// <remarks>
/// Registrarlo o no es una decision de configuracion. El resto del sistema solo
/// conoce <see cref="IApplicationSnapshotProvider"/>, asi que las fases 07 y 09
/// no cambian nada aguas arriba.
/// </remarks>
public sealed class DummySnapshotProvider : IApplicationSnapshotProvider
{
    /// <summary>
    /// Cuantas cargas historicas sinteticas se generan hacia atras, una por
    /// trimestre. Sin historia, la pantalla de evolucion no tendria que mostrar.
    /// </summary>
    public const int HistoricalVersions = 4;

    private readonly DummyPortfolio _portfolio;
    private readonly int _seed;
    private readonly int _applicationCount;

    public DummySnapshotProvider(DummyPortfolio portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        _portfolio = portfolio;
        _seed = portfolio.Seed;
        _applicationCount = portfolio.Eai.Count;
    }

    public DummySnapshotProvider(
        int seed = DummyPortfolio.DefaultSeed,
        int applicationCount = DummyPortfolio.DefaultApplicationCount)
        : this(DummyPortfolio.Generate(seed, applicationCount))
    {
    }

    public string Provider => DummySourceReader<object>.ProviderName;

    /// <summary>Portafolio crudo, para exportarlo a CSV o inspeccionarlo.</summary>
    public DummyPortfolio Portfolio => _portfolio;

    public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<SnapshotVersion>>(BuildVersions());
    }

    public Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
        => GetAllAsync(null, cancellationToken);

    public async Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
    {
        var versions = BuildVersions();

        SnapshotVersion version = versionId is { Length: > 0 }
            ? versions.SingleOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal))
                ?? throw new ArgumentOutOfRangeException(
                    nameof(versionId), versionId, "The dummy portfolio has no such upload.")
            : versions[0];

        // Cada version historica es el mismo portafolio unos trimestres antes: las
        // mismas aplicaciones con los valores que tenian entonces, para que la
        // evolucion de cada una tenga sentido (DummyTimeline).
        var portfolio = version.IsCurrent
            ? _portfolio
            : DummyTimeline.Rewind(_portfolio, VersionOffset(versions, version));

        return await AssembleAsync(portfolio, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cargas trimestrales hacia atras desde la captura del portafolio actual.
    /// </summary>
    private List<SnapshotVersion> BuildVersions()
    {
        var versions = new List<SnapshotVersion>(HistoricalVersions);

        for (var index = 0; index < HistoricalVersions; index++)
        {
            var capturedAt = _portfolio.CapturedAt.AddMonths(-3 * index);

            versions.Add(new SnapshotVersion
            {
                Id = capturedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
                CapturedAt = capturedAt,
                Label = FormattableString.Invariant($"Q{Quarter(capturedAt)} {capturedAt.Year}"),
                Provider = DummySourceReader<object>.ProviderName,
                ApplicationCount = _applicationCount,
                IsCurrent = index == 0,
            });
        }

        return versions;
    }

    private static int VersionOffset(List<SnapshotVersion> versions, SnapshotVersion version)
    {
        for (var i = 0; i < versions.Count; i++)
        {
            if (string.Equals(versions[i].Id, version.Id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private static int Quarter(DateTimeOffset moment) => ((moment.Month - 1) / 3) + 1;

    private async Task<SnapshotSet> AssembleAsync(
        DummyPortfolio portfolio,
        SnapshotVersion version,
        CancellationToken cancellationToken)
    {
        var origin = portfolio.Origin;
        var at = portfolio.CapturedAt;

        SourceReadResult<T> Read<T>(string name, IReadOnlyList<T> records)
            => new()
            {
                SourceName = name,
                Provider = Provider,
                Origin = origin,
                Records = records,
                Rejections = [],
                ExtractedAt = at,
            };

        // Se leen a traves del puerto para que el camino ejercitado sea el mismo
        // que usaran los otros adaptadores, no un atajo en memoria.
        var eai = await new DummySourceReader<EaiRecord>(
            SourceNames.EaiMaster, origin, portfolio.Eai, at).ReadAsync(cancellationToken).ConfigureAwait(false);

        return SnapshotAssembler.Assemble(new SnapshotAssembler.Input
        {
            Provider = Provider,
            CapturedAt = at,
            Version = version,
            Eai = eai,
            ArchitecturalValue = Read(SourceNames.ArchitecturalValue, portfolio.ArchitecturalValue),
            ProductStrategy = Read(SourceNames.ProductStrategy, portfolio.ProductStrategy),
            TechDebt = Read(SourceNames.TechDebt, portfolio.TechDebt),
            ScaFlaws = Read(SourceNames.ScaFlaws, portfolio.ScaFlaws),
            OssScore = Read(SourceNames.OssScore, portfolio.OssScore),
            Monitoring = Read(SourceNames.Monitoring, portfolio.Monitoring),
            ApimOnboarding = Read(SourceNames.ApimOnboarding, portfolio.ApimOnboarding),
        });
    }
}
