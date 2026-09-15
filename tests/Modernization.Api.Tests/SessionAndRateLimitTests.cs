using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Modernization.ChannelApi.Auth;

namespace Modernization.Api.Tests;

/// <summary>Reloj que avanza solo cuando la prueba lo pide.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// Vida de la sesion (ASVS 3.3). Cada prueba levanta su propio host: mover el reloj
/// de uno no puede afectar a otra.
/// </summary>
public sealed class SessionLifetimeTests
{
    private static TwoLayerHost CreateHost(ManualTimeProvider clock) => new(
        new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "20",
        },
        configureChannelServices: services => services.AddSingleton<TimeProvider>(clock));

    /// <summary>
    /// Con la cookie como unico almacen, cerrar sesion solo la borraba del
    /// navegador: una copia robada seguia valiendo ocho horas. Ahora deja de valer
    /// en el mismo instante.
    /// </summary>
    [Fact]
    public async Task LogoutInvalidatesACopiedCookie()
    {
        using var host = CreateHost(new ManualTimeProvider(DateTimeOffset.UtcNow));
        var user = host.CreateChannelClient();

        using var login = await user.GetAsync(new Uri("/api/login", UriKind.Relative));
        var copied = TwoLayerHost.ExtractCookie(login, "session");

        using var thief = host.CreateCookielessChannelClient();
        thief.DefaultRequestHeaders.Add("Cookie", $"{ChannelAuthentication.SessionCookieName}={copied}");

        using (var before = await thief.GetAsync(new Uri("/api/session", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        using var session = await user.GetAsync(new Uri("/api/session", UriKind.Relative));
        user.DefaultRequestHeaders.Add("X-CSRF-Token", TwoLayerHost.ExtractCookie(session, "csrf"));

        using (var logout = await user.PostAsync(new Uri("/api/logout", UriKind.Relative), content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using var after = await thief.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    /// <summary>
    /// Dos inicios de sesion sin cookie previa son dos sesiones independientes:
    /// cerrar una no toca la otra. Es el caso de todo login con OIDC, porque el
    /// callback del IdP es cross-site y la cookie SameSite=Strict no viaja en el:
    /// una cookie plantada no puede fijar la sesion (ADR 0011).
    /// </summary>
    [Fact]
    public async Task LoginsWithoutAPreviousCookieCreateIndependentSessions()
    {
        using var host = CreateHost(new ManualTimeProvider(DateTimeOffset.UtcNow));

        var first = await host.CreateMutatingClientAsync();
        var second = await host.CreateAuthenticatedChannelClientAsync();

        using (var logout = await first.PostAsync(new Uri("/api/logout", UriKind.Relative), content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using var stillValid = await second.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
    }

    /// <summary>
    /// Con una sesion abierta, volver a iniciar sesion en desarrollo no firma otra
    /// encima, porque el manejador renovaria la misma clave.
    /// </summary>
    [Fact]
    public async Task LoggingInAgainDoesNotSignOverTheOpenSession()
    {
        using var host = CreateHost(new ManualTimeProvider(DateTimeOffset.UtcNow));
        var client = await host.CreateAuthenticatedChannelClientAsync();

        using var again = await client.GetAsync(new Uri("/api/login", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.False(
            again.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.Contains("session", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SessionExpiresAfterThirtyMinutesWithoutActivity()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = CreateHost(clock);
        var client = await host.CreateAuthenticatedChannelClientAsync();

        using (var active = await client.GetAsync(new Uri("/api/session", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        }

        clock.Advance(TimeSpan.FromMinutes(31));

        using var idle = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, idle.StatusCode);
    }

    /// <summary>
    /// Actividad cada 20 minutos: nunca vence por inactividad, y aun asi vence al
    /// cruzar las 12 horas desde el inicio de sesion.
    /// </summary>
    [Fact]
    public async Task AnActiveSessionStillExpiresAfterTwelveHours()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = CreateHost(clock);
        var client = await host.CreateAuthenticatedChannelClientAsync();

        var elapsed = TimeSpan.Zero;

        while (elapsed + TimeSpan.FromMinutes(20) < TimeSpan.FromHours(12))
        {
            clock.Advance(TimeSpan.FromMinutes(20));
            elapsed += TimeSpan.FromMinutes(20);

            using var response = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Menos que el tiempo de inactividad, pero cruzando las 12 horas.
        clock.Advance(TimeSpan.FromMinutes(25));

        using var expired = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }
}

public sealed class RateLimitTests
{
    private static readonly Dictionary<string, string?> SmallPortfolio = new()
    {
        ["DataSource:Provider"] = "Dummy",
        ["DataSource:ApplicationCount"] = "20",
    };

    /// <summary>
    /// El limitador corria antes de autenticar, asi que ningun usuario tenia su
    /// propio balde: todos compartian el de la IP, que detras del ingress es la
    /// misma. Agotar ese balde no puede dejar sin servicio a un usuario con sesion.
    /// </summary>
    [Fact]
    public async Task AuthenticatedUsersAreLimitedPerUserNotPerAddress()
    {
        using var host = new TwoLayerHost(
            SmallPortfolio,
            new Dictionary<string, string?> { ["RateLimiting:RequestsPerMinute"] = "5" });

        var user = await host.CreateAuthenticatedChannelClientAsync();
        var anonymous = host.CreateChannelClient();

        var last = HttpStatusCode.OK;
        for (var i = 0; i < 10 && last != HttpStatusCode.TooManyRequests; i++)
        {
            using var response = await anonymous.GetAsync(new Uri("/api/portfolio", UriKind.Relative));
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);

        using var own = await user.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task UploadsHaveTheirOwnMuchLowerLimit()
    {
        using var host = new TwoLayerHost(
            SmallPortfolio,
            new Dictionary<string, string?> { ["RateLimiting:UploadsPerWindow"] = "2" });

        var client = await host.CreateMutatingClientAsync();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 3; i++)
        {
            using var form = new MultipartFormDataContent();
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes("EAI_CODE\n1001\n"));
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", "product_strategy.csv");

            using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
            statuses.Add(response.StatusCode);
        }

        Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[0]);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);

        // Agotar las cargas no bloquea las lecturas.
        using var read = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task SimulationsHaveTheirOwnLimit()
    {
        using var host = new TwoLayerHost(
            SmallPortfolio,
            new Dictionary<string, string?> { ["RateLimiting:SimulationsPerMinute"] = "2" });

        var client = await host.CreateMutatingClientAsync();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync(
                new Uri("/api/applications/14381/whatif/scenario", UriKind.Relative),
                new { levers = new[] { new { key = "mfa.enable", parameter = (int?)null } } });

            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[2]);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(2));
    }
}
