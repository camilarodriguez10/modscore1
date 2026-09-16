"""Fase 15 - opcion C: la capa Channel obtiene un token de vida corta."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


# --------------------------------- credencial: fabrica de clientes, no uno fijo
patch("src/channel/Modernization.ChannelApi/Clients/ServiceCredential.cs", [
    (
        """using System.Text.Json.Serialization;
using Modernization.Security;

namespace Modernization.ChannelApi.Clients;""",
        """using System.Text.Json.Serialization;

namespace Modernization.ChannelApi.Clients;""",
    ),
    (
        """public sealed class ClientCredentialsServiceCredential(
    HttpClient client,
    ClientCredentialsOptions options,
    TimeProvider clock) : IServiceCredential, IDisposable
{
    private readonly SemaphoreSlim _renewing = new(1, 1);""",
        """public sealed class ClientCredentialsServiceCredential(
    IHttpClientFactory clients,
    ClientCredentialsOptions options,
    TimeProvider clock) : IServiceCredential, IDisposable
{
    /// <summary>Nombre del cliente HTTP con que se pide el token.</summary>
    public const string ClientName = "credencial-de-servicio";

    private readonly SemaphoreSlim _renewing = new(1, 1);""",
    ),
    (
        """        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);""",
        """        // Un cliente por peticion: la fabrica rota los handlers y no se queda con una
        // conexion vieja cuando cambia el DNS del emisor.
        using var client = clients.CreateClient(ClientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);""",
    ),
])

# ------------------------------------------------------ cableado de la Channel
patch("src/channel/Modernization.ChannelApi/Program.cs", [
    (
        """builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<UserContextHandler>();""",
        """builder.Services.AddHttpContextAccessor();
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

builder.Services.AddTransient<ServiceCredentialHandler>();""",
    ),
    (
        """builder.Services.AddHttpClient<SystemApiClient>(client => ConfigureSystemClient(client, TimeSpan.FromSeconds(30)))
    .AddHttpMessageHandler<UserContextHandler>()""",
        """builder.Services.AddHttpClient<SystemApiClient>(client => ConfigureSystemClient(client, TimeSpan.FromSeconds(30)))
    .AddHttpMessageHandler<ServiceCredentialHandler>()
    .AddHttpMessageHandler<UserContextHandler>()""",
    ),
    (
        """    client.BaseAddress = new Uri(systemApiUrl);
    client.Timeout = timeout;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));""",
        """    client.BaseAddress = new Uri(systemApiUrl);
    client.Timeout = timeout;
    // La credencial la pone ServiceCredentialHandler en cada llamada: un token de
    // vida corta cambia, y un valor fijado al arrancar se quedaria viejo.
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));""",
    ),
])

# ------------------------------------------- validador en la capa System
patch("src/system/Modernization.SystemApi/Program.cs", [
    (
        """        options =>
        {
            options.Token = builder.Configuration["ServiceToken:Token"] ?? string.Empty;
            options.SecondaryToken = builder.Configuration["ServiceToken:SecondaryToken"];
            options.CallerName = builder.Configuration["ServiceToken:CallerName"] ?? "channel-api";
        });""",
        """        options =>
        {
            options.Mode = jwtServiceToken ? ServiceTokenMode.Jwt : ServiceTokenMode.Static;
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
    builder.Services.AddSingleton<IServiceTokenValidator, JwtServiceTokenValidator>();
}
else
{
    builder.Services.AddSingleton<IServiceTokenValidator, StaticServiceTokenValidator>();
}""",
    ),
    (
        """builder.Services
    .AddAuthentication(ServiceTokenAuthenticationHandler.SchemeName)""",
        """var jwtServiceToken = string.Equals(
    builder.Configuration["ServiceToken:Mode"],
    "Jwt",
    StringComparison.OrdinalIgnoreCase);

builder.Services
    .AddAuthentication(ServiceTokenAuthenticationHandler.SchemeName)""",
    ),
])
