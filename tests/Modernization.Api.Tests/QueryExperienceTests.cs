using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>Un portafolio dummy con variedad suficiente de paises, LOB, CIO y roadmaps.</summary>
public sealed class QueryExperienceFixture : IDisposable
{
    public TwoLayerHost Host { get; } = new(new Dictionary<string, string?>
    {
        ["DataSource:Provider"] = "Dummy",
        ["DataSource:ApplicationCount"] = "120",
    });

    public void Dispose() => Host.Dispose();
}

/// <summary>
/// Fase 10, puntos 1, 2 y 4: los filtros son los mismos en todas las pantallas y
/// cada estadistica es un filtro.
/// </summary>
public sealed class QueryExperienceTests(QueryExperienceFixture fixture) : IClassFixture<QueryExperienceFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var value = await client.GetFromJsonAsync<T>(new Uri(url, UriKind.Relative), Json);
        Assert.NotNull(value);
        return value;
    }

    /// <summary>
    /// Criterio de aceptacion de la Fase 10: hacer clic en un pais filtra el
    /// portafolio, y los agregados pasan a hablar solo de ese pais.
    /// </summary>
    [Fact]
    public async Task ClickingACountryFiltersTheTableAndItsAggregates()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var all = await GetAsync<PortfolioView>(client, "/api/portfolio?pageSize=100");
        var mexico = Assert.Single(all.ByCountry, g => g.Key == "Mexico");

        var filtered = await GetAsync<PortfolioView>(client, "/api/portfolio?country=Mexico&pageSize=100");

        Assert.Equal(mexico.Count, filtered.TotalApplications);
        Assert.Equal(mexico.AverageScore, filtered.AverageScore);
        Assert.All(filtered.Applications, row => Assert.Equal("Mexico", row.Country));
        Assert.Equal("Mexico", Assert.Single(filtered.ByCountry).Key);
        Assert.Equal(filtered.TotalApplications, filtered.Distribution.Sum(b => b.Count));
    }

    /// <summary>
    /// Toda clave que emite un agregado es un filtro valido que devuelve exactamente
    /// su conteo. Esta prueba es la que detecto que las barras de roadmap emitian
    /// "Maintain (Patch)" mientras el filtro esperaba "MaintainPatch".
    /// </summary>
    [Theory]
    [InlineData("lob")]
    [InlineData("country")]
    [InlineData("cio")]
    [InlineData("disposition")]
    [InlineData("roadmap")]
    public async Task EveryAggregateKeyIsAFilterThatReturnsItsOwnCount(string dimension)
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var all = await GetAsync<PortfolioView>(client, "/api/portfolio?pageSize=1");

        var groups = dimension switch
        {
            "lob" => all.ByLob,
            "country" => all.ByCountry,
            "cio" => all.ByCio,
            "disposition" => all.ByDisposition,
            _ => all.ByRoadmap,
        };

        Assert.NotEmpty(groups);

        foreach (var group in groups.Where(g => g.Key != "Sin asignar"))
        {
            var filtered = await GetAsync<PortfolioView>(
                client,
                $"/api/portfolio?{dimension}={Uri.EscapeDataString(group.Key)}&pageSize=1");

            Assert.True(
                group.Count == filtered.TotalApplications,
                $"{dimension}={group.Key}: el agregado dice {group.Count} y el filtro devuelve {filtered.TotalApplications}");
        }
    }

    [Fact]
    public async Task ClickingABandFiltersToThatBand()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var all = await GetAsync<PortfolioView>(client, "/api/portfolio?pageSize=1");
        var band = all.Distribution.First(b => b.Count > 0);

        var filtered = await GetAsync<PortfolioView>(
            client,
            $"/api/portfolio?band={Uri.EscapeDataString(band.Band)}&pageSize=100");

        Assert.Equal(band.Count, filtered.TotalApplications);
        Assert.All(filtered.Applications, row => Assert.Equal(band.Band, row.Band));
    }

    /// <summary>Un tramo inexistente no es un portafolio vacio: es una entrada invalida.</summary>
    [Fact]
    public async Task AnUnknownBandIsRejectedInsteadOfLookingLikeAnEmptyPortfolio()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        using var channel = await client.GetAsync(new Uri("/api/portfolio?band=Excelente", UriKind.Relative));
        using var system = await fixture.Host.CreateSystemClient()
            .GetAsync(new Uri("/system/applications?band=Excelente", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, channel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, system.StatusCode);
    }

    [Fact]
    public async Task FiltersCombineAcrossDimensions()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var mexico = await GetAsync<PortfolioView>(client, "/api/portfolio?country=Mexico&pageSize=1");
        var lob = mexico.ByLob[0];

        var both = await GetAsync<PortfolioView>(
            client,
            $"/api/portfolio?country=Mexico&lob={Uri.EscapeDataString(lob.Key)}&pageSize=100");

        Assert.Equal(lob.Count, both.TotalApplications);
        Assert.All(both.Applications, row =>
        {
            Assert.Equal("Mexico", row.Country);
            Assert.Equal(lob.Key, row.Lob);
        });
    }

    // --------------------------------------------------------------- roadmap

    /// <summary>
    /// Punto 4: el roadmap por pais. Los grupos reparten el portafolio completo y la
    /// clave de cada uno, usada como filtro, reproduce su conteo.
    /// </summary>
    [Theory]
    [InlineData("country")]
    [InlineData("cio")]
    [InlineData("lob")]
    [InlineData("disposition")]
    [InlineData("roadmap")]
    public async Task TheRoadmapGroupsByDimensionAndEachGroupIsAFilter(string groupBy)
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var view = await GetAsync<PortfolioRoadmapView>(client, $"/api/roadmap?groupBy={groupBy}");

        Assert.Equal(groupBy, view.GroupBy, ignoreCase: true);
        Assert.NotEmpty(view.Groups);
        Assert.Equal(view.TotalApplications, view.Groups.Sum(g => g.Count));

        foreach (var group in view.Groups.Where(g => g.Key != "Sin asignar"))
        {
            var portfolio = await GetAsync<PortfolioView>(
                client,
                $"/api/portfolio?{groupBy}={Uri.EscapeDataString(group.Key)}&pageSize=1");

            Assert.True(
                group.Count == portfolio.TotalApplications,
                $"{groupBy}={group.Key}: el roadmap dice {group.Count} y el portafolio {portfolio.TotalApplications}");
        }
    }

    /// <summary>
    /// La proyeccion por aplicacion es la misma que da el plan individual: agrupar
    /// no recalcula nada, solo promedia distinto.
    /// </summary>
    [Fact]
    public async Task GroupingByApplicationMatchesEachIndividualPlan()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var view = await GetAsync<PortfolioRoadmapView>(client, "/api/roadmap?groupBy=application");

        Assert.Equal(view.TotalApplications, view.Groups.Count);
        Assert.All(view.Groups, g => Assert.Equal(1, g.Count));

        var sample = view.Groups.First(g => g.ProjectedScore is not null);
        var plan = await GetAsync<RoadmapPlanView>(client, $"/api/applications/{sample.Key}/roadmap");

        Assert.Equal(plan.BaselineScore, sample.AverageScore);
        Assert.Equal(plan.ProjectedScore, sample.ProjectedScore);
    }

    [Fact]
    public async Task TheRoadmapHonoursTheSameFiltersAsThePortfolio()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var portfolio = await GetAsync<PortfolioView>(client, "/api/portfolio?country=Chile&pageSize=1");

        var roadmap = await GetAsync<PortfolioRoadmapView>(client, "/api/roadmap?groupBy=country&country=Chile");

        Assert.Equal(portfolio.TotalApplications, roadmap.TotalApplications);
        Assert.Equal("Chile", Assert.Single(roadmap.Groups).Key);
    }

    [Theory]
    [InlineData("planeta")]
    [InlineData("3")]
    public async Task AnUnknownGroupingIsRejected(string groupBy)
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        using var channel = await client.GetAsync(new Uri($"/api/roadmap?groupBy={groupBy}", UriKind.Relative));
        using var system = await fixture.Host.CreateSystemClient()
            .GetAsync(new Uri($"/system/roadmap/portfolio?groupBy={groupBy}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, channel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, system.StatusCode);
    }

    /// <summary>
    /// El enum sigue valiendo para quien llame al API directamente, y devuelve lo
    /// mismo que el nombre de pantalla.
    /// </summary>
    [Fact]
    public async Task RoadmapFilterAcceptsTheEnumAndTheDisplayName()
    {
        var system = fixture.Host.CreateSystemClient();

        var byEnum = await system.GetFromJsonAsync<PagedResponse<ApplicationResponse>>(
            new Uri("/system/applications?roadmap=MaintainPatch&pageSize=1", UriKind.Relative), Json);
        var byDisplay = await system.GetFromJsonAsync<PagedResponse<ApplicationResponse>>(
            new Uri("/system/applications?roadmap=Maintain%20(Patch)&pageSize=1", UriKind.Relative), Json);

        Assert.NotNull(byEnum);
        Assert.NotNull(byDisplay);
        Assert.True(byEnum.TotalCount > 0);
        Assert.Equal(byEnum.TotalCount, byDisplay.TotalCount);
    }
}

