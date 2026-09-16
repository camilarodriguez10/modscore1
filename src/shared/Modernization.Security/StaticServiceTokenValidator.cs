using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Modernization.Security;

/// <summary>
/// Secreto compartido entre las dos capas, con rotacion sin corte.
/// </summary>
/// <remarks>
/// La comparacion es en tiempo constante a proposito: un compare normal filtra el
/// token por temporizacion. Acepta el token vigente y, durante una rotacion, el
/// siguiente (runbook de rotacion de secretos).
/// <para>
/// Es el modo de hoy y el que queda mientras no exista APIM. Su limite conocido es
/// H-16: el secreto es estatico y quien lo tiene puede declarar cualquier alcance.
/// </para>
/// </remarks>
public sealed class StaticServiceTokenValidator(IOptionsMonitor<ServiceTokenOptions> options) : IServiceTokenValidator
{
    public ValueTask<ServiceTokenValidation> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var current = options.Get(ServiceTokenAuthenticationHandler.SchemeName);
        var presented = Encoding.UTF8.GetBytes(token);

        var primary = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(current.Token),
            presented);

        var secondary = !string.IsNullOrEmpty(current.SecondaryToken)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(current.SecondaryToken),
                presented);

        return ValueTask.FromResult(primary || secondary
            ? ServiceTokenValidation.Valid(current.CallerName)
            : ServiceTokenValidation.Invalid("el token no coincide con el vigente ni con el de rotacion"));
    }
}
