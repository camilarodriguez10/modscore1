using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Modernization.Api.Tests;

/// <summary>
/// Los controles de seguridad que el plan promete, verificados. Sin estos tests
/// serian solo intenciones escritas en un documento.
/// </summary>
public sealed class SecurityTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    // ------------------------------------------------------ autenticacion

    [Theory]
    [InlineData("/api/session")]
    [InlineData("/api/portfolio")]
    [InlineData("/api/applications/14381/overview")]
    [InlineData("/api/applications/14381/explain")]
    [InlineData("/api/lineage")]
    public async Task ChannelEndpointsRejectAnonymousRequests(string path)
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Un API responde 401; nunca redirige a una pagina de login.</summary>
    [Fact]
    public async Task UnauthenticatedRequestDoesNotRedirect()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/portfolio", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("/system/applications")]
    [InlineData("/system/applications/14381/score")]
    [InlineData("/system/scores")]
    [InlineData("/system/sources")]
    [InlineData("/system/ruleset")]
    public async Task SystemEndpointsRejectRequestsWithoutAServiceToken(string path)
    {
        var client = fixture.CreateSystemClient(authenticated: false);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SystemLayerRejectsAWrongServiceToken()
    {
        var client = fixture.CreateSystemClient(authenticated: false);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-equivocado");

        using var response = await client.GetAsync(new Uri("/system/ruleset", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------ sesion

    /// <summary>
    /// Patron BFF: el navegador recibe una cookie httpOnly, nunca un token. Es lo
    /// que hace que un XSS no pueda robar credenciales.
    /// </summary>
    [Fact]
    public async Task SessionCookieIsHttpOnlySecureAndStrict()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri("/api/login", UriKind.Relative));

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.Contains("session", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("__Host-", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponseNeverCarriesAnAccessToken()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", body, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------- CSRF

    [Fact]
    public async Task MutationWithoutACsrfTokenIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.PostAsync(new Uri("/api/logout", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MutationWithAValidCsrfTokenIsAccepted()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        // El token se emite al consultar la sesion.
        using var session = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        var csrf = ExtractCookie(session, "csrf");

        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);

        using var response = await client.PostAsync(new Uri("/api/logout", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // -------------------------------------------------------- cabeceras

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("Referrer-Policy", "no-referrer")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Cross-Origin-Opener-Policy", "same-origin")]
    public async Task SecurityHeadersArePresentOnEveryResponse(string header, string expected)
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/session", UriKind.Relative));

        Assert.Equal(expected, response.Headers.GetValues(header).Single());
    }

    /// <summary>Un API que solo devuelve JSON no necesita permitir nada.</summary>
    [Fact]
    public async Task ContentSecurityPolicyDeniesEverythingOnAJsonApi()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Contains("default-src 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", csp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PortfolioResponsesAreNotCacheable()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri("/api/portfolio?pageSize=5", UriKind.Relative));

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    // -------------------------------------------------- validacion de entrada

    [Theory]
    [InlineData("/api/portfolio?page=0")]
    [InlineData("/api/portfolio?pageSize=0")]
    [InlineData("/api/portfolio?pageSize=5000")]
    public async Task InvalidPaginationIsRejected(string path)
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Un EAI que no cumple el formato se rechaza en la puerta: no llega al
    /// compositor, ni a la URL de la capa System, ni de vuelta en la respuesta.
    /// </summary>
    [Theory]
    [InlineData("%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("..%2F..%2Fetc%2Fpasswd")]
    [InlineData("14381%27%20OR%201%3D1")]
    [InlineData("99999999999999999999")]
    public async Task MalformedEaiCodeIsRejectedAtTheEdge(string eaiCode)
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri($"/api/applications/{eaiCode}/overview", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>El mensaje de error no hace eco de la entrada del usuario.</summary>
    [Fact]
    public async Task ErrorMessagesDoNotEchoUserInput()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri("/api/applications/%3Cscript%3E/overview", UriKind.Relative));

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------- guardia del esquema de desarrollo

    /// <summary>
    /// El esquema de desarrollo no autentica a nadie. Si llegara a un entorno real,
    /// cualquiera entraria — asi que el proceso no debe arrancar.
    /// </summary>
    [Fact]
    public void DevelopmentAuthSchemeCannotStartOutsideDevelopment()
    {
        using var factory = new WebApplicationFactory<Modernization.ChannelApi.ChannelApiMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.UseSetting("Authentication:UseDevelopmentScheme", "true");
                builder.UseSetting("SystemApi:BaseUrl", "https://system-api.test");
                builder.UseSetting("SystemApi:ServiceToken", LayeredApiFixture.ServiceToken);
            });

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Development", ex.Message, StringComparison.Ordinal);
    }

    private static string ExtractCookie(HttpResponseMessage response, string contains)
    {
        var raw = response.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains(contains, StringComparison.OrdinalIgnoreCase));

        var pair = raw.Split(';')[0];
        return pair[(pair.IndexOf('=', StringComparison.Ordinal) + 1)..];
    }
}
