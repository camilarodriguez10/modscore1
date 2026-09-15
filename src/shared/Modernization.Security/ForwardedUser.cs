using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Modernization.Security;

/// <summary>
/// Identidad del usuario final que la capa Channel reenvia a la System, para
/// auditoria.
/// </summary>
/// <remarks>
/// Es el identificador seudonimo del IdP (el subject), nunca el nombre ni el
/// correo: los logs no llevan PII (plan secc. 7.2). La capa System solo lo acepta
/// de un llamador autenticado con token de servicio.
/// </remarks>
public static class ForwardedUser
{
    public const string HeaderName = "X-Portfolio-User";

    public const string Unknown = "desconocido";

    private const int MaxLength = 128;

    /// <summary>
    /// Lista blanca de caracteres de un subject. Lo que no encaja se reemplaza por
    /// <see cref="Unknown"/> en lugar de sanearse: un identificador alterado no
    /// sirve para auditar.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return Unknown;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '@' or ':' or '|'))
            {
                return Unknown;
            }
        }

        return value;
    }

    /// <summary>Usuario final de la peticion, segun lo declara la capa Channel.</summary>
    public static string Read(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ServiceCaller.Is(context.User)
            ? Sanitize(context.Request.Headers[HeaderName].ToString())
            : Unknown;
    }
}

/// <summary>Reconoce al llamador autenticado con token de servicio.</summary>
internal static class ServiceCaller
{
    public static bool Is(ClaimsPrincipal user)
        => user.Identity?.IsAuthenticated == true
            && user.HasClaim(ServiceTokenAuthenticationHandler.CallerTypeClaim, ServiceTokenAuthenticationHandler.ServiceCallerType);
}