/// <summary>
/// ADR 0012 aplicado a los filtros: un filtro acota dentro del alcance, nunca lo
/// amplia. Un usuario limitado a un LOB que pide otro no ve nada de ese otro.
/// </summary>
public sealed class ScopedFilterTests(LobScopedFixture fixture) : IClassFixture<LobScopedFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<string> AnotherLobAsync()
    {
        var all = await fixture.Host.CreateSystemClient().GetFromJsonAsync<PagedResponse<ApplicationResponse>>(
            new Uri("/system/applications?pageSize=200", UriKind.Relative), Json);

        return all!.Items.Select(a => a.Lob).First(l => l is not null && l != LobScopedFixture.Lob)!;
    }

    [Fact]
    public async Task FilteringByAnotherLobShowsNothingOfIt()
    {
        var other = await AnotherLobAsync();
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var portfolio = await client.GetFromJsonAsync<PortfolioView>(
            new Uri($"/api/portfolio?lob={Uri.EscapeDataString(other)}&pageSize=100", UriKind.Relative), Json);
        var roadmap = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri($"/api/roadmap?groupBy=application&lob={Uri.EscapeDataString(other)}", UriKind.Relative), Json);

        Assert.Equal(0, portfolio!.TotalApplications);
        Assert.Empty(portfolio.Applications);
        Assert.Equal(0, roadmap!.TotalApplications);
        Assert.Empty(roadmap.Groups);
    }

    [Fact]
    public async Task GroupingTheRoadmapNeverRevealsGroupsOutsideTheScope()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var byLob = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri("/api/roadmap?groupBy=lob", UriKind.Relative), Json);
        var options = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=1", UriKind.Relative), Json);

        Assert.Equal(LobScopedFixture.Lob, Assert.Single(byLob!.Groups).Key);

        // Las opciones de la barra de filtros salen de este mismo agregado: tampoco
        // pueden ofrecer un LOB que el usuario no ve.
        Assert.Equal(LobScopedFixture.Lob, Assert.Single(options!.ByLob).Key);
    }
}

