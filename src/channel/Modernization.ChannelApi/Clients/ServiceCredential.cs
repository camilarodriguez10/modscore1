using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Modernization.ChannelApi.Clients;

/// <summary>
/// La credencial con que la capa Channel llama a la System.
/// </summary>
/// <remarks>
/// Detras de la interfaz hay dos mundos: el secreto compartido de hoy, que no
/// vence, y el token de vida corta que emitira APIM o el IdP
/// ([ADR 0020](../../../../docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md), opcion C).
/// El resto del codigo no sabe cual esta configurado.
/// </remarks>
public interface IServiceCredential
{
    ValueTask<string> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Secreto compartido, leido del gestor de secretos al arrancar.</summary>
public sealed class StaticServiceCredential(string token) : IServiceCredential
{
    public ValueTask<string> GetAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(token);
}

/// <summary>Configuracion del flujo client_credentials contra APIM o el IdP.</summary>
public sealed class ClientCredentialsOptions
{
    public string TokenEndpoint { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Viene como archivo del gestor de secretos, nunca de una variable de entorno.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Alcance o recurso que identifica a la capa System como audiencia.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Cuanto antes del vencimiento se pide uno nuevo.</summary>
    public TimeSpan RenewBefore { get; set; } = TimeSpan.FromSeconds(60);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TokenEndpoint) || !Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("'SystemApi:Credential:TokenEndpoint' debe ser una URL absoluta.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && !uri.IsLoopback)
        {
            throw new InvalidOperationException(
                "'SystemApi:Credential:TokenEndpoint' tiene que ser https fuera de localhost.");
        }

        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException(
                "El flujo client_credentials necesita 'SystemApi:Credential:ClientId' y ':ClientSecret'.");
        }
    }
}

/// <summary>
/// Pide un token de vida corta con client_credentials y lo reutiliza hasta poco
/// antes de que venza.
/// </summary>
/// <remarks>
/// Una sola peticion en vuelo: si veinte llamadas coinciden con el vencimiento, una
/// pide el token y las demas esperan ese mismo. El token no se registra en ningun
/// log, ni entero ni en parte.
/// </remarks>
public sealed class ClientCredentialsServiceCredential(
    IHttpClientFactory clients,
    ClientCredentialsOptions options,
    TimeProvider clock) : IServiceCredential, IDisposable
{
    /// <summary>Nombre del cliente HTTP con que se pide el token.</summary>
    public const string ClientName = "credencial-de-servicio";

    private readonly SemaphoreSlim _renewing = new(1, 1);
    private string? _token;
    private DateTimeOffset _renewAt = DateTimeOffset.MinValue;

    public void Dispose() => _renewing.Dispose();

    public async ValueTask<string> GetAsync(CancellationToken cancellationToken = default)
    {
        var current = _token;

        if (current is not null && clock.GetUtcNow() < _renewAt)
        {
            return current;
        }

        await _renewing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Otra llamada pudo renovarlo mientras esta esperaba.
            if (_token is { } fresh && clock.GetUtcNow() < _renewAt)
            {
                return fresh;
            }

            var (token, expiresIn) = await RequestAsync(cancellationToken).ConfigureAwait(false);

            _token = token;
            _renewAt = clock.GetUtcNow() + expiresIn - options.RenewBefore;

            return token;
        }
        finally
        {
            _renewing.Release();
        }
    }

    private async Task<(string Token, TimeSpan ExpiresIn)> RequestAsync(CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
        };

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            form["scope"] = options.Scope;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };

        // Un cliente por peticion: la fabrica rota los handlers y no se queda con una
        // conexion vieja cuando cambia el DNS del emisor.
        using var client = clients.CreateClient(ClientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Sin cuerpo en el mensaje: el error del emisor puede traer detalle del cliente.
            throw new ServiceCredentialException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"El emisor de tokens respondio {(int)response.StatusCode}."));
        }

        var payload = await response.Content
            .ReadFromJsonAsync<TokenResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new ServiceCredentialException("El emisor de tokens no devolvio un access_token.");
        }

        var seconds = payload.ExpiresIn > 0 ? payload.ExpiresIn : 300;

        return (payload.AccessToken, TimeSpan.FromSeconds(seconds));
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>No se pudo obtener la credencial de servicio.</summary>
public sealed class ServiceCredentialException : Exception
{
    public ServiceCredentialException(string message) : base(message)
    {
    }

    public ServiceCredentialException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ServiceCredentialException()
    {
    }
}

/// <summary>
/// Pone la credencial vigente en cada llamada a la capa System.
/// </summary>
/// <remarks>
/// Va por peticion y no en las cabeceras por defecto del cliente: un token de vida
/// corta cambia, y un valor fijado al arrancar se quedaria viejo.
/// </remarks>
public sealed class ServiceCredentialHandler(IServiceCredential credential) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await credential.GetAsync(cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
