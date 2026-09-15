using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modernization.ChannelApi.Clients;
using Modernization.Security;

namespace Modernization.Api.Tests;

/// <summary>
/// Las dos capas en memoria, con la Channel llamando a la System por HTTP real.
/// Es el montaje comun de todos los fixtures.
/// </summary>
/// <remarks>
/// El punto del montaje es que el trafico entre capas sea real: la capa Channel
/// llama a la System con su token de servicio, su alcance y su usuario, igual que
/// en despliegue. Un doble de prueba en el medio ocultaria precisamente lo que hay
/// que verificar.
/// </remarks>
public sealed class TwoLayerHost : IDisposable
{
    public const string ServiceToken = "test-service-token-with-enough-length-1234567890";

    private readonly WebApplicationFactory<Modernization.SystemApi.SystemApiMarker> _system;
    private readonly WebApplicationFactory<Modernization.ChannelApi.ChannelApiMarker> _channel;

    public TwoLayerHost(
        IReadOnlyDictionary<string, string?> systemSettings,
        IReadOnlyDictionary<string, string?>? channelSettings = null,
        Action<IServiceCollection>? configureChannelServices = null,
        Action<IServiceCollection>? configureSystemServices = null)
    {
        ArgumentNullException.ThrowIfNull(systemSettings);

        _system = new WebApplicationFactory<Modernization.SystemApi.SystemApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("ServiceToken:Token", ServiceToken);

                foreach (var (key, value) in systemSettings)
                {
                    builder.UseSetting(key, value);
                }

                if (configureSystemServices is not null)
                {
                    builder.ConfigureServices(configureSystemServices);
                }
            });

        _channel = new WebApplicationFactory<Modernization.ChannelApi.ChannelApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.UseSetting("SystemApi:BaseUrl", "http://system-api.test");
                builder.UseSetting("SystemApi:ServiceToken", ServiceToken);
                builder.UseSetting("Authentication:UseDevelopmentScheme", "true");

                // Limites altos por defecto: las pruebas comparten un mismo usuario y no
                // deben tropezar con el rate limiting. Las que lo prueban lo bajan.
                builder.UseSetting("RateLimiting:RequestsPerMinute", "100000");
                builder.UseSetting("RateLimiting:SimulationsPerMinute", "100000");
                builder.UseSetting("RateLimiting:UploadsPerWindow", "100000");
                builder.UseSetting("RateLimiting:RulesSimulationsPerMinute", "100000");
                builder.UseSetting("RateLimiting:RulesPublishesPerWindow", "100000");

                foreach (var (key, value) in channelSettings ?? new Dictionary<string, string?>())
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureServices(services =>
                {
                    // Los dos clientes de la capa System apuntan al host en memoria.
                    // Si el de la carga quedara fuera, esas pruebas saldrian a la red.
                    services.AddHttpClient<SystemApiClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => _system.Server.CreateHandler());

                    services.AddHttpClient(SystemApiClient.UploadClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => _system.Server.CreateHandler());

                    configureChannelServices?.Invoke(services);
                });
            });
    }

    public IServiceProvider ChannelServices => _channel.Services;

    public IServiceProvider SystemServices => _system.Services;

    /// <summary>Cliente sin autenticar de la capa Channel.</summary>
    public HttpClient CreateChannelClient() => _channel.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://channel-api.test"),
    });

    /// <summary>Cliente sin manejo de cookies: para reenviar a mano una cookie copiada.</summary>
    public HttpClient CreateCookielessChannelClient() => _channel.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false,
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://channel-api.test"),
    });

    /// <summary>Cliente de la capa Channel con sesion ya establecida.</summary>
    public async Task<HttpClient> CreateAuthenticatedChannelClientAsync()
    {
        var client = CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/login", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>Cliente autenticado y con token CSRF cargado, listo para mutar.</summary>
    public async Task<HttpClient> CreateMutatingClientAsync()
    {
        var client = await CreateAuthenticatedChannelClientAsync();

        using var session = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookie(session, "csrf"));

        return client;
    }

    /// <summary>
    /// Cliente directo a la capa System, con su token de servicio. Por defecto
    /// declara alcance completo, como lo haria la Channel para un usuario con acceso
    /// a todo el portafolio.
    /// </summary>
    public HttpClient CreateSystemClient(bool authenticated = true, string? scope = PortfolioScope.FullValue)
    {
        var client = _system.CreateClient();

        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ServiceToken);
        }

        if (scope is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(PortfolioScope.HeaderName, scope);
        }

        return client;
    }

    public static string ExtractCookie(HttpResponseMessage response, string contains)
    {
        ArgumentNullException.ThrowIfNull(response);

        var raw = response.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains(contains, StringComparison.OrdinalIgnoreCase));

        var pair = raw.Split(';')[0];
        return pair[(pair.IndexOf('=', StringComparison.Ordinal) + 1)..];
    }

    public void Dispose()
    {
        _channel.Dispose();
        _system.Dispose();
    }
}
