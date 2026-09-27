using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// La pantalla Code atravesando las dos capas (ADR 0026): una llamada con el filtro de la
/// barra, el alcance del usuario y el origen de los datos dicho con todas sus letras.
/// </summary>
public sealed class CodeEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<CodeResponse> GetAsync(string url = "/api/code")
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();
        var view = await client.GetFromJsonAsync<CodeResponse>(new Uri(url, UriKind.Relative), Json);

        Assert.NotNull(view);
        return view;
    }

    [Fact]
    public async Task OneRequestCarriesTheIndicatorsTheLifecycleTheLibrariesAndTheApplications()
    {
        var view = await GetAsync();
        var analysis = view.Analysis;

        Assert.True(analysis.Kpis.Repositories > 0);
        Assert.Equal(analysis.Kpis.Applications, analysis.Applications.Count);
        Assert.Equal(analysis.Kpis.Components, analysis.Statuses.Sum(s => s.Components));
        Assert.NotEmpty(analysis.Lifecycle);
        Assert.Equal(["eol", "approaching", "security", "supported", "unknown"], analysis.Statuses.Select(s => s.Status));
        Assert.Equal(analysis.Kpis.Applications, analysis.RiskLevels.Sum(l => l.Applications));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), analysis.AsOf);
    }

    /// <summary>
    /// Sin integración, los repositorios son de demostración. La respuesta lo dice, y dice
    /// de dónde salen las fechas de fin de vida.
    /// </summary>
    [Fact]
    public async Task TheResponseSaysTheRepositoriesAreSampleDataAndNamesTheCatalog()
    {
        var view = await GetAsync();

        Assert.Equal("Sample", view.Source.Provider);
        Assert.False(view.Source.Connected);
        Assert.Equal("endoflife.date", view.Source.Catalog);
        Assert.Equal("https://endoflife.date", view.Source.CatalogUrl);
        Assert.True(view.Source.CatalogProducts > 10);
        Assert.Equal(Insights.AnalyticsCatalog.Parameters.Count, view.Definitions.Parameters.Count);
        Assert.Contains(view.Definitions.Areas, a => a.Key == "code");
    }

    [Fact]
    public async Task TheFiltersNarrowTheCodeScreen()
    {
        var everything = await GetAsync();
        var lob = everything.Analysis.Applications.First(a => a.Lob is not null).Lob!;

        var filtered = await GetAsync($"/api/code?lob={Uri.EscapeDataString(lob)}");

        Assert.True(filtered.Analysis.Kpis.Applications < everything.Analysis.Kpis.Applications);
        Assert.All(filtered.Analysis.Applications, a => Assert.Equal(lob, a.Lob));
        Assert.True(filtered.Analysis.Kpis.Repositories <= everything.Analysis.Kpis.Repositories);
    }

    [Fact]
    public async Task AnApplicationBringsItsRepositoriesAndHowItsIndexAddsUp()
    {
        var everything = await GetAsync();
        var riskiest = everything.Analysis.Applications[0];
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<CodeApplicationResponse>(
            new Uri($"/api/code/applications/{riskiest.EaiCode}", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(riskiest.EaiCode, view.Detail.Application.EaiCode);
        Assert.Equal(riskiest.Repositories, view.Detail.Repositories.Count);
        Assert.Equal(riskiest.RiskIndex, view.Detail.Factors.Sum(f => f.Total));
        Assert.Equal(7, view.Detail.Factors.Count);
        Assert.Equal(everything.Analysis.AsOf, view.AsOf);
    }

    [Theory]
    [InlineData("/api/code/applications/abc", HttpStatusCode.BadRequest)]
    [InlineData("/api/code/applications/99999999", HttpStatusCode.NotFound)]
    [InlineData("/api/code?lob=" + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", HttpStatusCode.BadRequest)]
    public async Task BadInputIsRejectedAtTheEdge(string url, HttpStatusCode expected)
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task WithoutASessionThereIsNoCodeScreen()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/code", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>La capa System no atiende a quien no trae el token de servicio.</summary>
    [Fact]
    public async Task TheSystemLayerRequiresTheServiceToken()
    {
        var client = fixture.CreateSystemClient(authenticated: false);

        using var response = await client.GetAsync(new Uri("/system/code", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>Un usuario limitado a un LOB solo ve los repositorios de ese LOB, en todos los números.</summary>
public sealed class ScopedCodeTests(LobScopedFixture fixture) : IClassFixture<LobScopedFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EveryRepositoryBelongsToTheScopedLob()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var portfolio = await client.GetFromJsonAsync<PortfolioView>(new Uri("/api/portfolio?pageSize=1", UriKind.Relative), Json);
        var view = await client.GetFromJsonAsync<CodeResponse>(new Uri("/api/code", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(portfolio!.TotalApplications, view.Analysis.Kpis.Applications);
        Assert.All(view.Analysis.Applications, a => Assert.Equal(LobScopedFixture.Lob, a.Lob));
    }

    /// <summary>Fuera del alcance es indistinguible de inexistente: 404, como el resto del API.</summary>
    [Fact]
    public async Task AnApplicationOutsideTheScopeIsNotFound()
    {
        using var everyone = new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "60",
        });

        var all = await (await everyone.CreateAuthenticatedChannelClientAsync())
            .GetFromJsonAsync<CodeResponse>(new Uri("/api/code", UriKind.Relative), Json);
        var outside = all!.Analysis.Applications.First(a => a.Lob != LobScopedFixture.Lob);

        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        using var response = await client.GetAsync(new Uri($"/api/code/applications/{outside.EaiCode}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>Sin integración ni datos de demostración, la pantalla dice que no hay inventario.</summary>
public sealed class NoCodeInventoryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WithoutAnInventoryTheApplicationsAreThereButNoRepository()
    {
        using var host = new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "20",
            ["CodeInventory:Provider"] = "None",
        });

        var client = await host.CreateAuthenticatedChannelClientAsync();
        var view = await client.GetFromJsonAsync<CodeResponse>(new Uri("/api/code", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal("None", view.Source.Provider);
        Assert.False(view.Source.Connected);
        Assert.Null(view.Source.ScannedAt);
        Assert.Equal(20, view.Analysis.Kpis.Applications);
        Assert.Equal(0, view.Analysis.Kpis.Repositories);
        Assert.All(view.Analysis.Applications, a => Assert.Null(a.RiskLevel));
    }

    /// <summary>
    /// Un repositorio de demostración leído como real sería un dato falso en una decisión:
    /// fuera de Development, el proceso no arranca con ese proveedor.
    /// </summary>
    [Fact]
    public void SampleRepositoriesCannotStartOutsideDevelopment()
    {
        using var factory = new WebApplicationFactory<Modernization.SystemApi.SystemApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("DataSource:Provider", "Dummy");
                builder.UseSetting("CodeInventory:Provider", "Sample");
                builder.UseSetting("AllowedHosts", "system-api.test");
                builder.UseSetting("ServiceToken:Token", LayeredApiFixture.ServiceToken);
            });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("CodeInventory:Provider", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownProviderIsRejected()
    {
        using var factory = new WebApplicationFactory<Modernization.SystemApi.SystemApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("DataSource:Provider", "Dummy");
                builder.UseSetting("CodeInventory:Provider", "GitHubCloud");
                builder.UseSetting("ServiceToken:Token", LayeredApiFixture.ServiceToken);
            });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("GitHubCloud", ex.Message, StringComparison.Ordinal);
    }
}
