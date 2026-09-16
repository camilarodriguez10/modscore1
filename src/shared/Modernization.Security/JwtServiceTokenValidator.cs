using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Modernization.Security;

/// <summary>Como validar el token de vida corta que emite APIM o el IdP.</summary>
public sealed class JwtServiceTokenOptions
{
    /// <summary>
    /// Autoridad que publica las llaves (`.well-known/openid-configuration`). Se deja
    /// vacia solo en pruebas, donde las llaves se inyectan directamente.
    /// </summary>
    public string? Authority { get; set; }

    public string Issuer { get; set; } = string.Empty;

    /// <summary>La capa System es la audiencia: un token para otra API no sirve aqui.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Identificadores de cliente aceptados (`azp`, `appid` o `client_id`). Vacio
    /// acepta cualquier cliente que el emisor haya autorizado para esta audiencia.
    /// </summary>
    public IList<string> AllowedCallers { get; } = [];

    /// <summary>Tolerancia de reloj. Corta a proposito: son dos servicios del mismo cluster.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Llaves fijas. Se usan en pruebas y cuando no hay descubrimiento.</summary>
    public IList<SecurityKey> SigningKeys { get; } = [];
}

/// <summary>
/// Valida el token de servicio como JWT firmado (ADR 0020, opcion C).
/// </summary>
/// <remarks>
/// Exige firma, emisor, audiencia y vencimiento; el algoritmo sale de una lista
/// blanca, para que un token con <c>alg: none</c> o con HMAC sobre la llave publica
/// no pase. Las llaves se descubren de la autoridad y se refrescan solas.
/// <para>
/// A diferencia del secreto compartido, este token vence: si lo roban, sirve
/// minutos y solo contra esta audiencia (H-16, ASVS 2.10.1).
/// </para>
/// </remarks>
public sealed partial class JwtServiceTokenValidator : IServiceTokenValidator, IDisposable
{
    private static readonly string[] AllowedAlgorithms =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
    ];

    private static readonly string[] CallerClaims = ["azp", "appid", "client_id"];

    private readonly JwtServiceTokenOptions _options;
    private readonly ILogger<JwtServiceTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _discovery;

    public JwtServiceTokenValidator(JwtServiceTokenOptions options, ILogger<JwtServiceTokenValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(options.Authority))
        {
            var metadata = options.Authority.TrimEnd('/') + "/.well-known/openid-configuration";

            _discovery = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadata,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
        }
        else if (options.SigningKeys.Count == 0)
        {
            throw new InvalidOperationException(
                "El validador JWT necesita 'ServiceToken:Jwt:Authority' o llaves de firma configuradas.");
        }
    }

    public void Dispose() => (_discovery as IDisposable)?.Dispose();

    public async ValueTask<ServiceTokenValidation> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = _options.ClockSkew,
            ValidAlgorithms = AllowedAlgorithms,
        };

        if (_options.SigningKeys.Count > 0)
        {
            parameters.IssuerSigningKeys = _options.SigningKeys;
        }
        else
        {
            var configuration = await _discovery!.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            parameters.IssuerSigningKeys = configuration.SigningKeys;
        }

        var result = await _handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);

        if (!result.IsValid)
        {
            // El detalle queda en el log del servidor; la respuesta no lo lleva.
            LogRejected(_logger, result.Exception?.GetType().Name ?? "sin detalle");
            return ServiceTokenValidation.Invalid("el token no validó");
        }

        var caller = CallerOf(result.ClaimsIdentity);

        if (_options.AllowedCallers.Count > 0
            && (caller is null || !_options.AllowedCallers.Contains(caller, StringComparer.Ordinal)))
        {
            LogUnexpectedCaller(_logger, SecurityAudit.Clean(caller));
            return ServiceTokenValidation.Invalid("el cliente del token no esta en la lista");
        }

        return ServiceTokenValidation.Valid(caller ?? _options.Audience);
    }

    private static string? CallerOf(ClaimsIdentity? identity)
    {
        if (identity is null)
        {
            return null;
        }

        foreach (var claim in CallerClaims)
        {
            var value = identity.FindFirst(claim)?.Value;

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return identity.FindFirst("sub")?.Value;
    }

    [LoggerMessage(EventId = 5304, Level = LogLevel.Warning,
        Message = "Token de servicio rechazado por la validacion JWT. causa={Cause}")]
    private static partial void LogRejected(ILogger logger, string cause);

    [LoggerMessage(EventId = 5305, Level = LogLevel.Warning,
        Message = "Token de servicio de un cliente no esperado. cliente={Caller}")]
    private static partial void LogUnexpectedCaller(ILogger logger, string caller);
}
