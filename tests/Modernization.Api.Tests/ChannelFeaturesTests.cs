using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

/// <summary>
/// La navegación del 2026-10-21: Code y Roadmap se apagan desde la configuración de la capa
/// Channel, y vienen apagadas. Apagada, una pantalla no se ofrece en la sesión y sus
/// endpoints responden 404; encendida, funciona como siempre.
/// </summary>
public sealed class ChannelFeaturesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string?> Dummy = new() { ["DataSource:Provider"] = "Dummy" };

    /// <summary>La configuración de fábrica (appsettings.json): las dos apagadas.</summary>
    private static readonly Dictionary<string, string?> FactoryDefaults = new()
    {
        ["Features:Code"] = "false",
        ["Features:PortfolioRoadmap"] = "false",
    };

    [Fact]
    public async Task ByDefaultTheSessionOffersNeitherCodeNorRoadmap()
    {
        using var host = new TwoLayerHost(Dummy, FactoryDefaults);
        var client = await host.CreateMutatingClientAsync();

        var session = await client.GetFromJsonAsync<SessionView>(new Uri("/api/session", UriKind.Relative), Json);

        Assert.NotNull(session);
        Assert.False(session.Features.Code);
        Assert.False(session.Features.PortfolioRoadmap);
    }

    [Theory]
    [InlineData("/api/code")]
    [InlineData("/api/code/applications/14381")]
    [InlineData("/api/roadmap")]
    [InlineData("/api/roadmap/export.csv")]
    public async Task ATurnedOffScreenAnswersNotFound(string url)
    {
        using var host = new TwoLayerHost(Dummy, FactoryDefaults);
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>El plan de cada aplicación no es la pantalla Roadmap: sigue disponible.</summary>
    [Fact]
    public async Task TheFuturePlanOfAnApplicationDoesNotDependOnTheRoadmapScreen()
    {
        using var host = new TwoLayerHost(Dummy, FactoryDefaults);
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(new Uri("/api/applications/14381/roadmap", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TurnedOnTheScreensAnswerAsAlways()
    {
        using var host = new TwoLayerHost(Dummy, new Dictionary<string, string?>
        {
            ["Features:Code"] = "true",
            ["Features:PortfolioRoadmap"] = "true",
        });
        var client = await host.CreateMutatingClientAsync();

        var session = await client.GetFromJsonAsync<SessionView>(new Uri("/api/session", UriKind.Relative), Json);
        using var code = await client.GetAsync(new Uri("/api/code", UriKind.Relative));
        using var roadmap = await client.GetAsync(new Uri("/api/roadmap", UriKind.Relative));

        Assert.True(session!.Features.Code);
        Assert.True(session.Features.PortfolioRoadmap);
        Assert.Equal(HttpStatusCode.OK, code.StatusCode);
        Assert.Equal(HttpStatusCode.OK, roadmap.StatusCode);
    }
}
