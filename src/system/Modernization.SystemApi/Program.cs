using System.Text.Json.Serialization;
using Modernization.Domain.Rules;
using Modernization.Domain.Sources;
using Modernization.Security;
using Modernization.Sources.Dummy;
using Modernization.Sources.Database;
using Modernization.Sources.Files;
using Modernization.SystemApi;
using Modernization.SystemApi.Endpoints;
using Modernization.SystemApi.Services;

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

    // Todo cuerpo esta acotado a 1 MB; la carga amplia el suyo en su endpoint.
    options.Limits.MaxRequestBodySize = SystemLimits.DefaultMaxRequestBodyBytes;
});

// ---------------------------------------------------------------- seguridad
//
// La capa System no es alcanzable desde internet: solo la llama la capa Channel.
// El token de servicio es el control de aplicacion; mTLS es el de transporte y
// lo impone el service mesh (ADR 0001).

builder.Services
    .AddAuthentication(ServiceTokenAuthenticationHandler.SchemeName)
    .AddScheme<ServiceTokenOptions, ServiceTokenAuthenticationHandler>(
        ServiceTokenAuthenticationHandler.SchemeName,
        options =>
        {
            options.Token = builder.Configuration["ServiceToken:Token"] ?? string.Empty;
            options.SecondaryToken = builder.Configuration["ServiceToken:SecondaryToken"];
            options.CallerName = builder.Configuration["ServiceToken:CallerName"] ?? "channel-api";
        });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Sin datos del portafolio en las respuestas de error.
builder.Services.AddProblemDetails();

// "Todavia no hay datos" es un estado, no una falla: se responde 409 para que la
// interfaz ofrezca la carga en vez de una pantalla de error.
builder.Services.AddExceptionHandler<NoDataExceptionHandler>();

// ------------------------------------------------------------------- fuentes
//
// Un solo punto de decision para las fases 07 y 09: cambiar "DataSource:Provider"
// intercambia el adaptador sin tocar nada aguas arriba. Las reglas se guardan con el
// mismo adaptador que los datos (Fase 12).

var provider = builder.Configuration["DataSource:Provider"] ?? "Dummy";
switch (provider)
{
    case "Dummy":
        var seed = builder.Configuration.GetValue("DataSource:Seed", DummyPortfolio.DefaultSeed);
        var count = builder.Configuration.GetValue("DataSource:ApplicationCount", DummyPortfolio.DefaultApplicationCount);
        builder.Services.AddSingleton<IApplicationSnapshotProvider>(_ => new DummySnapshotProvider(seed, count));

        // Datos generados, reglas en memoria: los dos se pierden al reiniciar.
        builder.Services.AddSingleton<IRulesStore, InMemoryRulesStore>();
        break;

    case "File":
        var dataDirectory = builder.Configuration["DataSource:Directory"]
            ?? throw new InvalidOperationException(
                "El adaptador de archivos necesita 'DataSource:Directory'.");

        builder.Services.AddSingleton<IApplicationSnapshotProvider>(
            _ => new FileSnapshotProvider(dataDirectory));
        builder.Services.AddSingleton(_ => new UploadService(dataDirectory));
        builder.Services.AddSingleton<ISnapshotPublisher>(sp => sp.GetRequiredService<UploadService>());

        // Por defecto en el mismo volumen que las cargas: persisten juntas.
        builder.Services.AddSingleton<IRulesStore>(_ => new FileRulesStore(
            builder.Configuration["Rules:Directory"]
                ?? Path.Combine(dataDirectory, FileRulesStore.DefaultDirectoryName)));
        break;

    case "Database":
        // La cadena de conexion es un secreto: llega como archivo del CSI driver,
        // igual que el token de servicio.
        var connectionString = builder.Configuration.GetConnectionString("Modernization")
            ?? throw new InvalidOperationException(
                "El adaptador de base de datos necesita 'ConnectionStrings:Modernization'.");

        builder.Services.AddDatabaseSnapshotAdapter(new DatabaseAdapterOptions(
            builder.Configuration["Database:Provider"] ?? DatabaseAdapter.Sqlite,
            connectionString,
            builder.Configuration.GetValue("Database:RetainVersions", 0)));
        break;

    default:
        throw new InvalidOperationException(
            $"'DataSource:Provider' tiene el valor '{provider}'. Valores validos: Dummy, File, Database.");
}

