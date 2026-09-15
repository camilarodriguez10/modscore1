using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Contrato que todo adaptador de fuentes debe cumplir. Se hereda una vez por
/// adaptador: hoy Dummy, en la Fase 07 File y en la Fase 09 Database.
/// </summary>
/// <remarks>
/// Esta suite es el seguro de las dos migraciones previstas. Si al cambiar de
/// adaptador algo se desvia — un EAI duplicado, un score fuera de rango, una
/// fuente que deja de reportar su estado — falla aqui, no en produccion.
/// </remarks>
public abstract class SnapshotProviderContract
{
    private static readonly ScoringEngine Engine = new(RulesetCatalog.Default);

    protected abstract IApplicationSnapshotProvider CreateProvider();

    protected abstract string ExpectedProviderName { get; }

    /// <summary>Las ocho fuentes que el ensamblador espera.</summary>
    private static readonly string[] RequiredSources =
    [
        "EAI Master", "Architectural Value", "Product Strategy", "Tech Debt",
        "SCA Flaws", "OSS Score", "Monitoring", "APIM Onboarding",
    ];

    private async Task<SnapshotSet> LoadAsync() => await CreateProvider().GetAllAsync();

    // ---------------------------------------------------------------- carga

    [Fact]
    public async Task LoadsANonEmptyPortfolio()
    {
        var snapshot = await LoadAsync();

        Assert.NotEmpty(snapshot.Applications);
        Assert.Equal(ExpectedProviderName, snapshot.Provider);
    }

    [Fact]
    public async Task ProducesTheSameResultOnEveryLoad()
    {
        // Sin esto no hay diffs limpios ni tests reproducibles.
        var first = await LoadAsync();
        var second = await LoadAsync();

        Assert.Equal(first.Applications.Count, second.Applications.Count);
        Assert.Equal(
            first.Applications.Select(a => a.EaiCode),
            second.Applications.Select(a => a.EaiCode));
        Assert.Equal(
            first.Applications.Select(a => Engine.Calculate(a).Score),
            second.Applications.Select(a => Engine.Calculate(a).Score));
    }

    // ------------------------------------------------------------ integridad

