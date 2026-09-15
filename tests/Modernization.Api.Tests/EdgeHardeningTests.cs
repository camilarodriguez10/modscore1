using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

// -------------------------------------------------------------------- SPA

/// <summary>El build de la SPA servido por el channel-api, como en despliegue.</summary>
public sealed class SpaHostingFixture : IDisposable
{
    public const string AssetPath = "/assets/app-3f9a1c.js";

    private readonly string _webRoot = Path.Combine(
        Path.GetTempPath(),
        "modscore-web-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public SpaHostingFixture()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><head><script type=\"module\" src=\"{AssetPath}\"></script></head>"
            + "<body><div id=\"root\"></div></body></html>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app-3f9a1c.js"), "export {};");

        Host = new TwoLayerHost(
            new Dictionary<string, string?>
            {
                ["DataSource:Provider"] = "Dummy",
                ["DataSource:ApplicationCount"] = "20",
            },
            new Dictionary<string, string?> { ["webroot"] = _webRoot });
    }

    public TwoLayerHost Host { get; }

    public void Dispose()
    {
        Host.Dispose();

        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}

public sealed class SpaHostingTests(SpaHostingFixture fixture) : IClassFixture<SpaHostingFixture>
{
    /// <summary>
    /// La CSP del plan (secc. 6.3), verificada sobre la respuesta real: sin
    /// 'unsafe-inline', sin 'unsafe-eval', sin origenes de terceros.
    /// </summary>
    [Fact]
    public async Task ShellIsServedWithAStrictContentSecurityPolicy()
    {
        var client = fixture.Host.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("style-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("http", csp, StringComparison.OrdinalIgnoreCase);

        // El shell no se cachea: una version nueva tiene que llegar al primer intento.
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepLinksFallBackToTheShell()
    {
        var client = fixture.Host.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/aplicaciones/14381/calculo", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HashedAssetsAreCacheableAndImmutable()
    {
        var client = fixture.Host.CreateChannelClient();

        using var response = await client.GetAsync(new Uri(SpaHostingFixture.AssetPath, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Una ruta de API inexistente nunca responde con la SPA y un 200.</summary>
    [Fact]
    public async Task UnknownApiRoutesAreNotAnsweredWithTheShell()
    {
        var anonymous = fixture.Host.CreateChannelClient();
        using (var response = await anonymous.GetAsync(new Uri("/api/no-existe", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        using var authenticated = await client.GetAsync(new Uri("/api/no-existe", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, authenticated.StatusCode);
        Assert.NotEqual("text/html", authenticated.Content.Headers.ContentType?.MediaType);
    }
}

// ------------------------------------------------------ guardas de despliegue

public sealed class DeploymentGuardTests
{
    private static WebApplicationFactory<Modernization.SystemApi.SystemApiMarker> ProductionSystem(
        IReadOnlyDictionary<string, string?> settings)
        => new WebApplicationFactory<Modernization.SystemApi.SystemApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("DataSource:Provider", "Dummy");
                builder.UseSetting("DataSource:ApplicationCount", "20");

                foreach (var (key, value) in settings)
                {
                    builder.UseSetting(key, value);
                }
            });

    /// <summary>Con AllowedHosts="*" el servidor construye URLs con el Host que mande el cliente.</summary>
    [Fact]
    public void ProductionRefusesToStartWithoutExplicitHosts()
    {
        using var factory = ProductionSystem(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["ServiceToken:Token"] = TwoLayerHost.ServiceToken,
        });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("AllowedHosts", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRefusesTheRepositoryDevelopmentToken()
    {
        using var factory = ProductionSystem(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "localhost",
            ["ServiceToken:Token"] = "dev-only-service-token-not-for-any-real-environment",
        });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ServiceToken:Token", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionDoesNotPublishTheApiMap()
    {
        using var factory = ProductionSystem(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "localhost",
            ["ServiceToken:Token"] = TwoLayerHost.ServiceToken,
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TwoLayerHost.ServiceToken);

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Un 500 en produccion no lleva el tipo de excepcion ni la pila.</summary>
    [Fact]
    public async Task ProductionErrorsCarryNoInternalDetail()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            "modscore-broken-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var version = Path.Combine(dataDirectory, "20260901-120000-abcd");
        Directory.CreateDirectory(version);

        File.WriteAllText(Path.Combine(version, "eai_master.csv"), "columna,equivocada\n1,2\n");
        File.WriteAllText(
            Path.Combine(version, "manifest.json"),
            "{\"id\":\"20260901-120000-abcd\",\"label\":\"rota\",\"capturedAt\":\"2026-09-01T12:00:00+00:00\","
            + "\"applicationCount\":1,\"files\":[\"eai_master.csv\"],\"uploadedBy\":\"tests\"}");

        try
        {
            using var factory = ProductionSystem(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "localhost",
                ["ServiceToken:Token"] = TwoLayerHost.ServiceToken,
                ["DataSource:Provider"] = "File",
                ["DataSource:Directory"] = dataDirectory,
            });

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TwoLayerHost.ServiceToken);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Portfolio-Scope", "*");

            using var response = await client.GetAsync(new Uri("/system/scores", UriKind.Relative));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Modernization.", body, StringComparison.Ordinal);
            Assert.DoesNotContain("columna", body, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Rotacion sin corte: durante la ventana se aceptan el token vigente y el siguiente.</summary>
    [Fact]
    public async Task SecondaryTokenIsAcceptedDuringARotation()
    {
        const string Next = "next-service-token-for-the-rotation-window-0987654321";

        using var factory = new WebApplicationFactory<Modernization.SystemApi.SystemApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("DataSource:Provider", "Dummy");
                builder.UseSetting("ServiceToken:Token", TwoLayerHost.ServiceToken);
                builder.UseSetting("ServiceToken:SecondaryToken", Next);
            });

        foreach (var (token, expected) in new[]
        {
            (TwoLayerHost.ServiceToken, HttpStatusCode.OK),
            (Next, HttpStatusCode.OK),
            ("otro-token-que-no-es-ninguno-de-los-dos-configurados", HttpStatusCode.Unauthorized),
        })
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await client.GetAsync(new Uri("/system/ruleset", UriKind.Relative));
            Assert.Equal(expected, response.StatusCode);
        }
    }
}

// ------------------------------------------------------------------ entrada

public sealed class InputHardeningTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    /// <summary>Una clave de palanca fuera de la lista blanca no sale del borde.</summary>
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("mfa.enable' OR '1'='1")]
    [InlineData("")]
    public async Task LeverKeysOutsideTheWhitelistAreRejectedAtTheEdge(string key)
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/applications/14381/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key, parameter = (int?)null } } });

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OR '1'", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// El error de la capa System llega curado: solo su detalle, sin el cuerpo
    /// crudo ni la clave que envio el cliente.
    /// </summary>
    [Fact]
    public async Task ScenarioErrorsCarryOnlyTheCuratedDetail()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/applications/14381/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "palanca.inventada", parameter = (int?)null } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = document.RootElement.GetProperty("detail").GetString();

