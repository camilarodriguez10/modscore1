using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

// ---------------------------------------------------------------- fixtures

/// <summary>Un usuario que consulta todo el portafolio pero no puede publicar.</summary>
public sealed class ReaderFixture : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "modscore-reader-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public ReaderFixture()
    {
        FileBackedApiFixture.SeedVersion(_dataDirectory);

        Host = new TwoLayerHost(
            new Dictionary<string, string?>
            {
                ["DataSource:Provider"] = "File",
                ["DataSource:Directory"] = _dataDirectory,
            },
            new Dictionary<string, string?>
            {
                ["Authentication:Development:Roles:0"] = "portfolio.read",
                ["Authentication:Development:Scopes:0"] = "*",
            });
    }

    public TwoLayerHost Host { get; }

    public void Dispose()
    {
        Host.Dispose();

        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}

/// <summary>Un usuario limitado a un solo LOB.</summary>
public sealed class LobScopedFixture : IDisposable
{
    public const string Lob = "Individual";

    public TwoLayerHost Host { get; } = new(
        new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "60",
        },
        new Dictionary<string, string?>
        {
            ["Authentication:Development:Roles:0"] = "portfolio.read",
            ["Authentication:Development:Scopes:0"] = "lob:" + Lob,
        });

    public void Dispose() => Host.Dispose();
}

/// <summary>Autenticado, pero sin roles ni alcance asignados.</summary>
public sealed class NoAccessFixture : IDisposable
{
    public TwoLayerHost Host { get; } = new(
        new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "20",
        },
        new Dictionary<string, string?>
        {
            ["Authentication:Development:Roles:0"] = "",
            ["Authentication:Development:Scopes:0"] = "",
        });

    public void Dispose() => Host.Dispose();
}

// ------------------------------------------------------------------- tests

/// <summary>
/// Publicar reemplaza el portafolio de toda la organizacion: estar autenticado no
/// alcanza. Antes de la Fase 08 cualquier usuario con sesion podia hacerlo.
/// </summary>
public sealed class ReaderPermissionTests(ReaderFixture fixture) : IClassFixture<ReaderFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SessionReportsReadingWithoutPublishing()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var session = await client.GetFromJsonAsync<SessionView>(new Uri("/api/session", UriKind.Relative), Json);

        Assert.NotNull(session);
        Assert.True(session.Permissions.CanRead);
        Assert.True(session.Permissions.FullPortfolio);
        Assert.False(session.Permissions.CanUpload);
    }

    [Fact]
    public async Task AReaderCannotPublishAnUpload()
    {
        var client = await fixture.Host.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("EAI_CODE,Eliminate_Count\n1001,0\n"));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "product_strategy.csv");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// La pantalla no ofrece un formulario que el servidor va a rechazar, y explica
    /// por que: el adaptador si admite cargas, el usuario no.
    /// </summary>
    [Fact]
    public async Task UploadScreenExplainsThePermissionThatIsMissing()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal("File", view.Provider);
        Assert.False(view.UploadEnabled);
        Assert.Equal("ingestion.write", view.DisabledReason?.Args["role"]);
    }
}

public sealed class NoAccessTests(NoAccessFixture fixture) : IClassFixture<NoAccessFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>La sesion responde igual: es lo que permite a la SPA decir "sin acceso".</summary>
    [Fact]
    public async Task SessionSaysThereIsNoAccess()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var session = await client.GetFromJsonAsync<SessionView>(new Uri("/api/session", UriKind.Relative), Json);

        Assert.NotNull(session);
        Assert.True(session.IsAuthenticated);
        Assert.False(session.Permissions.CanRead);
    }

    [Theory]
    [InlineData("/api/portfolio")]
    [InlineData("/api/roadmap")]
    [InlineData("/api/ingestion")]
    [InlineData("/api/applications/14381/overview")]
    public async Task EveryScreenIsForbidden(string path)
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