    [Fact]
    public async Task EveryApplicationHasAnIdentity()
    {
        var snapshot = await LoadAsync();

        Assert.All(snapshot.Applications, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.EaiCode));
            Assert.False(string.IsNullOrWhiteSpace(app.ApplicationName));
        });
    }

    [Fact]
    public async Task EaiCodesAreUnique()
    {
        var snapshot = await LoadAsync();

        var duplicates = snapshot.Applications
            .GroupBy(a => a.EaiCode, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task StampsTheProviderOnEverySnapshot()
    {
        var snapshot = await LoadAsync();

        Assert.All(snapshot.Applications, app => Assert.Equal(ExpectedProviderName, app.Provider));
    }

    /// <summary>
    /// Todo adaptador declara al menos una version, y exactamente una es la
    /// actual. Sin esa garantia, la pantalla de evolucion no sabria de donde
    /// partir.
    /// </summary>
    [Fact]
    public async Task ReportsAtLeastOneVersionWithExactlyOneCurrent()
    {
        var versions = await CreateProvider().GetVersionsAsync();

        Assert.NotEmpty(versions);
        Assert.Single(versions, v => v.IsCurrent);
        Assert.All(versions, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Id));
            Assert.False(string.IsNullOrWhiteSpace(v.Label));
            Assert.Equal(ExpectedProviderName, v.Provider);
        });

        // Ordenadas de la mas reciente a la mas antigua.
        Assert.Equal(
            versions.Select(v => v.CapturedAt).OrderByDescending(d => d),
            versions.Select(v => v.CapturedAt));
    }

    /// <summary>El portafolio sabe a que version corresponde.</summary>
    [Fact]
    public async Task SnapshotCarriesItsVersion()
    {
        var snapshot = await LoadAsync();
        var versions = await CreateProvider().GetVersionsAsync();

        Assert.Equal(versions.Single(v => v.IsCurrent).Id, snapshot.Version.Id);
    }

    [Fact]
    public async Task ReportsStatusForEverySource()
    {
        var snapshot = await LoadAsync();

        foreach (var required in RequiredSources)
        {
            var status = snapshot.Sources.SingleOrDefault(s => s.SourceName == required);

            Assert.NotNull(status);
            Assert.Equal(ExpectedProviderName, status.Provider);
            Assert.False(string.IsNullOrWhiteSpace(status.Origin));
            Assert.InRange(status.MatchedApplications, 0, snapshot.Applications.Count);
        }
    }

    [Fact]
    public async Task ReportsRejectionsWithAReadableReason()
    {
        var snapshot = await LoadAsync();

        foreach (var source in snapshot.Sources)
        {
            Assert.Equal(source.RejectedCount, source.Rejections.Count);
            Assert.All(source.Rejections, rejection => Assert.False(string.IsNullOrWhiteSpace(rejection.Reason.Code)));
        }
    }

    // -------------------------------------------------------------- scoring

    [Fact]
    public async Task EveryApplicationScoresWithoutThrowing()
    {
        var snapshot = await LoadAsync();
        var failures = new List<string>();

        foreach (var app in snapshot.Applications)
        {
            try
            {
                Engine.Calculate(app);
            }
            catch (RulesetException ex)
            {
                failures.Add($"{app.EaiCode}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task EveryScoreStaysWithinTheNormalizedRange()
    {
        var snapshot = await LoadAsync();

        Assert.All(snapshot.Applications, app =>
        {
            var result = Engine.Calculate(app);
            Assert.InRange(result.Score, 20m, 100m);
        });
    }

    [Fact]
    public async Task DenominatorNeverExceedsTheTotalWeight()
    {
        var snapshot = await LoadAsync();

        Assert.All(snapshot.Applications, app =>
        {
            var result = Engine.Calculate(app);
            Assert.InRange(result.Denominator, 0m, 100m);
        });
    }

    /// <summary>
    /// Verificacion extremo a extremo: el EAI documentado en el BRD debe llegar a
    /// 79,67 pasando por lectura, ensamblado y scoring. Es la prueba de que
    /// cambiar de adaptador no cambia un score.
    /// </summary>
    [Fact]
    public async Task ReferenceApplicationReproducesTheBrdScore()
    {
        var snapshot = await LoadAsync();
        var reference = snapshot.Find("14381");

        Assert.NotNull(reference);

        var result = Engine.Calculate(reference);

        Assert.Equal(44.75m, result.Numerator);
        Assert.Equal(60m, result.Denominator);
        Assert.Equal(79.67m, result.Score);
    }

    // ---------------------------------------------------- casos borde cubiertos

    /// <summary>
    /// El portafolio debe contener los casos que rompen un motor de scoring, o los
    /// tests pasarian sin haber ejercitado nada interesante.
    /// </summary>
    [Theory]
    [InlineData("sin datos de SCA")]
    [InlineData("con SCA limpio")]
    [InlineData("sin tech debt")]
    [InlineData("sin monitoreo")]
    [InlineData("sin exposicion de API")]
    [InlineData("sin usuarios")]
    [InlineData("con conteos en cero")]
    public async Task PortfolioCoversTheEdgeCase(string edgeCase)
    {
        var apps = (await LoadAsync()).Applications;

        var matches = edgeCase switch
        {
            "sin datos de SCA" => apps.Count(a => a.ScaFlaws is null),
            "con SCA limpio" => apps.Count(a => a.ScaFlaws is { Critical: 0, High: 0 }),
            "sin tech debt" => apps.Count(a => a.TechDebtScore is null),
            "sin monitoreo" => apps.Count(a => a.Monitoring is null),
            "sin exposicion de API" => apps.Count(a => a.NativeApi is null),
            "sin usuarios" => apps.Count(a => a.UserCount == 0),
            "con conteos en cero" => apps.Count(a => a.ArchitecturalValue is { TotalCount: 0 }),
            _ => throw new ArgumentOutOfRangeException(nameof(edgeCase), edgeCase, "Caso borde no reconocido."),
        };

        Assert.True(matches > 0, $"El portafolio no contiene ninguna aplicacion {edgeCase}.");
    }

    /// <summary>
    /// Una aplicacion presente en el archivo SCA con cero flaws abiertos puntua 100.
    /// Una ausente queda sin dato. Confundirlas regalaria puntos a lo nunca escaneado.
    /// </summary>
    [Fact]
    public async Task DistinguishesCleanScansFromMissingScans()
    {
        var apps = (await LoadAsync()).Applications;

        var clean = apps.First(a => a.ScaFlaws is { Critical: 0 });
        var missing = apps.First(a => a.ScaFlaws is null);

        var cleanMetric = Engine.Calculate(clean).Metrics.Single(m => m.MetricKey == "sca.critical");
        var missingMetric = Engine.Calculate(missing).Metrics.Single(m => m.MetricKey == "sca.critical");

        Assert.True(cleanMetric.IsIncluded);
        Assert.Equal(100m, cleanMetric.MappedScore);

        Assert.False(missingMetric.IsIncluded);
        Assert.Null(missingMetric.MappedScore);
    }

    /// <summary>Los pares disposition/roadmap deben respetar el modelo MAPS.</summary>
    [Fact]
    public async Task RoadmapsBelongToTheirDisposition()
    {
        var apps = (await LoadAsync()).Applications;

        var invalid = apps
            .Where(a => a.Disposition is not null && a.Roadmap is not null)
            .Where(a => !IsValidMapsPair(a.Disposition!.Value, a.Roadmap!.Value))
            .Select(a => $"{a.EaiCode}: {a.Disposition} / {a.Roadmap}")
            .ToList();

        Assert.Empty(invalid);
    }

    private static bool IsValidMapsPair(ApplicationDisposition disposition, RoadmapType roadmap)
        => (disposition, roadmap) switch
        {
            (ApplicationDisposition.Tolerate, RoadmapType.MaintainPatch or RoadmapType.Rehost) => true,
            (ApplicationDisposition.Invest, RoadmapType.MaintainEvolve or RoadmapType.Refactor or RoadmapType.Reimagine) => true,
            (ApplicationDisposition.Eliminate, RoadmapType.Retire or RoadmapType.Consolidate) => true,
            _ => false,
        };
}
