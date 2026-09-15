using System.Globalization;
using System.Security.Claims;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Reautenticacion antes de una operacion sensible (ASVS 3.7.1).
/// </summary>
/// <remarks>
/// Publicar reglas mueve el score de toda la organizacion. Una sesion abierta hace
/// horas en un equipo desatendido no deberia bastar: se pide al IdP que vuelva a
/// autenticar (<c>max_age=0</c>, OIDC Core 3.1.2.1) y solo se acepta una marca de
/// autenticacion reciente del token que devuelve.
/// </remarks>
public static class StepUp
{
    /// <summary>Marca en las propiedades del challenge: este inicio de sesion es una reautenticacion.</summary>
    public const string PropertyKey = "modscore.stepup";

    /// <summary>Unica pantalla a la que se vuelve despues. Una lista blanca no se puede usar para redirigir fuera.</summary>
    public const string RulesPath = "/admin/rules";

    /// <summary>
    /// Que tan reciente tiene que ser el <c>auth_time</c> del token para contar como
    /// reautenticacion. Un token de un inicio de sesion viejo reutilizado por el IdP no lo es.
    /// </summary>
    public static readonly TimeSpan FreshAuthentication = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A donde volver. Solo rutas de una lista blanca: un parametro de retorno libre es
    /// la forma clasica de convertir un login en una redireccion abierta.
    /// </summary>
    public static string SafeReturnPath(string? requested)
        => string.Equals(requested, RulesPath, StringComparison.Ordinal) ? RulesPath : "/";

    /// <summary>Registra en la identidad cuando se autentico, reemplazando cualquier marca previa.</summary>
    public static void Stamp(ClaimsIdentity identity, DateTimeOffset authenticatedAt)
    {
        ArgumentNullException.ThrowIfNull(identity);

        Clear(identity);
        identity.AddClaim(new Claim(
            ChannelPermissions.ReauthenticatedAtClaimType,
            authenticatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Toma el <c>auth_time</c> del token del IdP y lo registra solo si es reciente.
    /// Devuelve true si lo registro.
    /// </summary>
    public static bool StampFromIdentityProvider(ClaimsIdentity identity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(identity);

        // Un claim con el nombre de la marca que viniera del IdP no vale: la marca la
        // escribe esta capa y nadie mas.
        Clear(identity);

        if (!long.TryParse(identity.FindFirst("auth_time")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds is <= 0 or >= 253_402_300_800)
        {
            return false;
        }

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);

        if (authenticatedAt > now + ClockSkew || now - authenticatedAt > FreshAuthentication)
        {
            return false;
        }

        Stamp(identity, authenticatedAt);
        return true;
    }

    private static void Clear(ClaimsIdentity identity)
    {
        foreach (var claim in identity.FindAll(ChannelPermissions.ReauthenticatedAtClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }
    }
}
