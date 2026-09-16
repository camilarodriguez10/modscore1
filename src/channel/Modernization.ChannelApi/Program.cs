using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;
using Modernization.ChannelApi;
using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.Composition;
using Modernization.ChannelApi.Endpoints;
using Modernization.Security;

var builder = WebApplication.CreateBuilder(args);

// Secretos montados como archivos por el CSI driver del gestor de secretos, un
// archivo por clave ("ServiceToken__Token"). Nunca variables de entorno: se
// filtran a volcados de proceso y a la consola del orquestador (plan secc. 7.2).
if (builder.Configuration["Secrets:Directory"] is { Length: > 0 } secretsDirectory)
{
    builder.Configuration.AddKeyPerFile(secretsDirectory, optional: false);
}

// -------------------------------------------------------------------- borde

builder.WebHost.ConfigureKestrel(options =>
{
    // Sin banner: no se anuncia la tecnologia del servidor (ASVS 14.3.3).
    options.AddServerHeader = false;

    // Todo cuerpo esta acotado a 1 MB. La carga de archivos amplia el limite en su
    // propio endpoint, de forma explicita; nada mas lo necesita.
    options.Limits.MaxRequestBodySize = ChannelLimits.DefaultMaxRequestBodyBytes;
});

// Detras del ingress, la IP y el esquema reales llegan en X-Forwarded-*. Solo se
// aceptan de los proxies declarados: de cualquier otro origen, un cliente podria
// inventarse la IP y esquivar el rate limiting.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

// HSTS de un ano, con subdominios y preload (plan secc. 7.2). Solo fuera de Development.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// ------------------------------------------------------------ autenticacion
//
// Patron BFF: la identidad vive del lado servidor y el navegador solo ve una
// cookie httpOnly. Sin tokens en localStorage no hay robo de tokens por XSS.

builder.Services.AddChannelAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddChannelAuthorization(builder.Configuration);

// Las llaves de Data Protection cifran la cookie y el ticket de sesion. Con mas de
// una replica, o para sobrevivir a un reinicio, se persisten fuera del contenedor
// (runbook de rotacion de secretos).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("modernization-score-channel");
if (builder.Configuration["DataProtection:KeysDirectory"] is { Length: > 0 } keysDirectory)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
}

// ------------------------------------------------------------ rate limiting

var rateLimits = RateLimits.From(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Por usuario cuando hay sesion; por IP cuando aun no la hay.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.RequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Cada simulacion recalcula el score varias veces: cuesta mas que una lectura.
    options.AddPolicy(ChannelLimits.SimulationPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.SimulationsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Una carga mueve cientos de megas y reemplaza el portafolio: pocas, y en una
    // ventana deslizante para que no se puedan concentrar en el borde de un minuto.
    options.AddPolicy(ChannelLimits.UploadPolicy, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            PartitionKey(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.UploadsPerWindow,
                Window = rateLimits.UploadWindow,
                SegmentsPerWindow = 5,
                QueueLimit = 0,
            }));

    // Simular un cambio de reglas recalcula todo el portafolio con dos juegos de
    // reglas y proyecta cada plan: es la operacion de lectura mas cara del sistema.
    options.AddPolicy(ChannelLimits.RulesSimulationPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.RulesSimulationsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Publicar reglas mueve el score de toda la organizacion: pocas por hora, en
    // ventana deslizante, igual que la carga.
    options.AddPolicy(ChannelLimits.RulesPublishPolicy, context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            PartitionKey(context),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.RulesPublishesPerWindow,
                Window = rateLimits.RulesPublishWindow,
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            }));

    // Exportar lee el portafolio completo de una vez: pocas por minuto.
    options.AddPolicy(ChannelLimits.ExportPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimits.ExportsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        SecurityAudit.RateLimited(
            SecurityAudit.CreateLogger(context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()),
            PartitionKey(context.HttpContext),
            SecurityAudit.Clean(context.HttpContext.Request.Path.Value));

        return ValueTask.CompletedTask;
    };
});

// -------------------------------------------------------------- capa System
//
// Composicion, no proxy: la capa Channel llama a la System y arma la respuesta
// de cada pantalla.

var systemApiUrl = builder.Configuration["SystemApi:BaseUrl"]
    ?? throw new InvalidOperationException("Falta la configuracion 'SystemApi:BaseUrl'.");

