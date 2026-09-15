using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Modernization.Security;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Que puede hacer un usuario, derivado de sus claims.
/// </summary>
/// <remarks>
/// Dos ejes independientes:
/// <list type="bullet">
/// <item><b>Roles</b> (claim <c>roles</c>): <c>portfolio.read</c> para consultar,
/// <c>ingestion.write</c> para publicar cargas y <c>ruleset.admin</c> para editar las
/// reglas de scoring.</item>
/// <item><b>Alcance</b> (claim configurable, <c>portfolio_scope</c> por defecto):
/// que parte del portafolio ve. Lo aplica la capa System sobre los datos.</item>
/// </list>
/// Publicar una carga y administrar reglas exigen ademas alcance completo: las dos
/// cambian el portafolio de toda la organizacion, no solo el tramo que el usuario ve.
/// Son roles separados a proposito: quien sube los datos no deberia poder cambiar
/// tambien como se puntuan.
/// </remarks>
public sealed class ChannelPermissions(string scopeClaimType, TimeSpan? reauthenticationWindow = null)
{
    public const string RoleClaimType = "roles";
    public const string ReadRole = "portfolio.read";
    public const string UploadRole = "ingestion.write";
    public const string RulesAdminRole = "ruleset.admin";

    public const string ReadPolicy = "portfolio.read";
    public const string FullPortfolioPolicy = "portfolio.full";
    public const string UploadPolicy = "ingestion.write";
    public const string RulesAdminPolicy = "ruleset.admin";

    public const string DefaultScopeClaimType = "portfolio_scope";

    /// <summary>
    /// Cuando el usuario se autentico por ultima vez, en segundos Unix. Lo emite solo
    /// esta capa: el que pudiera llegar del IdP con el mismo nombre se descarta.
    /// </summary>
    public const string ReauthenticatedAtClaimType = "modscore_reauth_at";

    /// <summary>Cuanto vale una reautenticacion para publicar reglas.</summary>
    public static readonly TimeSpan DefaultReauthenticationWindow = TimeSpan.FromMinutes(10);

    /// <summary>Tolerancia de reloj con el IdP para una marca de autenticacion.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    public string ScopeClaimType { get; } = scopeClaimType;

    public TimeSpan ReauthenticationWindow { get; } = reauthenticationWindow ?? DefaultReauthenticationWindow;

    public PortfolioScope ScopeOf(ClaimsPrincipal user) => PortfolioScope.FromClaims(user, ScopeClaimType);

    /// <summary>
    /// Subject seudonimo del usuario: <c>sub</c> con OIDC, el identificador del
    /// principal en desarrollo.
    /// </summary>
    public static string SubjectOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return ForwardedUser.Sanitize(
            user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    public static bool HasRole(ClaimsPrincipal user, string role)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.HasClaim(RoleClaimType, role);
    }

    /// <summary>Puede consultar: tiene el rol y algun alcance concedido.</summary>
    public bool CanRead(ClaimsPrincipal user) => HasRole(user, ReadRole) && !ScopeOf(user).IsEmpty;

    public bool HasFullPortfolio(ClaimsPrincipal user) => HasRole(user, ReadRole) && ScopeOf(user).IsFull;

    public bool CanUpload(ClaimsPrincipal user) => HasFullPortfolio(user) && HasRole(user, UploadRole);

    /// <summary>Puede ver, simular y preparar cambios de reglas. Publicar pide ademas reautenticacion.</summary>
    public bool CanAdministerRules(ClaimsPrincipal user) => HasFullPortfolio(user) && HasRole(user, RulesAdminRole);

    /// <summary>Cuando se autentico por ultima vez, si esta capa lo registro.</summary>
    public static DateTimeOffset? ReauthenticatedAt(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirst(ReauthenticatedAtClaimType)?.Value;

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds is > 0 and < 253_402_300_800
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
    }

    /// <summary>
    /// Se autentico dentro de la ventana. Una marca en el futuro, mas alla de la
    /// tolerancia de reloj, no la escribio esta capa y no vale.
    /// </summary>
    public bool IsRecentlyReauthenticated(ClaimsPrincipal user, DateTimeOffset now)
        => ReauthenticatedAt(user) is { } at
            && at <= now + ClockSkew
            && now - at <= ReauthenticationWindow;
}

public static class ChannelAuthorization
{
    public static IServiceCollection AddChannelAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var permissions = new ChannelPermissions(
            configuration["Authorization:ScopeClaimType"] ?? ChannelPermissions.DefaultScopeClaimType,
            TimeSpan.FromMinutes(configuration.GetValue(
                "RulesAdministration:ReauthenticationMinutes",
                (int)ChannelPermissions.DefaultReauthenticationWindow.TotalMinutes)));

        services.AddSingleton(permissions);
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

        services.AddAuthorizationBuilder()
            // Sin politica explicita, al menos autenticado: nada queda abierto por omision.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(ChannelPermissions.ReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => permissions.CanRead(context.User)))
            .AddPolicy(ChannelPermissions.FullPortfolioPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => permissions.HasFullPortfolio(context.User)))
            .AddPolicy(ChannelPermissions.UploadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => permissions.CanUpload(context.User)))
            .AddPolicy(ChannelPermissions.RulesAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => permissions.CanAdministerRules(context.User)));

        return services;
    }
}

/// <summary>
/// Registra cada acceso denegado antes de responder 403. Es la senal que un SIEM
/// usa para detectar a alguien probando recursos que no le corresponden.
/// </summary>
internal sealed class AuditingAuthorizationResultHandler(ILoggerFactory loggers) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    private readonly ILogger _audit = SecurityAudit.CreateLogger(loggers);

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            SecurityAudit.AccessDenied(
                _audit,
                ChannelPermissions.SubjectOf(context.User),
                SecurityAudit.Clean(context.Request.Path.Value));
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