builder.Services.AddSingleton(TimeProvider.System);

// Las reglas vigentes: el motor de scoring, el what-if y el roadmap salen de aqui en
// cada peticion. Publicar desde el portal las cambia sin redesplegar, y las demas
// replicas las toman en la siguiente comprobacion (ADR 0018).
builder.Services.AddSingleton(sp => new RulesService(
    sp.GetRequiredService<IRulesStore>(),
    sp.GetRequiredService<TimeProvider>(),
    TimeSpan.FromSeconds(builder.Configuration.GetValue(
        "Rules:RefreshSeconds",
        (int)RulesService.DefaultRefreshInterval.TotalSeconds)),
    sp.GetRequiredService<ILogger<RulesService>>()));

builder.Services.AddSingleton<PortfolioService>();

// Scores e historia calculados fuera de las peticiones: al arrancar, tras publicar y
// periodicamente, que es como otra replica toma reglas nuevas ya calientes.
builder.Services.AddSingleton(new PortfolioWarmupOptions(TimeSpan.FromSeconds(builder.Configuration.GetValue(
    "Portfolio:WarmupSeconds",
    (int)PortfolioWarmupOptions.DefaultInterval.TotalSeconds))));
builder.Services.AddSingleton<PortfolioWarmup>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PortfolioWarmup>());
builder.Services.AddSingleton(_ => SourceSchemaCatalog.Default);

// La carga solo existe con el adaptador de archivos. Con Dummy el servicio se
// registra igual pero responde que no admite cargas, en lugar de fallar al
// resolver dependencias.
builder.Services.AddSingleton(sp => new IngestionService(
    sp.GetService<ISnapshotPublisher>(),
    sp.GetRequiredService<SourceSchemaCatalog>(),
    sp.GetRequiredService<TimeProvider>()));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Limite multipart de la carga. El de Kestrel lo amplia el endpoint.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadService.MaxTotalBytes;
    options.ValueLengthLimit = 8 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});

// El mapa completo del API no se publica fuera de Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

DeploymentGuards.Validate(
    builder.Configuration,
    builder.Environment,
    "ServiceToken:Token",
    "ServiceToken:SecondaryToken");

var app = builder.Build();

// Una carga interrumpida por un reinicio deja su directorio de staging. Se limpia
// al arrancar: sin esto, cada interrupcion deja hasta 256 MB huerfanos.
app.Services.GetService<UploadService>()?.CleanupStaleStaging(
    app.Services.GetRequiredService<TimeProvider>().GetUtcNow(),
    TimeSpan.FromHours(1));

// Con base de datos, el esquema se pone al dia antes de atender la primera
// peticion. Con varias replicas esto lo hace un job de despliegue, no cada proceso.
if (provider == "Database" && builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DatabaseAdapter.MigrateAsync(app.Services).ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseSecurityHeaders(apiOnly: true);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous().ExcludeFromDescription();
app.MapSystemEndpoints();
app.MapWhatIfEndpoints();
app.MapRoadmapEndpoints();
app.MapIngestionEndpoints();
app.MapRulesEndpoints();

await app.RunAsync();

namespace Modernization.SystemApi
{
    /// <summary>
    /// Marcador del ensamblado para los tests de integracion. Las dos capas
    /// declaran Program en el namespace global, asi que cada una necesita un tipo
    /// distinguible para WebApplicationFactory.
    /// </summary>
    public sealed class SystemApiMarker;
}
