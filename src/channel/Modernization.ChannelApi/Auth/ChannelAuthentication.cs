using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Modernization.Security;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Autenticacion del BFF.
/// </summary>
/// <remarks>
/// Patron BFF: la identidad vive del lado servidor y el navegador solo recibe una
/// cookie httpOnly con una clave de sesion. Eso elimina de raiz el robo de tokens
/// por XSS, que es el vector mas comun contra una SPA que guarda el token en
/// localStorage.
/// </remarks>
public static class ChannelAuthentication
{
    public const string SessionCookieName = "__Host-modscore.session";

    /// <summary>
    /// True cuando el esquema de desarrollo esta activo. Program lo usa para
    /// decidir si el endpoint de login firma la sesion localmente o inicia OIDC.
    /// </summary>
    public static bool UsesDevelopmentScheme { get; private set; }

    public static IServiceCollection AddChannelAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var useDevelopmentAuth = configuration.GetValue("Authentication:UseDevelopmentScheme", false);

        if (useDevelopmentAuth && !environment.IsDevelopment())
        {
            // Guardia dura: el esquema de desarrollo no autentica a nadie de verdad.
            // Si llegara a un entorno real, cualquiera entraria. Preferimos que el
            // proceso no arranque.
            throw new InvalidOperationException(
                "'Authentication:UseDevelopmentScheme' solo puede activarse en el entorno Development. " +
                $"El entorno actual es '{environment.EnvironmentName}'.");
        }

        UsesDevelopmentScheme = useDevelopmentAuth;

        var lifetime = new SessionLifetime(
            TimeSpan.FromMinutes(configuration.GetValue("Session:IdleTimeoutMinutes", 30)),
            TimeSpan.FromHours(configuration.GetValue("Session:AbsoluteTimeoutHours", 12)));

        services.AddSingleton(lifetime);
        services.TryAddSingleton(TimeProvider.System);

        // Sesiones del lado servidor (ADR 0011). El cache en memoria sirve para un
        // nodo; con varias replicas se registra un IDistributedCache compartido.
        services.AddDistributedMemoryCache();
        services.TryAddSingleton<ITicketStore, DistributedTicketStore>();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore, TimeProvider>((options, store, clock) =>
            {
                options.SessionStore = store;
                options.TimeProvider = clock;
            });

