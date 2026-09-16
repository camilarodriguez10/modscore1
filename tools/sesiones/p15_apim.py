"""Fase 15 - flujo de reautenticacion para APIM y alcance derivado de grupos."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


# --------------------------------------------------- reautenticacion con APIM
patch("src/channel/Modernization.ChannelApi/Auth/Reauthentication.cs", [
    (
        """/// <summary>
/// OIDC: pide al IdP que vuelva a pedir las credenciales (<c>max_age=0</c>). La marca la
/// escribe <see cref="StepUp.StampFromIdentityProvider"/> al validar el token de vuelta.
/// </summary>""",
        """/// <summary>
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
/// </summary>""",
    ),
])

# ------------------------------- registro del proveedor y parametros extra
patch("src/channel/Modernization.ChannelApi/Auth/ChannelAuthentication.cs", [
    (
        """        // Punto de reemplazo cuando la autenticacion pase a OAuth mediante APIM (ADR 0020).
        services.TryAddSingleton<IReauthenticationFlow, OidcReauthentication>();""",
        """        // Quien conduce la reautenticacion (ADR 0020). Con APIM delante, el flujo es el
        // mismo codigo de autorizacion y cambian los parametros que se piden.
        if (string.Equals(configuration["Authentication:Provider"], "Apim", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<IReauthenticationFlow, ApimReauthentication>();
        }
        else
        {
            services.TryAddSingleton<IReauthenticationFlow, OidcReauthentication>();
        }""",
    ),
    (
        """                if (context.Properties.Items.ContainsKey(StepUp.PropertyKey))
                {
                    context.ProtocolMessage.MaxAge = "0";
                    context.ProtocolMessage.Prompt = "login";
                }

                return Task.CompletedTask;""",
        """                if (context.Properties.Items.ContainsKey(StepUp.PropertyKey))
                {
                    context.ProtocolMessage.MaxAge = "0";
                    context.ProtocolMessage.Prompt = "login";
                }

                // Parametros propios de APIM, si los hay: viajan como propiedades del
                // challenge y solo se agregan los que esta capa puso.
                foreach (var (key, value) in context.Properties.Items)
                {
                    if (key.StartsWith(ApimReauthentication.ParameterPrefix, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(value))
                    {
                        context.ProtocolMessage.SetParameter(
                            key[ApimReauthentication.ParameterPrefix.Length..],
                            value);
                    }
                }

                return Task.CompletedTask;""",
    ),
])

# ---------------------------------------------- alcance derivado de grupos
patch("src/channel/Modernization.ChannelApi/Auth/ChannelPermissions.cs", [
    (
        "public sealed class ChannelPermissions(string scopeClaimType, TimeSpan? reauthenticationWindow = null)\n{",
        """/// <summary>
/// Traduccion de grupos del IdP a alcance del portafolio, para cuando el IdP no puede
/// emitir el claim propio (PA-03).
/// </summary>
/// <param name="ClaimType">Claim donde llegan los grupos; <c>groups</c> por defecto.</param>
/// <param name="Map">Grupo a entradas de alcance, en formato de header: <c>lob:Individual;country:Chile</c>.</param>
public sealed record GroupScopeMapping(string ClaimType, IReadOnlyDictionary<string, string> Map)
{
    public const string DefaultClaimType = "groups";
}

public sealed class ChannelPermissions(
    string scopeClaimType,
    TimeSpan? reauthenticationWindow = null,
    GroupScopeMapping? groupScopes = null)
{""",
    ),
    (
        "    public PortfolioScope ScopeOf(ClaimsPrincipal user) => PortfolioScope.FromClaims(user, ScopeClaimType);",
        """    /// <summary>
    /// El alcance del usuario. Manda el claim propio; los grupos son el camino de
    /// respaldo para un IdP que no puede emitirlo (PA-03).
    /// </summary>
    /// <remarks>
    /// No se suman: si el claim esta, decide el. Sumar grupos a un alcance ya emitido
    /// podria ampliarlo sin que nadie lo haya pedido.
    /// </remarks>
    public PortfolioScope ScopeOf(ClaimsPrincipal user)
    {
        var declared = PortfolioScope.FromClaims(user, ScopeClaimType);

        if (!declared.IsEmpty || groupScopes is null)
        {
            return declared;
        }

        ArgumentNullException.ThrowIfNull(user);

        var entries = user.FindAll(groupScopes.ClaimType)
            .Select(claim => groupScopes.Map.TryGetValue(claim.Value, out var scope) ? scope : null)
            .Where(scope => !string.IsNullOrWhiteSpace(scope));

        var header = string.Join(';', entries);

        return string.IsNullOrEmpty(header) ? PortfolioScope.None : PortfolioScope.FromHeader(header);
    }""",
    ),
])
