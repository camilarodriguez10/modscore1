using System.Net;
using System.Text.Json;
using Modernization.Domain;
using Modernization.Insights;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// Las seis preguntas del portafolio atravesando las dos capas.
/// </summary>
/// <remarks>
/// La pantalla 09 no calcula nada: pregunta una vez y pinta. Estas pruebas cuidan que
/// lo que llega alcance para responder —salud, prioridades, complejidad, riesgo,
/// obsolescencia y resiliencia—, que respete el filtro y el alcance del usuario, y que
/// los umbrales que llegan por la URL no puedan pedir una respuesta desmedida.
/// </remarks>
public sealed class InsightsEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] Groupings = ["lob", "country", "cio", "capability"];

    private async Task<InsightsResponse> GetAsync(string url = "/api/insights")
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();
        var view = await client.GetFromJsonAsync<InsightsResponse>(new Uri(url, UriKind.Relative), Json);

        Assert.NotNull(view);
        return view;
    }

    /// <summary>Una sola llamada trae las seis respuestas, con las reglas y la carga que las produjeron.</summary>
    [Fact]
    public async Task OneRequestAnswersTheSixQuestions()
    {
        var view = await GetAsync();

        Assert.NotEmpty(view.RulesetVersion);
        Assert.NotEqual(default, view.CapturedAt);

        Assert.True(view.Health.Applications > 0);
        Assert.NotEmpty(view.Priorities);
        Assert.NotEmpty(view.Complexity);
        Assert.NotEmpty(view.Risk);
        Assert.NotEmpty(view.Obsolescence);
        Assert.NotEmpty(view.Resilience.Applications);
    }

    /// <summary>¿Que tan sano esta? Los tramos reparten el portafolio completo, sin sobrar ni faltar.</summary>
    [Fact]
    public async Task HealthDistributesEveryApplicationAcrossTheBands()
    {
        var view = await GetAsync();
        var health = view.Health;

        Assert.Equal(health.Applications, health.Bands.Sum(b => b.Applications));
        Assert.InRange(health.Bands.Sum(b => b.Share), 99m, 101m);
        Assert.InRange(health.AverageScore, 20m, 100m);
        Assert.InRange(health.MedianScore, 20m, 100m);
        Assert.InRange(health.DataCoverage, 0m, 100m);
        Assert.InRange(health.CriticalBelowHealthy, 0, health.Applications);

        // Los nombres de tramo llegan del catalogo de reglas: el color nunca va solo.
        Assert.All(health.Bands, band =>
        {
            Assert.NotEmpty(band.Code);
            Assert.NotEmpty(band.Name);
        });
    }

    /// <summary>
    /// ¿Que modernizar primero? La lista se ordena por valor por semana, y cada fila
    /// muestra de que esta hecha: el uplift sale del roadmap y el peso de negocio de
    /// criticidad, usuarios y capacidades.
    /// </summary>
    [Fact]
    public async Task PrioritiesRankCandidatesByValuePerWeekAndShowTheirEvidence()
    {
        var view = await GetAsync();

        var ordered = view.Priorities.Select(p => p.ValuePerWeek).ToList();
        Assert.Equal(ordered.OrderByDescending(v => v), ordered);

        Assert.All(view.Priorities, candidate =>
        {
            Assert.NotEmpty(candidate.EaiCode);
            Assert.True(candidate.Uplift > 0m);
            Assert.True(candidate.Weeks > 0);
            Assert.True(candidate.BusinessWeight >= 1m);
            // Redondeos de dos decimales: se compara con tolerancia, no por igualdad exacta.
            Assert.InRange(Math.Abs(candidate.ProjectedScore!.Value - candidate.CurrentScore - candidate.Uplift), 0m, 0.02m);
            Assert.InRange(Math.Abs((candidate.Impact / candidate.Weeks) - candidate.ValuePerWeek), 0m, 0.02m);

            // El roadmap viaja como texto, no como numero: un enum serializado como
            // entero obliga a la pantalla a conocer el orden de la enumeracion. Va dos
            // veces a proposito: el nombre del enum sirve de filtro y el de MAPS se lee.
            Assert.True(candidate.Roadmap is null || candidate.Roadmap.Length > 1);
            Assert.Equal(candidate.Roadmap is null, candidate.RoadmapName is null);

            var evidence = candidate.Evidence.Select(e => e.Code).ToList();
            Assert.Contains(NoticeCodes.InsightsUpliftFromRoadmap, evidence);
            Assert.All(candidate.Evidence, notice => Assert.StartsWith("insights.", notice.Code, StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// ¿Donde reducir complejidad? Cada concentracion dice que clase es, a quienes
    /// agrupa y con que evidencia; nadie tiene que deducirlo del titulo.
    /// </summary>
    [Fact]
    public async Task ComplexityNamesTheKindOfConcentrationAndItsMembers()
    {
        var view = await GetAsync();
        var kinds = Enum.GetNames<ComplexityKind>();

        Assert.All(view.Complexity, cluster =>
        {
            Assert.Contains(cluster.Kind, kinds);
            Assert.NotEmpty(cluster.Dimension);
            Assert.True(cluster.Applications >= 2);
            Assert.NotEmpty(cluster.EaiCodes);
            Assert.StartsWith("insights.", cluster.Evidence.Code, StringComparison.Ordinal);
        });

        // El caso del ejemplo: varias aplicaciones con la misma capacidad en un pais.
        Assert.Contains(view.Complexity, c => c.Kind == nameof(ComplexityKind.DuplicateCapabilityInCountry));
    }

    /// <summary>
    /// ¿Donde se concentra el riesgo? Se agrupa por LOB, pais, CIO y capacidad, y el
    /// indice ordena; las señales que lo componen quedan a la vista para discutirlo.
    /// </summary>
    [Fact]
    public async Task RiskIsGroupedByFourDimensionsAndOrderedByItsIndex()
    {
        var view = await GetAsync("/api/insights?top=50");

        var indexes = view.Risk.Select(r => r.RiskIndex).ToList();
        Assert.Equal(indexes.OrderByDescending(i => i), indexes);

        Assert.All(view.Risk, risk =>
        {
            Assert.Contains(risk.Grouping, Groupings);
            Assert.NotEmpty(risk.Dimension);
            Assert.True(risk.RiskIndex > 0m);
            Assert.True(risk.Applications > 0);
        });

        Assert.Contains(view.Risk, r => r.Grouping == "lob");
        Assert.Contains(view.Risk, r => r.CriticalFlaws > 0 || r.HighFlaws > 0 || r.ObsoleteComponents > 0);
    }

    /// <summary>
    /// ¿Que causa la obsolescencia? Los puntos perdidos se reparten por metrica y suman
    /// el total; la falta de dato va aparte, porque no es lo mismo estar mal que no saber.
    /// </summary>
    [Fact]
    public async Task ObsolescenceRanksTheMetricsThatCostTheMostPoints()
    {
        var view = await GetAsync("/api/insights?top=50");

        var lost = view.Obsolescence.Select(o => o.LostPoints).ToList();
        Assert.Equal(lost.OrderByDescending(l => l), lost);

        Assert.All(view.Obsolescence, driver =>
        {
            Assert.NotEmpty(driver.MetricKey);
            Assert.NotEmpty(driver.Name);
            Assert.NotEmpty(driver.PillarKey);
            Assert.True(driver.LostPoints > 0m || driver.WeightWithoutData > 0m);
            Assert.InRange(driver.Share, 0m, 100m);
            Assert.InRange(driver.ApplicationsAffected, 0, view.Health.Applications);
        });

        Assert.InRange(view.Obsolescence.Sum(o => o.Share), 99m, 101m);
    }

    /// <summary>
    /// ¿Donde faltan defensas? Los contadores de criticas no pueden superar a las
    /// aplicaciones listadas, y cada fila trae el hueco concreto que tiene.
    /// </summary>
    [Fact]
    public async Task ResilienceCountsGapsAndListsTheApplicationsThatHaveThem()
    {
        var view = await GetAsync("/api/insights?top=50");
        var gaps = view.Resilience;

        Assert.All(gaps.Applications, application =>
        {
            Assert.NotEmpty(application.EaiCode);
            Assert.NotEmpty(application.Gaps);
            Assert.All(application.Gaps, notice => Assert.StartsWith("insights.", notice.Code, StringComparison.Ordinal));
        });

        // Las criticas van primero: es donde un hueco duele.
        var criticalFirst = gaps.Applications.Select(a => a.Critical).ToList();
        Assert.Equal(criticalFirst.OrderByDescending(c => c), criticalFirst);

        Assert.True(gaps.CriticalWithoutDisasterRecovery >= 0);
        Assert.True(gaps.CriticalWithoutMonitoring >= 0);
        Assert.True(gaps.WithLowOperationalScore >= 0);
        Assert.True(gaps.WithoutRecoveryObjectives >= 0);
    }

    /// <summary>El analisis habla del recorte que la persona tiene puesto, no del portafolio entero.</summary>
    [Fact]
    public async Task TheAnalysisFollowsTheFilterOnScreen()
    {
        var everything = await GetAsync();
        var filtered = await GetAsync("/api/insights?criticalOnly=true&top=50");

        Assert.True(filtered.Health.Applications < everything.Health.Applications);
        Assert.All(filtered.Resilience.Applications, application => Assert.True(application.Critical));

        var byCountry = await GetAsync("/api/insights?country=Chile&top=50");
        Assert.True(byCountry.Health.Applications < everything.Health.Applications);
        Assert.All(byCountry.Priorities, candidate => Assert.Equal("Chile", candidate.Country));
        Assert.All(byCountry.Resilience.Applications, a => Assert.Equal("Chile", a.Country));

        // Un solo pais no puede sostener una capacidad repartida entre paises.
        Assert.DoesNotContain(
            byCountry.Complexity,
            c => c.Kind == nameof(ComplexityKind.CapabilityAcrossCountries));
    }

    /// <summary>
    /// Los umbrales llegan por la URL y la capa System los acota: nadie pide una
    /// respuesta de diez mil filas ni un duplicado "a partir de una".
    /// </summary>
    [Fact]
    public async Task ThresholdsFromTheUrlAreClampedByTheServer()
    {
        var view = await GetAsync("/api/insights?top=9999&duplicates=1");

        Assert.Equal(InsightsOptions.MaxTopCount, view.Options.TopCount);
        Assert.Equal(2, view.Options.DuplicateThreshold);
        Assert.True(view.Priorities.Count <= InsightsOptions.MaxTopCount);
        Assert.True(view.Complexity.Count <= InsightsOptions.MaxTopCount);
        Assert.True(view.Risk.Count <= InsightsOptions.MaxTopCount);
        Assert.True(view.Resilience.Applications.Count <= InsightsOptions.MaxTopCount);

        // Los umbrales de "sano" y "base de usuarios" salen del catalogo del roadmap.
        Assert.True(view.Options.HealthyScore > 0m);
        Assert.True(view.Options.SignificantUserBase > 0);
    }

    /// <summary>
    /// Un tramo que no existe se rechaza antes de calcular nada: devolver cero
    /// resultados haria pensar que el portafolio esta vacio.
    /// </summary>
    [Fact]
    public async Task AnUnknownBandIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/insights?band=Excelente", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Un filtro que no deja nada dentro responde vacio, no roto: la pantalla tiene que
    /// poder decir "no hay nada aqui" en vez de mostrar un error.
    /// </summary>
    [Theory]
    [InlineData("/api/insights?disposition=Eliminate&country=Narnia")]
    [InlineData("/api/insights?lob=Ninguna")]
    public async Task AFilterThatMatchesNothingAnswersAnEmptyAnalysis(string url)
    {
        var view = await GetAsync(url);

        Assert.Equal(0, view.Health.Applications);
        Assert.Empty(view.Health.Bands);
        Assert.Empty(view.Priorities);
        Assert.Empty(view.Complexity);
        Assert.Empty(view.Risk);
        Assert.Empty(view.Obsolescence);
        Assert.Empty(view.Resilience.Applications);
        Assert.NotEmpty(view.RulesetVersion);
    }

    /// <summary>Sin sesion no hay analisis: es el portafolio completo en una sola respuesta.</summary>
    [Fact]
    public async Task WithoutASessionThereIsNoAnalysis()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/insights", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// El analisis se calcula sobre los datos que el alcance deja ver, no sobre el
/// portafolio completo filtrado despues: lo que un usuario no puede ver no entra en
/// ningun promedio.
/// </summary>
public sealed class ScopedInsightsTests(LobScopedFixture fixture) : IClassFixture<LobScopedFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TheAnalysisOnlyCoversTheScopedLob()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<InsightsResponse>(
            new Uri("/api/insights?top=50", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.Health.Applications > 0);

        Assert.All(view.Priorities, candidate => Assert.Equal(LobScopedFixture.Lob, candidate.Lob));
        Assert.All(view.Resilience.Applications, a => Assert.Equal(LobScopedFixture.Lob, a.Lob));

        var lobs = view.Risk.Where(r => r.Grouping == "lob").Select(r => r.Dimension).Distinct().ToList();
        Assert.All(lobs, lob => Assert.Equal(LobScopedFixture.Lob, lob));
    }
}
