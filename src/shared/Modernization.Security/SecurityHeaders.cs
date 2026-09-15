using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Modernization.Security;

/// <summary>
/// Cabeceras de seguridad aplicadas en el borde. Se centralizan aqui para que las
/// dos capas apliquen exactamente las mismas y no se desincronicen.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>CSP de una respuesta JSON: no hay nada que permitir.</summary>
    public const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>
    /// CSP de la SPA. Sin 'unsafe-inline' ni 'unsafe-eval', y sin nonce: el build
    /// de produccion no tiene un solo script ni estilo en linea, asi que 'self' es
    /// mas estricto que cualquier nonce. Tampoco hay origenes de terceros: las
    /// fuentes tipograficas se sirven desde el mismo origen.
    /// </summary>
    public const string SpaContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; "
        + "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    private static readonly PathString[] ApiPrefixes =
        [new("/api"), new("/system"), new("/health"), new("/openapi")];

    /// <summary>
    /// Aplica las cabeceras de seguridad a toda respuesta.
    /// </summary>
    /// <param name="app">Pipeline al que se agrega el middleware.</param>
    /// <param name="apiOnly">
    /// True para un servicio que nunca sirve HTML, como la capa System: toda
    /// respuesta lleva la CSP de API. Con false, las rutas de API la llevan igual
    /// y el resto recibe la CSP de la SPA.
    /// </param>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool apiOnly)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Permissions-Policy"] =
                "accelerometer=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";

            // Evita que un proxy o un navegador cachee respuestas con datos del
            // portafolio, que es informacion sensible. Los assets con hash de la
            // SPA lo sobrescriben: no llevan datos y son inmutables.
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            headers["Pragma"] = "no-cache";

            if (apiOnly || IsApiRequest(context.Request.Path))
            {
                headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
            }
            else
            {
                // upgrade-insecure-requests solo tiene sentido servido por HTTPS;
                // sobre HTTP local reescribiria los assets a un esquema que no existe.
                headers["Content-Security-Policy"] = context.Request.IsHttps
                    ? SpaContentSecurityPolicy + "; upgrade-insecure-requests"
                    : SpaContentSecurityPolicy;
            }

            await next().ConfigureAwait(false);
        });
    }

    public static bool IsApiRequest(PathString path)
        => Array.Exists(ApiPrefixes, prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