        Assert.NotNull(detail);
        Assert.Contains("does not exist", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("palanca.inventada", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("{", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("traceId", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverlongFiltersAreRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri("/api/portfolio?lob=" + new string('x', 101), UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// La etiqueta se guarda, se muestra y se audita. Un salto de linea fabricaria
    /// lineas de log; U+202E invierte como se lee en pantalla.
    /// </summary>
    [Theory]
    [InlineData("Carga 0809\nNIVEL=CRITICO usuario=admin")]
    [InlineData("Carga normal‮vsc.exe")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task UploadLabelsWithControlCharactersOrExcessLengthAreRejected(string label)
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("EAI_CODE\n1001\n"));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "product_strategy.csv");
        form.Add(new StringContent(label), "label");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid label", document.RootElement.GetProperty("title").GetString());
    }
}

// ---------------------------------------------------------------- auditoria

public sealed class UploadAuditTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Antes la version quedaba firmada por "usuario autenticado". Ahora lleva el
    /// subject de quien la publico, reenviado por la capa Channel.
    /// </summary>
    [Fact]
    public async Task APublishedVersionRecordsWhoUploadedIt()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        form.Add(new StringContent("Carga auditada"), "label");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.NotNull(result.Version);

        var manifest = Path.Combine(fixture.DataDirectory, result.Version.Id, "manifest.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));

        Assert.Equal("dev-user", document.RootElement.GetProperty("uploadedBy").GetString());
        Assert.Equal("Carga auditada", document.RootElement.GetProperty("label").GetString());
    }
}
