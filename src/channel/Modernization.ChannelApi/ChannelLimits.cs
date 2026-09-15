using System.Globalization;

namespace Modernization.ChannelApi;

/// <summary>
/// Limites de recursos del borde. Cada uno acota lo que un usuario autenticado
/// puede consumir: autenticado no significa inofensivo.
/// </summary>
public static class ChannelLimits
{
    /// <summary>Cuerpo maximo de cualquier peticion salvo la carga.</summary>
    public const long DefaultMaxRequestBodyBytes = 1024 * 1024;

    /// <summary>Debe coincidir con el tope por archivo de la capa System.</summary>
    public const long UploadMaxFileBytes = 64L * 1024 * 1024;

    /// <summary>Debe coincidir con el tope por carga de la capa System.</summary>
    public const long UploadMaxTotalBytes = 256L * 1024 * 1024;

    /// <summary>La carga mas el margen de los encabezados multipart y la etiqueta.</summary>
    public const long UploadMaxRequestBytes = UploadMaxTotalBytes + (1024 * 1024);

    public const string UploadPolicy = "upload";

    public const string SimulationPolicy = "simulation";

    /// <summary>Simular un cambio de reglas recalcula todo el portafolio, dos veces.</summary>
    public const string RulesSimulationPolicy = "rules-simulation";

    /// <summary>Publicar reglas mueve el score de toda la organizacion.</summary>
    public const string RulesPublishPolicy = "rules-publish";

    /// <summary>Lo que puede tardar en viajar y validarse una carga completa.</summary>
    public static TimeSpan UploadTimeout { get; } = TimeSpan.FromMinutes(5);

    public static string Megabytes(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>
/// Frecuencias maximas por usuario. Configurables porque dependen del tamano real
/// del equipo; los valores por defecto asumen personas, no scripts.
/// </summary>
public sealed record RateLimits(
    int RequestsPerMinute,
    int SimulationsPerMinute,
    int UploadsPerWindow,
    TimeSpan UploadWindow,
    int RulesSimulationsPerMinute,
    int RulesPublishesPerWindow,
    TimeSpan RulesPublishWindow)
{
    public static RateLimits From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new RateLimits(
            configuration.GetValue("RateLimiting:RequestsPerMinute", 120),
            configuration.GetValue("RateLimiting:SimulationsPerMinute", 30),
            configuration.GetValue("RateLimiting:UploadsPerWindow", 5),
            TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:UploadWindowMinutes", 10)),
            configuration.GetValue("RateLimiting:RulesSimulationsPerMinute", 10),
            configuration.GetValue("RateLimiting:RulesPublishesPerWindow", 5),
            TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:RulesPublishWindowMinutes", 60)));
    }
}

/// <summary>
/// Sirve el build de la SPA desde el mismo origen que el API: la cookie de sesion
/// es SameSite=Strict y __Host-, y no hace falta abrir CORS.
/// </summary>
internal static class SpaHosting
{
    /// <summary>
    /// Activa los archivos estaticos si hay un build publicado. Devuelve false en
    /// desarrollo, donde la SPA la sirve Vite.
    /// </summary>
    public static bool UseStaticAssets(WebApplication app)
    {
        var root = app.Environment.WebRootPath;

        if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, "index.html")))
        {
            return false;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Los assets llevan hash en el nombre: inmutables y sin datos, se
                // pueden cachear. index.html conserva el no-store.
                if (context.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal))
                {
                    context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                }
            },
        });

        return true;
    }

    public static void MapFallback(WebApplication app)
    {
        // Una ruta de API inexistente es 404 (o 401 sin sesion), nunca la SPA: un
        // cliente que se equivoca de URL tiene que enterarse.
        app.MapFallback("/api/{**path}", () => Results.NotFound());

        // El shell de la SPA no lleva datos: se sirve sin sesion para poder mostrar
        // la pantalla de inicio de sesion.
        app.MapFallbackToFile("index.html").AllowAnonymous();
    }
}
