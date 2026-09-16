namespace Modernization.Security;

/// <summary>
/// Acepta el secreto compartido o el token de vida corta, en ese orden.
/// </summary>
/// <remarks>
/// Es la ventana del cambio a APIM y nada mas: mientras dure, una replica vieja de la
/// capa Channel sigue funcionando y una nueva ya usa el token de APIM. Cuando todas
/// hayan cambiado, se pasa a <see cref="ServiceTokenMode.Jwt"/> y el secreto se retira
/// del gestor de secretos, que es lo que cierra H-16.
/// <para>
/// El orden importa poco para la seguridad —los dos validan de verdad— y mucho para el
/// costo: el secreto se compara en memoria y el JWT puede pedir llaves.
/// </para>
/// </remarks>
public sealed class EitherServiceTokenValidator(
    StaticServiceTokenValidator shared,
    JwtServiceTokenValidator issued) : IServiceTokenValidator
{
    public async ValueTask<ServiceTokenValidation> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var first = await shared.ValidateAsync(token, cancellationToken).ConfigureAwait(false);

        return first.IsValid
            ? first
            : await issued.ValidateAsync(token, cancellationToken).ConfigureAwait(false);
    }
}
