using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

/// <summary>
/// Criterio de aceptacion de la Fase 3: una peticion autenticada al BFF devuelve
/// el desglose completo atravesando las dos capas.
/// </summary>
public sealed class LayeredEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>EAI de referencia del BRD, presente en el portafolio dummy.</summary>
    private const string ReferenceEai = "14381";

    [Fact]
    public async Task AuthenticatedRequestReturnsTheFullBreakdownThroughBothLayers()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<ExplainView>(
            new Uri($"/api/applications/{ReferenceEai}/explain", UriKind.Relative), Json);

        Assert.NotNull(view);

        // El caso de referencia servido por el BFF: 79,67 en el BRD con bandas, 80,70 con OSS
        // y Raw Tech en su valor bruto (ADR 0032).
        Assert.Equal(45.5285m, view.Formula.Numerator);
        Assert.Equal(60m, view.Formula.Denominator);
        Assert.Equal(80.70m, view.Formula.Score);
        Assert.Equal("v1.3", view.Formula.RulesetVersion);

        // La formula llega con los numeros reales ya sustituidos.
        Assert.Equal("20 + (45.529 / 60) x 80 = 80.70", view.Formula.Expression);
        Assert.Equal(5, view.Formula.Steps.Count);

        // Cada metrica incluida explica su regla y su procedencia.
        Assert.NotEmpty(view.Included);
        Assert.All(view.Included, m =>
        {
            Assert.NotEmpty(m.RuleApplied);
            Assert.NotEmpty(m.Provenance.System);
            Assert.NotEmpty(m.Provenance.Field);
            Assert.Equal("Dummy", m.Provenance.Provider);
        });

        // Y cada excluida, su motivo.
        Assert.NotEmpty(view.Excluded);
        Assert.All(view.Excluded, m => Assert.NotEmpty(m.Reason.Code));
    }

    [Fact]
    public async Task WaterfallAccumulatesToTheNumerator()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<ExplainView>(
            new Uri($"/api/applications/{ReferenceEai}/explain", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotEmpty(view.Waterfall);

        // El ultimo acumulado del waterfall es el numerador de la formula, redondeado a dos
        // decimales como todo acumulado: con OSS y Raw Tech en su valor bruto el numerador
        // puede tener mas (45,5285 en la 14381).
        Assert.Equal(Math.Round(view.Formula.Numerator, 2, MidpointRounding.AwayFromZero), view.Waterfall[^1].Cumulative);

        // Y viene ordenado de mayor a menor aporte.
        var weighted = view.Waterfall.Select(w => w.WeightedScore).ToList();
        Assert.Equal(weighted.OrderByDescending(w => w), weighted);
    }

    [Fact]
    public async Task OverviewReturnsThePillarCardsOfTheDashboard()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<ScoreOverviewView>(
            new Uri($"/api/applications/{ReferenceEai}/overview", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(80.70m, view.Score);
        Assert.Equal(3, view.Pillars.Count);

        var risk = view.Pillars.Single(p => p.Key == "risk");
        Assert.Equal(50m, risk.TotalWeight);
        Assert.Equal(25m, risk.AvailableWeight);
        Assert.Equal(77.26m, risk.Score);

        // El dashboard muestra peso nominal y disponible por separado.
        Assert.All(view.Pillars, p => Assert.True(p.AvailableWeight <= p.TotalWeight));
    }

    [Fact]
    public async Task PortfolioComposesAggregatesInASingleCall()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=10", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(10, view.Applications.Count);
        Assert.True(view.TotalApplications > 0);
        Assert.InRange(view.AverageScore, 20m, 100m);
        Assert.Equal(4, view.Distribution.Count);
        Assert.InRange(view.Distribution.Sum(d => d.Percent), 99m, 101m);
        Assert.NotEmpty(view.ByLob);
        Assert.NotEmpty(view.ByCountry);
    }

    [Fact]
    public async Task PortfolioFiltersReachTheSystemLayer()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?country=Chile&pageSize=50", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotEmpty(view.Applications);
        Assert.All(view.Applications, a => Assert.Equal("Chile", a.Country));
    }

    /// <summary>
    /// Los agregados cubren el conjunto filtrado completo, no la pagina visible.
    /// Calcularlos sobre la pagina daria un promedio distinto en cada scroll.
    /// </summary>
    [Fact]
    public async Task PortfolioAggregatesCoverTheWholeFilteredSetNotThePage()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var firstPage = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5", UriKind.Relative), Json);
        var secondPage = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5&page=2", UriKind.Relative), Json);

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);

        Assert.Equal(firstPage.AverageScore, secondPage.AverageScore);
        Assert.Equal(firstPage.TotalApplications, secondPage.TotalApplications);
        Assert.Equal(firstPage.ByCountry.Count, secondPage.ByCountry.Count);
        Assert.Equal(firstPage.Distribution.Sum(d => d.Count), firstPage.TotalApplications);
    }

    /// <summary>Y el filtro tambien alcanza a los agregados.</summary>
    [Fact]
    public async Task PortfolioAggregatesRespectTheActiveFilter()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var all = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5", UriKind.Relative), Json);
        var chileOnly = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?country=Chile&pageSize=5", UriKind.Relative), Json);

        Assert.NotNull(all);
        Assert.NotNull(chileOnly);

        Assert.True(chileOnly.TotalApplications < all.TotalApplications);
        Assert.Equal("Chile", Assert.Single(chileOnly.ByCountry).Key);
    }

    [Fact]
    public async Task LineageExposesCoverageAndRejectionsPerSource()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<LineageView>(
            new Uri("/api/lineage", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal("Dummy", view.Provider);
        Assert.Equal(8, view.Sources.Count);

        var master = view.Sources.Single(s => s.SourceName == "EAI Master");
        Assert.Equal(100m, master.CoveragePercent);

        // Las filas huerfanas del portafolio dummy deben verse aqui.
        Assert.Contains(view.Sources, s => s.RejectedCount > 0);
    }

    [Fact]
    public async Task SessionReportsTheAuthenticatedUser()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<SessionView>(
            new Uri("/api/session", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.IsAuthenticated);
        Assert.NotEmpty(view.Name);
    }

    [Fact]
    public async Task UnknownApplicationReturnsNotFound()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/applications/00000/explain", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
