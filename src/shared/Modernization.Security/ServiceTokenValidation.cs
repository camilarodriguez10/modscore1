namespace Modernization.Security;

/// <summary>Como se valida el token que presenta la capa Channel.</summary>
public enum ServiceTokenMode
{
    /// <summary>Secreto compartido, rotado a mano (hasta la Fase 15).</summary>
    Static,

    /// <summary>Token de vida corta emitido por APIM o el IdP (ADR 0020, opcion C).</summary>
    Jwt,

    /// <summary>
    /// Los dos a la vez, solo durante el cambio: la capa Channel puede estar en una
    /// version y la System en otra, y el cambio no puede pedir un corte.
    /// </summary>
    Both,
}

/// <summary>El veredicto de un token de servicio: si vale y quien lo presenta.</summary>
/// <remarks>
/// El motivo es para el log del servidor, nunca para la respuesta: decir por que
/// fallo un token ayuda a enumerarlos.
/// </remarks>
public readonly record struct ServiceTokenValidation(bool IsValid, string? Caller, string? Reason)
{
    public static ServiceTokenValidation Valid(string caller) => new(true, caller, null);

    public static ServiceTokenValidation Invalid(string reason) => new(false, null, reason);
}

/// <summary>
/// Valida el token con que la capa Channel llama a la System.
/// </summary>
/// <remarks>
/// Una interfaz y no un <c>if</c> dentro del handler: el secreto compartido y el
/// token de APIM se validan de formas que no se parecen en nada, y el handler no
/// tiene por que saber cual esta configurado ([ADR 0020](../../../docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md)).
/// </remarks>
public interface IServiceTokenValidator
{
    ValueTask<ServiceTokenValidation> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
