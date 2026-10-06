using System.Net;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// El dashboard ejecutivo atravesando las dos capas, con los filtros nuevos.
/// </summary>
/// <remarks>
/// La pantalla hace una sola llamada y pinta. Estas pruebas cuidan que esa llamada
/// alcance para los siete espacios de indicador y los seis paneles, que respete el
/// filtro, el eje elegido y el alcance del usuario, y que el detalle de cualquier panel
/// sean las mismas filas.
/// </remarks>
public sealed class DashboardEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<DashboardResponse> GetAsync(string url = "/api/dashboard")
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();
        var view = await client.GetFromJsonAsync<DashboardResponse>(new Uri(url, UriKind.Relative), Json);

        Assert.NotNull(view);
        return view;
    }

    [Fact]
    public async Task OneRequestCarriesTheIndicatorsThePanelsAndTheDetail()
    {
        var view = await GetAsync();
        var dashboard = view.Dashboard;

        Assert.Equal(["health", "atRisk", "candidates", "currency", "resilience"], dashboard.Kpis.Select(k => k.Key));
        Assert.True(dashboard.Health.Applications > 0);
        Assert.Equal(dashboard.Health.Applications, dashboard.Applications.Count);
        Assert.Equal(dashboard.Applications.Count, dashboard.Matrix.Cells.Sum(c => c.Applications));
        Assert.NotEmpty(dashboard.Prioritization.Points);
        Assert.Equal(6, dashboard.Complexity.Dimensions.Count);
        Assert.Equal(5, dashboard.Risk.Hotspots.Count);
        Assert.Contains(dashboard.Obsolescence.Factors, f => f.Key == "eosExposure" && f.Status == "needs-data");
        Assert.Equal(5, dashboard.Resilience.Dimensions.Count);
    }

    /// <summary>
    /// Quien lee el dashboard tiene que saber que las definiciones son provisionales, y con
    /// que valores se calculo: las definiciones viajan con las reglas (ADR 0025).
    /// </summary>
    [Fact]
    public async Task TheResponseSaysWhichDefinitionsItUsesAndWhichSourcesAreMissing()
    {
        var view = await GetAsync();

        Assert.Equal(view.RulesetVersion, view.DefinitionsVersion);
        Assert.Equal("provisional", view.DefinitionsStatus);
        Assert.NotEmpty(view.RulesetVersion);
        Assert.Equal(Insights.AnalyticsCatalog.Parameters.Count, view.Definitions.Parameters.Count);
        Assert.Equal(60m, view.Definitions.Parameters.Single(p => p.Key == "currency.legacyBelow").Value);
        Assert.True(view.Definitions.SignificantUserBase > 0);

        Assert.Contains(view.Sources, s => s.Key == "eai" && s.Connected);
        Assert.Contains(view.Sources, s => s.Key == "tlm" && !s.Connected);
        Assert.Contains(view.Sources, s => s.Key == "resiliency" && !s.Connected);
    }

    [Fact]
    public async Task TheTrendEndsWithTheSamePortfolioTheDashboardShows()
    {
        var view = await GetAsync("/api/dashboard?country=Chile");

        Assert.NotEmpty(view.Trend);

        var last = view.Trend[^1];
        Assert.Equal(view.Dashboard.Health.Applications, last.Applications);
        Assert.Equal(view.Dashboard.Health.AverageScore, last.HealthScore);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("region")]
    [InlineData("disposition")]
    [InlineData("country")]
    public async Task TheHeatmapAxisIsChosenInTheUrl(string axis)
    {
        var view = await GetAsync($"/api/dashboard?axis={axis}");

        Assert.Equal(axis, view.Dashboard.Matrix.Axis);
        Assert.Equal(view.Dashboard.Applications.Count, view.Dashboard.Matrix.Cells.Sum(c => c.Applications));
    }

    [Fact]
    public async Task AnUnknownAxisIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/dashboard?axis=planet", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------- filtros nuevos

    /// <summary>Los cinco filtros del dashboard del Modernization Score acotan de verdad.</summary>
    [Fact]
    public async Task TheNewFiltersNarrowTheDashboard()
    {
        var everything = await GetAsync();
        var sample = everything.Dashboard.Applications[0];

        var byCapability = await GetAsync($"/api/dashboard?capability={Uri.EscapeDataString(sample.Capability!)}");
        Assert.True(byCapability.Dashboard.Applications.Count < everything.Dashboard.Applications.Count);
        Assert.All(byCapability.Dashboard.Applications, a => Assert.Equal(sample.Capability, a.Capability));

        var byCre = await GetAsync($"/api/dashboard?cre={Uri.EscapeDataString(sample.Cre!)}");
        Assert.All(byCre.Dashboard.Applications, a => Assert.Equal(sample.Cre, a.Cre));

        var byStatus = await GetAsync($"/api/dashboard?status={Uri.EscapeDataString(sample.Status!)}");
        Assert.All(byStatus.Dashboard.Applications, a => Assert.Equal(sample.Status, a.Status));

        var byRegion = await GetAsync($"/api/dashboard?region={Uri.EscapeDataString(sample.Region!)}");
        Assert.All(byRegion.Dashboard.Applications, a => Assert.Equal(sample.Region, a.Region));

        var byEai = await GetAsync($"/api/dashboard?eai={sample.EaiCode}");
        Assert.Equal(sample.EaiCode, Assert.Single(byEai.Dashboard.Applications).EaiCode);
    }

    /// <summary>Los filtros nuevos son de todo el sitio, no solo del dashboard.</summary>
    [Fact]
    public async Task TheNewFiltersAlsoNarrowThePortfolioAndOfferTheirOptions()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();
        var portfolio = await client.GetFromJsonAsync<PortfolioView>(new Uri("/api/portfolio?pageSize=1", UriKind.Relative), Json);

        Assert.NotNull(portfolio);
        Assert.NotEmpty(portfolio.ByCapability);
        Assert.NotEmpty(portfolio.ByCre);
        Assert.NotEmpty(portfolio.ByRegion);
        Assert.NotEmpty(portfolio.ByStatus);

        var capability = portfolio.ByCapability[0];
        var filtered = await client.GetFromJsonAsync<PortfolioView>(
            new Uri($"/api/portfolio?pageSize=1&capability={Uri.EscapeDataString(capability.Key)}", UriKind.Relative), Json);

        Assert.Equal(capability.Count, filtered!.TotalApplications);
    }

    [Fact]
    public async Task AFilterLongerThanTheLimitIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri("/api/dashboard?capability=" + new string('x', 101), UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WithoutASessionThereIsNoDashboard()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/dashboard", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>El dashboard de un usuario limitado a un LOB solo habla de ese LOB, en todos los números.</summary>
public sealed class ScopedDashboardTests(LobScopedFixture fixture) : IClassFixture<LobScopedFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EveryNumberComesFromTheScopedLobOnly()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var everythingVisible = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=1", UriKind.Relative), Json);
        var view = await client.GetFromJsonAsync<DashboardResponse>(new Uri("/api/dashboard", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(everythingVisible!.TotalApplications, view.Dashboard.Applications.Count);
        Assert.Equal(view.Dashboard.Applications.Count, view.Dashboard.Health.Applications);
        Assert.Equal(view.Dashboard.Applications.Count, view.Trend[^1].Applications);
    }
}
