using System.Security.Cryptography;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Proteccion CSRF por doble envio.
/// </summary>
/// <remarks>
/// La cookie de sesion es SameSite=Strict, que ya bloquea el grueso de los ataques
/// CSRF. Esto es la segunda capa: navegadores antiguos, y cualquier futuro
/// relajamiento de SameSite que haga falta por un flujo de terceros.
/// </remarks>
public static class CsrfProtection
{
    public const string CookieName = "__Host-modscore.csrf";
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>Emite el token si la sesion aun no tiene uno.</summary>
    public static string EnsureToken(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Cookies.TryGetValue(CookieName, out var existing) && !string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        // Hexadecimal, no Base64: ASP.NET Core codifica en URL el valor de la
        // cookie al escribirla, asi que un valor con '+', '/' o '=' no coincidiria
        // con lo que el cliente lee de document.cookie y reenvia en el header.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        // Legible por el script a proposito: el patron de doble envio necesita que
        // el front pueda copiarla al header. La cookie de SESION sigue siendo httpOnly.
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });

        return token;
    }

    /// <summary>
    /// Valida el token en toda mutacion. Los metodos seguros no lo requieren.
    /// </summary>
    public static bool IsValid(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
        {
            return true;
        }

        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var header))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(cookie),
            System.Text.Encoding.UTF8.GetBytes(header.ToString()));
    }
}