/// <summary>Fase 10, puntos 5 y 6: la pantalla de Datos.</summary>
public sealed class DataScreenTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string UploadPath = "/api/ingestion/upload";

    private MultipartFormDataContent RenamedSeedForm(string label)
    {
        var form = new MultipartFormDataContent();
        var index = 0;

        foreach (var (_, content) in fixture.ReadSeedFiles())
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", $"Export Power BI ({index++}).csv");
        }

        form.Add(new StringContent(label), "label");
        return form;
    }

    private static MultipartFormDataContent AmbiguousForm(params string[] assign)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("EAI,Country\n1001,Chile\n"));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "datos.csv");

        foreach (var value in assign)
        {
            form.Add(new StringContent(value), "assign");
        }

        return form;
    }

    private static async Task<UploadResultView> ReadResultAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);
        Assert.NotNull(result);
        return result;
    }

    /// <summary>Criterio de la Fase 10, punto 6: los exports suben sin renombrar.</summary>
    [Fact]
    public async Task RenamedExportsArePublishedThroughTheScreen()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = RenamedSeedForm("Exports sin renombrar");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        var result = await ReadResultAsync(response);

        Assert.True(result.Accepted, string.Join(" · ", result.Errors));
        Assert.Equal(8, result.Files.Count);
        Assert.All(result.Files, f =>
        {
            Assert.StartsWith("Export Power BI", f.FileName, StringComparison.Ordinal);
            Assert.NotNull(f.CanonicalFileName);
        });
    }

    [Fact]
    public async Task AnAmbiguousFileComesBackWithItsHeaderAndTheOptions()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = AmbiguousForm();
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        var result = await ReadResultAsync(response);
        var file = Assert.Single(result.Files);

        Assert.False(result.Accepted);
        Assert.Null(file.CanonicalFileName);
        Assert.Equal(["EAI", "Country"], file.DetectedColumns);
        Assert.Contains("apim_onboarded_eais.csv", file.Candidates);
        Assert.Equal(NoticeCodes.UploadRejectedUnidentified, result.Summary.Code);
    }

    [Fact]
    public async Task AssigningTheSourceResolvesTheFile()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = AmbiguousForm("apim_onboarded_eais.csv");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        var result = await ReadResultAsync(response);
        var file = Assert.Single(result.Files);

        Assert.Equal("apim_onboarded_eais.csv", file.CanonicalFileName);
        Assert.Null(file.Error);
    }

    /// <summary>Una asignacion con forma de ruta no llega a ninguna parte.</summary>
    [Fact]
    public async Task AnAssignmentShapedLikeAPathIsRejected()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = AmbiguousForm("../../appsettings.json");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        var result = await ReadResultAsync(response);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.FileAssignmentShape);
    }

    [Fact]
    public async Task AssignmentsThatDoNotLineUpWithTheFilesAreRejected()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = AmbiguousForm("apim_onboarded_eais.csv", "eai_master.csv");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --------------------------------------------------------------- linaje

    /// <summary>
    /// Punto 5: el linaje es por carga. Sin version es la vigente, y el total sale de
    /// la carga, no de la fuente mejor cubierta.
    /// </summary>
    [Fact]
    public async Task LineageDescribesTheCurrentLoadByDefault()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var ingestion = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);
        var current = Assert.Single(ingestion!.Versions, v => v.IsCurrent);

        var lineage = await client.GetFromJsonAsync<LineageView>(new Uri("/api/lineage", UriKind.Relative), Json);

        Assert.Equal(current.Id, lineage!.VersionId);
        Assert.Equal(current.ApplicationCount, lineage.TotalApplications);
    }

    [Fact]
    public async Task LineageCanDescribeAnOlderLoad()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var ingestion = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);
        var initial = ingestion!.Versions.First(v => v.Label == "Carga inicial");

        var lineage = await client.GetFromJsonAsync<LineageView>(
            new Uri($"/api/lineage?version={initial.Id}", UriKind.Relative), Json);

        Assert.Equal(initial.Id, lineage!.VersionId);
        Assert.Equal("Carga inicial", lineage.VersionLabel);
        Assert.NotEmpty(lineage.Sources);
    }

    /// <summary>
    /// En el adaptador de archivos una version es un directorio: un identificador con
    /// forma de ruta no pasa del borde, y uno inexistente es 404, no un 500.
    /// </summary>
    [Theory]
    [InlineData("..%2F..%2Fsecrets", HttpStatusCode.BadRequest)]
    [InlineData("20990101T000000000", HttpStatusCode.NotFound)]
    public async Task AnInvalidOrUnknownVersionIsRejected(string version, HttpStatusCode expected)
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var response = await client.GetAsync(new Uri($"/api/lineage?version={version}", UriKind.Relative));

        Assert.Equal(expected, response.StatusCode);
    }
}
