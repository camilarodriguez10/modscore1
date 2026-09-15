import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- El ensamblador ahora recibe la version
edit(os.path.join(root, r"src\system\Modernization.Sources\SnapshotAssembler.cs"), [
    ("""        public required string Provider { get; init; }
        public required DateTimeOffset CapturedAt { get; init; }""",
     """        public required string Provider { get; init; }
        public required DateTimeOffset CapturedAt { get; init; }
        public required SnapshotVersion Version { get; init; }"""),

    ("""            Provider = input.Provider,
            Applications = applications,
            CapturedAt = input.CapturedAt,""",
     """            Provider = input.Provider,
            Applications = applications,
            CapturedAt = input.CapturedAt,
            Version = input.Version,"""),
])

# --- El proveedor Dummy genera varias versiones con deriva, para que la
#     evolucion sea demostrable sin archivos reales.
edit(os.path.join(root, r"src\system\Modernization.Sources\Dummy\DummySnapshotProvider.cs"), [
    ("""public sealed class DummySnapshotProvider : IApplicationSnapshotProvider
{
    private readonly DummyPortfolio _portfolio;

    public DummySnapshotProvider(DummyPortfolio portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        _portfolio = portfolio;
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

    public async Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var origin = _portfolio.Origin;
        var at = _portfolio.CapturedAt;""",

     """public sealed class DummySnapshotProvider : IApplicationSnapshotProvider
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

        return Task.FromResult(BuildVersions());
    }

    public Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
        => GetAllAsync(null, cancellationToken);

    public async Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
    {
        var versions = BuildVersions();

        var version = versionId is { Length: > 0 }
            ? versions.SingleOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal))
                ?? throw new ArgumentOutOfRangeException(
                    nameof(versionId), versionId, "No existe esa carga en el portafolio dummy.")
            : versions[0];

        // Cada version historica se genera con su propia semilla derivada: los
        // datos derivan de forma determinista, como lo harian entre trimestres.
        var portfolio = version.IsCurrent
            ? _portfolio
            : DummyPortfolio.Generate(
                _seed + VersionOffset(versions, version),
                _applicationCount,
                version.CapturedAt);

        return await AssembleAsync(portfolio, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cargas trimestrales hacia atras desde la captura del portafolio actual.
    /// </summary>
    private IReadOnlyList<SnapshotVersion> BuildVersions()
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

    private static int VersionOffset(IReadOnlyList<SnapshotVersion> versions, SnapshotVersion version)
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
        var at = portfolio.CapturedAt;"""),

    ("""        var eai = await new DummySourceReader<EaiRecord>(
            SourceNames.EaiMaster, origin, _portfolio.Eai, at).ReadAsync(cancellationToken).ConfigureAwait(false);

        return SnapshotAssembler.Assemble(new SnapshotAssembler.Input
        {
            Provider = Provider,
            CapturedAt = at,
            Eai = eai,
            ArchitecturalValue = Read(SourceNames.ArchitecturalValue, _portfolio.ArchitecturalValue),
            ProductStrategy = Read(SourceNames.ProductStrategy, _portfolio.ProductStrategy),
            TechDebt = Read(SourceNames.TechDebt, _portfolio.TechDebt),
            ScaFlaws = Read(SourceNames.ScaFlaws, _portfolio.ScaFlaws),
            OssScore = Read(SourceNames.OssScore, _portfolio.OssScore),
            Monitoring = Read(SourceNames.Monitoring, _portfolio.Monitoring),
            ApimOnboarding = Read(SourceNames.ApimOnboarding, _portfolio.ApimOnboarding),
        });
    }""",

     """        var eai = await new DummySourceReader<EaiRecord>(
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
    }"""),

    ("using Modernization.Domain.Sources;",
     "using System.Globalization;\nusing Modernization.Domain.Sources;"),
])

# --- El generador acepta la marca de tiempo (ya la tenia) y expone la semilla
edit(os.path.join(root, r"src\system\Modernization.Sources\Modernization.Sources.csproj"), [
    ("""  <ItemGroup>
    <InternalsVisibleTo Include="Modernization.Sources.ContractTests" />
  </ItemGroup>""",
     """  <ItemGroup>
    <PackageReference Include="CsvHelper" />
  </ItemGroup>
  <ItemGroup>
    <!-- Los contratos de columna viajan con el ensamblado, igual que el ruleset. -->
    <EmbeddedResource Include="..\\..\\..\\contracts\\sources\\source-schemas.json" LogicalName="source-schemas.json" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Modernization.Sources.ContractTests" />
  </ItemGroup>"""),
])