        // El challenge por defecto apunta a la cookie incluso en desarrollo: asi un
        // request no autenticado recibe 401 en lugar de autenticarse solo.
        // Firmar la sesion es un acto explicito del endpoint de login, nunca un
        // efecto de fallar la autorizacion.
        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = useDevelopmentAuth
                ? CookieAuthenticationDefaults.AuthenticationScheme
                : OpenIdConnectDefaults.AuthenticationScheme;
        });

        authentication.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // Strict, no Lax: ademas de cortar CSRF, impide fijar la sesion. El
            // callback de OIDC es cross-site, asi que una cookie plantada no viaja en
            // el y el inicio de sesion siempre emite una clave nueva (ADR 0011).
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.IsEssential = true;

            // Inactividad: la sesion se renueva con el uso y vence tras el periodo
            // sin actividad. El tope absoluto lo impone OnValidatePrincipal.
            options.ExpireTimeSpan = lifetime.Idle;
            options.SlidingExpiration = true;

            // Un API responde 401 y 403; nunca redirige a una pagina de login.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };

            options.Events.OnSigningIn = context =>
            {
                var now = Clock(context.HttpContext).GetUtcNow();
                SessionLifetime.MarkStarted(context.Properties, now);

                // En desarrollo, firmar la sesion es haberse autenticado en ese
                // instante: es lo que con OIDC dice el auth_time del token.
                if (useDevelopmentAuth && context.Principal?.Identity is ClaimsIdentity identity)
                {
                    StepUp.Stamp(identity, now);
                }

                return Task.CompletedTask;
            };

            options.Events.OnSignedIn = context =>
            {
                if (context.Principal is not null)
                {
                    SecurityAudit.SessionStarted(Audit(context.HttpContext), ChannelPermissions.SubjectOf(context.Principal));
                }

                return Task.CompletedTask;
            };

            options.Events.OnValidatePrincipal = EnforceAbsoluteLifetimeAsync;
        });

        if (useDevelopmentAuth)
        {
            services.TryAddSingleton<IReauthenticationFlow, DevelopmentReauthentication>();
            return services;
        }

        // Punto de reemplazo cuando la autenticacion pase a OAuth mediante APIM (ADR 0020).
        services.TryAddSingleton<IReauthenticationFlow, OidcReauthentication>();

        authentication.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = Required(configuration, "Authentication:Oidc:Authority");
            options.ClientId = Required(configuration, "Authentication:Oidc:ClientId");
            options.ClientSecret = Required(configuration, "Authentication:Oidc:ClientSecret");

            // Authorization Code + PKCE. Nunca flujo implicito.
            options.ResponseType = "code";
            options.UsePkce = true;
            options.RequireHttpsMetadata = true;

            // La capa Channel no llama APIs en nombre del usuario: llama a la System
            // con su token de servicio. Guardar los tokens del IdP seria retener
            // credenciales que nadie usa.
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                RoleClaimType = ChannelPermissions.RoleClaimType,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            // Reautenticacion (ASVS 3.7.1): max_age=0 obliga al IdP a pedir las
            // credenciales aunque tenga su propia sesion abierta.
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (context.Properties.Items.ContainsKey(StepUp.PropertyKey))
                {
                    context.ProtocolMessage.MaxAge = "0";
                    context.ProtocolMessage.Prompt = "login";
                }

                return Task.CompletedTask;
            };

            // La marca de reautenticacion sale del auth_time del token, y solo si es
            // reciente. Un IdP que ignorara max_age y devolviera un token viejo no la
            // concede.
            options.Events.OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity
                    && StepUp.StampFromIdentityProvider(identity, Clock(context.HttpContext).GetUtcNow())
                    && context.Properties?.Items.ContainsKey(StepUp.PropertyKey) == true)
                {
                    SecurityAudit.StepUpCompleted(Audit(context.HttpContext), ChannelPermissions.SubjectOf(context.Principal));
                }

                return Task.CompletedTask;
            };
        });

        return services;
    }

    /// <summary>
    /// Identidad fija de desarrollo. No autentica a nadie: solo permite trabajar
    /// sin un proveedor OIDC levantado. Solo se alcanza cuando el entorno es
    /// Development y la configuracion lo pide, y esa combinacion se valida arriba.
    /// </summary>
    /// <remarks>
    /// Por defecto tiene todos los permisos, para trabajar en local. Los roles, el
    /// alcance y el subject se pueden fijar por configuracion para reproducir a un
    /// usuario limitado.
    /// </remarks>
    public static ClaimsPrincipal CreateDevelopmentPrincipal(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var roles = Values(configuration, "Authentication:Development:Roles")
            ?? [ChannelPermissions.ReadRole, ChannelPermissions.UploadRole, ChannelPermissions.RulesAdminRole];
        var scopes = Values(configuration, "Authentication:Development:Scopes")
            ?? [PortfolioScope.FullValue];
        var scopeClaim = configuration["Authorization:ScopeClaimType"] ?? ChannelPermissions.DefaultScopeClaimType;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, configuration["Authentication:Development:Subject"] ?? "dev-user"),
            new("name", "Development user"),
            new("email", "dev@localhost"),
        };

        claims.AddRange(roles.Select(role => new Claim(ChannelPermissions.RoleClaimType, role)));
        claims.AddRange(scopes.Select(scope => new Claim(scopeClaim, scope)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Development"));
    }

    /// <summary>
    /// Tope absoluto de la sesion: aun con uso continuo, vence a las 12 horas de
    /// iniciada (ASVS 3.3.2). Un ticket sin marca de inicio no lo emitio este
    /// codigo, y se descarta.
    /// </summary>
    private static async Task EnforceAbsoluteLifetimeAsync(CookieValidatePrincipalContext context)
    {
        var limits = context.HttpContext.RequestServices.GetRequiredService<SessionLifetime>();
        var startedAt = SessionLifetime.StartedAt(context.Properties);

        if (startedAt is not null && Clock(context.HttpContext).GetUtcNow() - startedAt.Value <= limits.Absolute)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext
            .SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        if (startedAt is not null && context.Principal is not null)
        {
            SecurityAudit.SessionExpired(
                Audit(context.HttpContext),
                ChannelPermissions.SubjectOf(context.Principal),
                startedAt.Value);
        }
    }

    private static string[]? Values(IConfiguration configuration, string key)
    {
        var values = configuration.GetSection(key).Get<string[]>();
        return values is null ? null : [.. values.Where(v => !string.IsNullOrWhiteSpace(v))];
    }

    private static TimeProvider Clock(HttpContext context)
        => context.RequestServices.GetRequiredService<TimeProvider>();

    private static ILogger Audit(HttpContext context)
        => SecurityAudit.CreateLogger(context.RequestServices.GetRequiredService<ILoggerFactory>());

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] ?? throw new InvalidOperationException(
            $"Falta la configuracion '{key}'. En despliegue viene de Key Vault; " +
            "en local, del gestor de secretos de usuario.");
}
