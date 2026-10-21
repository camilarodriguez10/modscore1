using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration.KeyPerFile;

namespace Modernization.SystemApi;

/// <summary>
/// Fuera de Development, una cadena de conexión de los conectores con contraseña solo puede
/// venir de un archivo del gestor de secretos (auditoría del 2026-10-21, ADR 0037).
/// </summary>
/// <remarks>
/// Una contraseña en <c>appsettings.json</c> termina en el repositorio o en la imagen; en una
/// variable de entorno, en volcados de proceso y en la consola del orquestador. Las dos están
/// prohibidas por el plan (secc. 7.2). La contraseña va en <c>PasswordFile</c> o la cadena entera
/// en un archivo de <c>Secrets:Directory</c>.
/// </remarks>
public static partial class ConnectorSecretGuard
{
    [GeneratedRegex(@"(^|;)\s*(password|pwd)\s*=\s*[^;\s]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PasswordKeyword();

    /// <summary>Los problemas, en inglés para el log de arranque. Vacío en Development o si no hay ninguno.</summary>
    public static IReadOnlyList<string> Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsDevelopment() || configuration is not IConfigurationRoot root)
        {
            return [];
        }

        var problems = new List<string>();

        foreach (var connection in configuration.GetSection("Connectors:Connections").GetChildren())
        {
            var key = $"Connectors:Connections:{connection.Key}:ConnectionString";

            foreach (var provider in root.Providers)
            {
                if (provider is KeyPerFileConfigurationProvider)
                {
                    continue;
                }

                if (provider.TryGet(key, out var value) && value is not null && PasswordKeyword().IsMatch(value))
                {
                    problems.Add(
                        $"{key} carries a password outside the secrets directory ({provider.GetType().Name}). "
                        + "Move it to PasswordFile, or the whole connection string to a file in Secrets:Directory.");
                }
            }
        }

        return problems;
    }
}
