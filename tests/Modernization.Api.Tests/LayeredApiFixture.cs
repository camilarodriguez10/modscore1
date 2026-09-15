namespace Modernization.Api.Tests;

/// <summary>
/// Las dos capas sobre el adaptador Dummy, con un usuario de desarrollo que tiene
/// todos los permisos.
/// </summary>
public sealed class LayeredApiFixture : IDisposable
{
    internal const string ServiceToken = TwoLayerHost.ServiceToken;

    private readonly TwoLayerHost _host = new(new Dictionary<string, string?>
    {
        ["DataSource:Provider"] = "Dummy",
        ["DataSource:ApplicationCount"] = "60",
    });

    /// <summary>Cliente sin autenticar de la capa Channel.</summary>
    public HttpClient CreateChannelClient() => _host.CreateChannelClient();

    /// <summary>Cliente de la capa Channel con sesion ya establecida.</summary>
    public Task<HttpClient> CreateAuthenticatedChannelClientAsync() => _host.CreateAuthenticatedChannelClientAsync();

    /// <summary>Cliente autenticado y con token CSRF cargado.</summary>
    public Task<HttpClient> CreateMutatingClientAsync() => _host.CreateMutatingClientAsync();

    /// <summary>Cliente directo a la capa System, con su token de servicio.</summary>
    public HttpClient CreateSystemClient(bool authenticated = true) => _host.CreateSystemClient(authenticated);

    /// <summary>Cliente de la capa System autenticado, con el alcance indicado (null: sin header).</summary>
    public HttpClient CreateSystemClientWithScope(string? scope) => _host.CreateSystemClient(authenticated: true, scope);

    public void Dispose() => _host.Dispose();
}
