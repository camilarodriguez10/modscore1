using System.Text.Json.Serialization;
using Modernization.Domain.Code;
using Modernization.Domain.Rules;
using Modernization.Domain.Sources;
using Modernization.Insights;
using Modernization.Security;
using Modernization.Sources.Connectors;
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

// Static (hoy), Jwt (con APIM) o Both, que es la ventana del cambio (ADR 0022).
var serviceTokenMode = Enum.TryParse<ServiceTokenMode>(
    builder.Configuration["ServiceToken:Mode"],
    ignoreCase: true,
    out var configuredMode)
        ? configuredMode
        : ServiceTokenMode.Static;

var jwtServiceToken = serviceTokenMode is ServiceTokenMode.Jwt or ServiceTokenMode.Both;

builder.Services
    .AddAuthentication(ServiceTokenAuthenticationHandler.SchemeName)
    .AddScheme<ServiceTokenOptions, ServiceTokenAuthenticationHandler>(
        ServiceTokenAuthenticationHandler.SchemeName,
        options =>
        {
            options.Mode = serviceTokenMode;
            options.Token = builder.Configuration["ServiceToken:Token"] ?? string.Empty;
            options.SecondaryToken = builder.Configuration["ServiceToken:SecondaryToken"];
            options.CallerName = builder.Configuration["ServiceToken:CallerName"] ?? "channel-api";
        });

// Quien decide si el token vale: el secreto compartido de hoy, o el token de vida
// corta que emite APIM (ADR 0020, opcion C). Un solo esquema, dos validadores.
if (jwtServiceToken)
{
    var jwt = new JwtServiceTokenOptions
    {
        Authority = builder.Configuration["ServiceToken:Jwt:Authority"],
        Issuer = builder.Configuration["ServiceToken:Jwt:Issuer"] ?? string.Empty,
        Audience = builder.Configuration["ServiceToken:Jwt:Audience"] ?? string.Empty,
    };

    foreach (var caller in builder.Configuration.GetSection("ServiceToken:Jwt:AllowedCallers").Get<string[]>() ?? [])
    {
        jwt.AllowedCallers.Add(caller);
    }

    builder.Services.AddSingleton(jwt);
    builder.Services.AddSingleton<JwtServiceTokenValidator>();

    if (serviceTokenMode == ServiceTokenMode.Both)
    {
        builder.Services.AddSingleton<StaticServiceTokenValidator>();
        builder.Services.AddSingleton<IServiceTokenValidator, EitherServiceTokenValidator>();
    }
    else
    {
        builder.Services.AddSingleton<IServiceTokenValidator>(
            sp => sp.GetRequiredService<JwtServiceTokenValidator>());
    }
}
else
{
    builder.Services.AddSingleton<IServiceTokenValidator, StaticServiceTokenValidator>();
}

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
        // Retencion declarativa, igual que en el adaptador de base: 0 conserva todas.
        builder.Services.AddSingleton(_ => new UploadService(
            dataDirectory,
            retainVersions: builder.Configuration.GetValue("Uploads:RetainVersions", 0)));
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

// ------------------------------------------------------------------ codigo
//
// El inventario de repositorios de la pantalla Code (ADR 0026). Hasta que exista el
// recolector de GitHub Enterprise hay dos opciones: datos de demostracion o nada. Los de
// demostracion solo existen en Development: fuera de ahi, un dato inventado se leeria
// como real, y el proceso no arranca.

var codeProvider = builder.Configuration["CodeInventory:Provider"]
    ?? (builder.Environment.IsDevelopment() ? SampleCodeInventoryProvider.Name : NoCodeInventoryProvider.Name);

switch (codeProvider)
{
    case SampleCodeInventoryProvider.Name when builder.Environment.IsDevelopment():
        builder.Services.AddSingleton<ICodeInventoryProvider, SampleCodeInventoryProvider>();
        break;

    case SampleCodeInventoryProvider.Name:
        throw new InvalidOperationException(
            $"'CodeInventory:Provider' = '{SampleCodeInventoryProvider.Name}' solo se admite en Development "
            + $"(entorno '{builder.Environment.EnvironmentName}'): son repositorios de demostracion.");

    case NoCodeInventoryProvider.Name:
        builder.Services.AddSingleton<ICodeInventoryProvider, NoCodeInventoryProvider>();
        break;

    default:
        throw new InvalidOperationException(
            $"'CodeInventory:Provider' tiene el valor '{codeProvider}'. Valores validos: "
            + $"{SampleCodeInventoryProvider.Name} (solo Development), {NoCodeInventoryProvider.Name}.");
}

// Las fechas de fin de vida: la foto de endoflife.date que viaja con el despliegue.
builder.Services.AddSingleton(_ => EndOfLifeCatalog.Default);

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

// ---------------------------------------------------------------- conectores
//
// Fuentes leídas directamente de las bases de la organización, sin archivos (ADR 0037).
// Apagado por defecto. Una configuración que no puede funcionar detiene el arranque con
// todos sus problemas; la guía está en docs/database-connections.md.

var connectorOptions = builder.Configuration.GetSection(ConnectorOptions.SectionName).Get<ConnectorOptions>()
    ?? new ConnectorOptions();
var connectorProblems = ConnectorConfiguration.Validate(connectorOptions, SourceSchemaCatalog.Default).ToList();

if (connectorOptions.Enabled && provider == "Dummy")
{
    connectorProblems.Add("Connectors need a data adapter that accepts uploads: set DataSource:Provider to File or Database.");
}

if (connectorProblems.Count > 0)
{
    throw new InvalidOperationException(
        "La seccion 'Connectors' no sirve:" + Environment.NewLine
        + string.Join(Environment.NewLine, connectorProblems.Select(p => " - " + p)));
}

builder.Services.AddSingleton(connectorOptions);
builder.Services.AddSingleton<IConnectorConnectionFactory>(_ => new SecureConnectionFactory(
    builder.Environment.IsDevelopment(),
    connectorOptions.ConnectTimeoutSeconds));
builder.Services.AddSingleton<DatabaseSourcePuller>();
builder.Services.AddSingleton(sp => new ConnectorPullService(
    sp.GetRequiredService<DatabaseSourcePuller>(),
    sp.GetService<ISnapshotPublisher>(),
    sp.GetRequiredService<PortfolioService>(),
    sp.GetRequiredService<PortfolioWarmup>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<ILoggerFactory>()));

if (connectorOptions.Enabled && connectorOptions.PullInterval is not null)
{
    builder.Services.AddHostedService<ConnectorSchedule>();
}

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
app.MapDashboardEndpoints();
app.MapCodeEndpoints();
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
