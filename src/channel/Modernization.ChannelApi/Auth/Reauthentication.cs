using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Modernization.Security;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Como se vuelve a autenticar a alguien antes de una operacion sensible (ASVS 3.7.1).
/// </summary>
/// <remarks>
/// Lo especifico del proveedor de identidad vive detras de esta interfaz. Hoy hay dos
/// implementaciones: el esquema de desarrollo y OIDC. Cuando la autenticacion pase a
/// OAuth mediante APIM (ADR 0020) se agrega una tercera; el endpoint, la marca de
/// reautenticacion (<see cref="StepUp"/>) y la ventana de validez no cambian.
/// <para>
/// <b>Toda implementacion cierra la sesion anterior antes de abrir la nueva.</b> Firmar
/// encima renovaria la misma clave, que es la fijacion de sesion; y dejarla abierta la
/// deja viva en el almacen hasta vencer por inactividad (ADR 0011).
/// </para>
/// </remarks>
public interface IReauthenticationFlow
{
    /// <summary>Inicia la reautenticacion y vuelve a <paramref name="returnPath"/>, ya validado.</summary>
    Task<IResult> StartAsync(HttpContext context, string returnPath);
}

/// <summary>
/// Desarrollo: emite una sesion nueva con la marca del momento. No autentica a nadie;
/// solo existe con el esquema de desarrollo, que no arranca fuera de Development.
/// </summary>
public sealed class DevelopmentReauthentication(IConfiguration configuration, ILoggerFactory loggers) : IReauthenticationFlow
{
    public async Task<IResult> StartAsync(HttpContext context, string returnPath)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subject = ChannelPermissions.SubjectOf(context.User);

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ChannelAuthentication.CreateDevelopmentPrincipal(configuration)).ConfigureAwait(false);

        SecurityAudit.StepUpCompleted(SecurityAudit.CreateLogger(loggers), subject);

        return Results.Redirect(returnPath);
    }
}

/// <summary>
/// APIM: el mismo flujo de codigo, pero el autorizador es APIM y puede exigir un nivel
/// de autenticacion propio (ADR 0020, opcion A).
/// </summary>
/// <remarks>
/// Lo que agrega sobre OIDC son los parametros que APIM espera en la autorizacion, por
/// ejemplo <c>acr_values</c> para exigir MFA (SEC-04). Salen de configuracion y viajan en
/// las propiedades del challenge, que es lo unico que el manejador de OIDC lee.
/// </remarks>
public sealed class ApimReauthentication(IConfiguration configuration) : IReauthenticationFlow
{
    /// <summary>Prefijo de las propiedades que se convierten en parametros de la autorizacion.</summary>
    public const string ParameterPrefix = "apim:";

    public async Task<IResult> StartAsync(HttpContext context, string returnPath)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Igual que con OIDC: la vuelta es una navegacion entre sitios y no trae la
        // cookie SameSite=Strict, asi que la sesion anterior se cierra antes de salir.
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        var properties = new AuthenticationProperties { RedirectUri = returnPath };
        properties.Items[StepUp.PropertyKey] = "1";

        foreach (var parameter in configuration.GetSection("Authentication:Apim:AuthorizeParameters").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(parameter.Value))
            {
                properties.Items[ParameterPrefix + parameter.Key] = parameter.Value;
            }
        }

        return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
    }
}

/// <summary>
/// OIDC: pide al IdP que vuelva a pedir las credenciales (<c>max_age=0</c>). La marca la
/// escribe <see cref="StepUp.StampFromIdentityProvider"/> al validar el token de vuelta.
/// </summary>
public sealed class OidcReauthentication : IReauthenticationFlow
{
    public async Task<IResult> StartAsync(HttpContext context, string returnPath)
    {
        ArgumentNullException.ThrowIfNull(context);

        // La vuelta desde el IdP es una navegacion entre sitios: la cookie SameSite=Strict
        // no viaja en ella, asi que el manejador de la cookie no ve la sesion anterior y
        // no puede reemplazarla. Se cierra aqui, antes de salir. Si la persona abandona el
        // IdP queda sin sesion, que es lo correcto para quien pidio reautenticarse.
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        var properties = new AuthenticationProperties { RedirectUri = returnPath };
        properties.Items[StepUp.PropertyKey] = "1";

        return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
    }
}
