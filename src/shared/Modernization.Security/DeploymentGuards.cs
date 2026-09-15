using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Modernization.Security;

/// <summary>
/// Configuraciones aceptables en un portatil y peligrosas en un despliegue. Fuera
/// de Development, el proceso no arranca con ellas.
/// </summary>
public static class DeploymentGuards
{
    /// <summary>Marca que llevan los secretos de desarrollo del repositorio.</summary>
    private const string DevelopmentMarker = "dev-only";

    public static void Validate(IConfiguration configuration, IHostEnvironment environment, params string[] secretKeys)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(secretKeys);

        if (environment.IsDevelopment())
        {
            return;
        }

        // Con "*" el servidor acepta cualquier Host, y hay codigo que lo usa para
        // construir URLs absolutas: la redirect_uri de OIDC, por ejemplo.
        var hosts = configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(hosts) || hosts.Split(';').Any(h => h.Trim() == "*"))
        {
            throw new InvalidOperationException(
                $"'AllowedHosts' debe listar los hosts del servicio (entorno '{environment.EnvironmentName}'). "
                + "Aceptar cualquier Host permite envenenar las URLs que el servidor construye.");
        }

        foreach (var key in secretKeys)
        {
            if (configuration[key]?.Contains(DevelopmentMarker, StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException(
                    $"'{key}' tiene el valor de desarrollo del repositorio. En un despliegue viene del gestor de secretos.");
            }
        }
    }
}
