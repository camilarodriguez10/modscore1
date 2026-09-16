using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modernization.Security;

/// <summary>
/// Autenticacion servicio a servicio para la capa System.
/// </summary>
/// <remarks>
/// La capa System nunca es alcanzable desde internet: solo la llama la capa
/// Channel. El token es el control de aplicacion; el de transporte es mTLS,
/// impuesto por el service mesh (ADR 0001). Uno no sustituye al otro.
/// <para>
/// La comparacion es en tiempo constante a proposito: un compare normal filtra
/// el token por temporizacion.
/// </para>
/// </remarks>
public sealed class ServiceTokenAuthenticationHandler(
    IOptionsMonitor<ServiceTokenOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ServiceTokenOptions>(options, logger, encoder)
{
    public const string SchemeName = "ServiceToken";

    /// <summary>Claim que distingue a un llamador de servicio de un usuario.</summary>
    public const string CallerTypeClaim = "caller_type";

    public const string ServiceCallerType = "service";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return AuthenticateResult.NoResult();
        }

        var value = header.ToString();
        const string Prefix = "Bearer ";

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Se esperaba un token Bearer.");
        }

        var presented = value[Prefix.Length..];

        // Que token es valido lo decide el validador configurado: secreto compartido
        // hoy, token de vida corta de APIM cuando exista ([ADR 0020], opcion C).
        var validator = Context.RequestServices.GetRequiredService<IServiceTokenValidator>();
        var outcome = await validator.ValidateAsync(presented, Context.RequestAborted).ConfigureAwait(false);

        if (!outcome.IsValid)
        {
            // Sin detalle en la respuesta: un mensaje especifico ayudaria a enumerar tokens.
            return AuthenticateResult.Fail("Token de servicio invalido.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, outcome.Caller ?? Options.CallerName),
                new Claim(CallerTypeClaim, ServiceCallerType),
            ],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public sealed class ServiceTokenOptions : AuthenticationSchemeOptions
{
    private const int MinimumLength = 32;

    /// <summary>
    /// Como se valida lo que presenta la capa Channel. <see cref="ServiceTokenMode.Static"/>
    /// exige el secreto compartido; <see cref="ServiceTokenMode.Jwt"/> no lo usa.
    /// </summary>
    public ServiceTokenMode Mode { get; set; } = ServiceTokenMode.Static;

    /// <summary>
    /// Token compartido con la capa Channel. Viene de Key Vault en despliegue;
    /// nunca de una variable de entorno ni del repositorio.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token entrante durante una rotacion. Vacio fuera de la ventana de cambio.
    /// </summary>
    public string? SecondaryToken { get; set; }

    /// <summary>Nombre del llamador autorizado, para trazas y auditoria.</summary>
    public string CallerName { get; set; } = "channel-api";

    public override void Validate()
    {
        base.Validate();

        if (Mode == ServiceTokenMode.Jwt)
        {
            // Con APIM el secreto compartido no existe: lo que hay que validar son
            // el emisor, la audiencia y las llaves, y de eso se ocupa su validador.
            return;
        }

        if (string.IsNullOrWhiteSpace(Token) || Token.Length < MinimumLength)
        {
            throw new InvalidOperationException(
                "El token de servicio debe estar configurado y tener al menos 32 caracteres. " +
                "Configuralo en 'ServiceToken:Token' desde el gestor de secretos.");
        }

        if (!string.IsNullOrEmpty(SecondaryToken) && SecondaryToken.Length < MinimumLength)
        {
            throw new InvalidOperationException(
                "'ServiceToken:SecondaryToken' debe tener al menos 32 caracteres, o quedar vacio.");
        }
    }
}