/// <summary>
/// El alcance se aplica en la capa System, sobre los datos (plan secc. 7.2). Lo que
/// un usuario limitado no puede ver no llega a la capa Channel.
/// </summary>
public sealed class LobScopeTests(LobScopedFixture fixture) : IClassFixture<LobScopedFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PortfolioAndItsAggregatesOnlyCoverTheScopedLob()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        using var document = await GetJsonAsync(client, "/api/portfolio?pageSize=100");
        var root = document.RootElement;

        var lobs = root.GetProperty("byLob").EnumerateArray().Select(g => g.GetProperty("key").GetString()).ToList();
        Assert.Equal(new string?[] { LobScopedFixture.Lob }, lobs);

        var visible = root.GetProperty("totalApplications").GetInt32();
        Assert.InRange(visible, 1, await FullPortfolioCountAsync() - 1);
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("explain")]
    [InlineData("whatif")]
    [InlineData("roadmap")]
    public async Task AnApplicationOutsideTheScopeIsIndistinguishableFromOneThatDoesNotExist(string screen)
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var outside = await FindApplicationAsync(inScope: false);

        using var response = await client.GetAsync(
            new Uri($"/api/applications/{outside}/{screen}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnApplicationInsideTheScopeIsVisible()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var inside = await FindApplicationAsync(inScope: true);

        using var response = await client.GetAsync(new Uri($"/api/applications/{inside}/explain", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HistoryOfAnApplicationOutsideTheScopeCarriesNoScores()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        var outside = await FindApplicationAsync(inScope: false);

        var history = await client.GetFromJsonAsync<IReadOnlyList<HistoryPointView>>(
            new Uri($"/api/applications/{outside}/history", UriKind.Relative), Json);

        Assert.NotNull(history);
        Assert.All(history, point => Assert.Null(point.Score));
    }

    /// <summary>El linaje tiene totales de archivo completo y EAI de filas rechazadas.</summary>
    [Fact]
    public async Task LineageRequiresTheFullPortfolio()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/lineage", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// El header de alcance lo escribe la capa Channel a partir de la sesion. Uno
    /// enviado por el navegador no llega a la capa System.
    /// </summary>
    [Fact]
    public async Task ABrowserCannotWidenItsScopeWithAHeader()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Portfolio-Scope", "*");

        using var document = await GetJsonAsync(client, "/api/portfolio?pageSize=100");

        var lobs = document.RootElement.GetProperty("byLob").EnumerateArray()
            .Select(g => g.GetProperty("key").GetString())
            .ToList();

        Assert.Equal(new string?[] { LobScopedFixture.Lob }, lobs);
    }

    [Fact]
    public async Task SessionReportsAPartialScope()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var session = await client.GetFromJsonAsync<SessionView>(new Uri("/api/session", UriKind.Relative), Json);

        Assert.NotNull(session);
        Assert.True(session.Permissions.CanRead);
        Assert.False(session.Permissions.FullPortfolio);
        Assert.False(session.Permissions.CanUpload);
    }

    private async Task<int> FullPortfolioCountAsync()
    {
        using var system = fixture.Host.CreateSystemClient();
        using var document = await GetJsonAsync(system, "/system/applications?pageSize=1");
        return document.RootElement.GetProperty("totalCount").GetInt32();
    }

    private async Task<string> FindApplicationAsync(bool inScope)
    {
        using var system = fixture.Host.CreateSystemClient();
        using var document = await GetJsonAsync(system, "/system/applications?pageSize=200");

        return document.RootElement.GetProperty("items").EnumerateArray()
            .First(a => string.Equals(
                a.GetProperty("lob").GetString(),
                LobScopedFixture.Lob,
                StringComparison.OrdinalIgnoreCase) == inScope)
            .GetProperty("eaiCode").GetString()!;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}

/// <summary>La capa System aplica el alcance aunque la llamen directamente.</summary>
public sealed class SystemScopeTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    /// <summary>Sin header de alcance no se ve nada: nunca el portafolio completo.</summary>
    [Theory]
    [InlineData("/system/applications?pageSize=10")]
    [InlineData("/system/scores?pageSize=10")]
    public async Task ARequestWithoutScopeSeesAnEmptyPortfolio(string path)
    {
        using var client = fixture.CreateSystemClientWithScope(scope: null);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task SourcesRequireTheFullScope()
    {
        using var client = fixture.CreateSystemClientWithScope("lob:Individual");

        using var response = await client.GetAsync(new Uri("/system/sources", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Defensa en profundidad: la capa Channel ya lo exige, y esta tambien.</summary>
    [Fact]
    public async Task UploadRequiresTheFullScope()
    {
        using var client = fixture.CreateSystemClientWithScope("lob:Individual");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("x"), "label");

        using var response = await client.PostAsync(new Uri("/system/ingestion/upload", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
