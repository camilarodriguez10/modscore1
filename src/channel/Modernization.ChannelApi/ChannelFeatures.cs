using Microsoft.Extensions.Options;
using Modernization.Security;

namespace Modernization.ChannelApi;

/// <summary>
/// Pantallas que se ocultan desde la configuración de la capa Channel (sección
/// <c>Features</c>), sin recompilar el sitio: en el equipo de la empresa el sitio llega ya
/// compilado y no se puede volver a compilar (ADR 0029).
/// </summary>
/// <remarks>
/// Desde el 2026-10-21 Code y Roadmap están apagadas por decisión de producto. Apagada, una
/// pantalla no aparece en la navegación, su ruta vuelve al dashboard y sus endpoints
/// responden 404: lo que no se usa no queda expuesto. Se encienden con
/// <c>Features__Code=true</c> o <c>Features__PortfolioRoadmap=true</c>.
/// </remarks>
public sealed class ChannelFeatures
{
    public const string SectionName = "Features";

    /// <summary>La pantalla Code: obsolescencia y riesgo por componentes (ADR 0026).</summary>
    public bool Code { get; init; }

    /// <summary>La pantalla Roadmap del portafolio y su export CSV. El plan de cada aplicación no depende de esto.</summary>
    public bool PortfolioRoadmap { get; init; }
}

public static class ChannelFeatureEndpoints
{
    /// <summary>
    /// El endpoint responde 404 mientras su pantalla esté apagada. El chequeo es por petición,
    /// así que un cambio de configuración recargada no exige reiniciar.
    /// </summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, Func<ChannelFeatures, bool> enabled)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(enabled);

        return builder.AddEndpointFilter(async (context, next) =>
        {
            var features = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<ChannelFeatures>>().CurrentValue;

            return enabled(features)
                ? await next(context).ConfigureAwait(false)
                : ApiProblem.Create(StatusCodes.Status404NotFound, "not-found", "Not found", "This screen is turned off.");
        });
    }
}
