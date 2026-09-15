using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modernization.ChannelApi.Auth;

namespace Modernization.Api.Tests;

/// <summary>
/// El contrato de la reautenticacion, igual para todo proveedor de identidad (ADR 0020):
/// la sesion anterior se cierra antes de abrir la nueva.
/// </summary>
public sealed class ReauthenticationFlowTests
{
    private static readonly string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Con OIDC la vuelta del IdP no trae la cookie SameSite=Strict, asi que la sesion
    /// anterior solo se puede cerrar antes de salir. Antes de esta prueba quedaba viva en
    /// el almacen hasta vencer por inactividad.
    /// </summary>
    [Fact]
    public async Task Oidc_ClosesThePreviousSessionBeforeLeavingForTheIdentityProvider()
    {
        var authentication = new RecordingAuthenticationService();
        await using var services = ServicesWith(authentication);
        var context = ContextWith(services);

        var result = await new OidcReauthentication().StartAsync(context, StepUp.RulesPath);

        Assert.Equal(["signout:" + Cookie], authentication.Calls);

        var challenge = Assert.IsType<ChallengeHttpResult>(result);
        Assert.Equal([OpenIdConnectDefaults.AuthenticationScheme], challenge.AuthenticationSchemes);
        Assert.Equal(StepUp.RulesPath, challenge.Properties?.RedirectUri);
        Assert.True(challenge.Properties?.Items.ContainsKey(StepUp.PropertyKey));
    }

    [Fact]
    public async Task Development_ClosesThePreviousSessionThenSignsANewOne()
    {
        var authentication = new RecordingAuthenticationService();
        await using var services = ServicesWith(authentication);
        var context = ContextWith(services);

        var flow = new DevelopmentReauthentication(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance);
        var result = await flow.StartAsync(context, StepUp.RulesPath);

        Assert.Equal(["signout:" + Cookie, "signin:" + Cookie], authentication.Calls);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Equal(StepUp.RulesPath, redirect.Url);
    }

    private static ServiceProvider ServicesWith(IAuthenticationService authentication)
        => new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();

    private static DefaultHttpContext ContextWith(IServiceProvider services) => new()
    {
        RequestServices = services,
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin-1")], "test")),
    };

    /// <summary>Registra el orden de las operaciones de autenticacion.</summary>
    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public List<string> Calls { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Calls.Add("challenge:" + scheme);
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Calls.Add("forbid:" + scheme);
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            Calls.Add("signin:" + scheme);
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Calls.Add("signout:" + scheme);
            return Task.CompletedTask;
        }
    }
}