var serviceToken = builder.Configuration["SystemApi:ServiceToken"]
    ?? throw new InvalidOperationException(
        "Falta 'SystemApi:ServiceToken'. En despliegue viene de Key Vault via CSI driver.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<UserContextHandler>();

// Con que credencial se llama a la capa System (ADR 0020, opcion C). Por defecto el
// secreto compartido; con APIM, un token de vida corta pedido con client_credentials.
if (string.Equals(
        builder.Configuration["SystemApi:Credential:Mode"],
        "ClientCredentials",
        StringComparison.OrdinalIgnoreCase))
{
    var credential = new ClientCredentialsOptions
    {
        TokenEndpoint = builder.Configuration["SystemApi:Credential:TokenEndpoint"] ?? string.Empty,
        ClientId = builder.Configuration["SystemApi:Credential:ClientId"] ?? string.Empty,
        ClientSecret = builder.Configuration["SystemApi:Credential:ClientSecret"] ?? string.Empty,
        Scope = builder.Configuration["SystemApi:Credential:Scope"] ?? string.Empty,
    };

    credential.Validate();

    builder.Services.AddHttpClient(
        ClientCredentialsServiceCredential.ClientName,
        client => client.Timeout = TimeSpan.FromSeconds(10));

    builder.Services.AddSingleton(credential);
    builder.Services.AddSingleton<IServiceCredential, ClientCredentialsServiceCredential>();
}
else
{
    builder.Services.AddSingleton<IServiceCredential>(new StaticServiceCredential(serviceToken));
}

builder.Services.AddTransient<ServiceCredentialHandler>();

builder.Services.AddHttpClient<SystemApiClient>(client => ConfigureSystemClient(client, TimeSpan.FromSeconds(30)))
    .AddHttpMessageHandler<ServiceCredentialHandler>()
    .AddHttpMessageHandler<UserContextHandler>()
    // Nunca se reintenta un POST: una simulacion es inocua, pero la regla no puede
    // depender de recordar cual es cual.
    .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

// La carga va por un cliente propio: sin reintentos, que publicarian dos versiones,
// y con el tiempo que necesita un archivo grande. Con el estandar, cualquier
// intento de mas de 10 segundos se cancelaba.
builder.Services.AddHttpClient(
        SystemApiClient.UploadClientName,
        client => ConfigureSystemClient(client, ChannelLimits.UploadTimeout))
    .AddHttpMessageHandler<ServiceCredentialHandler>()
    .AddHttpMessageHandler<UserContextHandler>();

builder.Services.AddScoped<ScreenComposer>();
builder.Services.AddScoped<WhatIfComposer>();
builder.Services.AddScoped<RoadmapComposer>();
builder.Services.AddScoped<IngestionComposer>();
builder.Services.AddScoped<RulesComposer>();
builder.Services.AddScoped<ExportComposer>();

// Los nulos se serializan explicitamente. El modelo de vista es el contrato del
// front: omitir una propiedad nula hace que el cliente reciba `undefined` donde su
// tipo dice `null`, y esa diferencia rompe las comprobaciones en tiempo de render.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

// CORS cerrado: la SPA se sirve desde el mismo origen que este API.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST")
        .WithHeaders("Content-Type", CsrfProtection.HeaderName)
        .AllowCredentials()));
}

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ChannelLimits.UploadMaxTotalBytes;
    options.ValueLengthLimit = 8 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});

builder.Services.AddProblemDetails();

// Las respuestas de la capa System con significado para el usuario: falta la
// primera carga, o el recurso esta fuera de su alcance.
builder.Services.AddExceptionHandler<SystemApiErrorHandler>();

// El mapa completo del API no se publica fuera de Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

DeploymentGuards.Validate(builder.Configuration, builder.Environment, "SystemApi:ServiceToken");

var app = builder.Build();
var audit = SecurityAudit.CreateLogger(app.Services.GetRequiredService<ILoggerFactory>());

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders(apiOnly: false);

var spaHosted = SpaHosting.UseStaticAssets(app);

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

// Autenticar antes de limitar: el limite es por usuario, y sin identidad todo caeria
// en el balde de la IP, que detras de un proxy es el mismo para todos.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Doble envio CSRF en toda mutacion. La cookie de sesion ya es SameSite=Strict;
// esto es la segunda capa.
app.Use(async (context, next) =>
{
    if (!CsrfProtection.IsValid(context))
    {
        SecurityAudit.CsrfRejected(
            audit,
            ChannelPermissions.SubjectOf(context.User),
            SecurityAudit.Clean(context.Request.Path.Value));

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "csrf",
                "Missing or invalid CSRF token",
                $"Send the value of the '{CsrfProtection.CookieName}' cookie in the '{CsrfProtection.HeaderName}' header.")
            .ExecuteAsync(context)
            .ConfigureAwait(false);
        return;
    }

    await next().ConfigureAwait(false);
});

// Una entrada rechazada por validacion tambien es informacion de seguridad (ASVS 7.1.3).
// Se audita aqui, y no endpoint por endpoint, para que ninguno se olvide: cualquier
// respuesta de /api que rechace la entrada deja el evento 4214 con su ruta y su codigo.
app.Use(async (context, next) =>
{
    await next().ConfigureAwait(false);

    if (SecurityAudit.IsValidationRejection(context.Response.StatusCode)
        && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        SecurityAudit.ValidationRejected(
            audit,
            ChannelPermissions.SubjectOf(context.User),
            context.Request.Path.Value,
            context.Response.StatusCode);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous().ExcludeFromDescription();
app.MapChannelEndpoints();
app.MapRulesAdminEndpoints();
app.MapExportEndpoints();

if (spaHosted)
{
    SpaHosting.MapFallback(app);
}

await app.RunAsync();

void ConfigureSystemClient(HttpClient client, TimeSpan timeout)
{
    client.BaseAddress = new Uri(systemApiUrl);
    client.Timeout = timeout;
    // La credencial la pone ServiceCredentialHandler en cada llamada: un token de
    // vida corta cambia, y un valor fijado al arrancar se quedaria viejo.
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}

static string PartitionKey(HttpContext context)
    => context.User.Identity?.IsAuthenticated == true
        ? "usuario:" + ChannelPermissions.SubjectOf(context.User)
        : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "desconocida");

namespace Modernization.ChannelApi
{
    /// <summary>Marcador del ensamblado para los tests de integracion.</summary>
    public sealed class ChannelApiMarker;
}
