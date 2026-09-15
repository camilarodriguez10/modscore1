using System.Security.Claims;
using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Composition;

/// <summary>
/// Arma la pantalla de administracion de reglas y reenvia simulaciones y publicaciones.
/// </summary>
/// <remarks>
/// La validacion de un borrador vive en la capa System, junto al motor que lo va a
/// ejecutar. Validar tambien aqui abriria la puerta a que las dos capas discrepen
/// sobre que es una regla valida; esta capa solo acota el tamano y la forma del cuerpo.
/// </remarks>
public sealed class RulesComposer(SystemApiClient system, ChannelPermissions permissions, TimeProvider clock)
{
    public async Task<RulesAdminView> ComposeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var state = await system.GetRulesAsync(cancellationToken).ConfigureAwait(false);

        return new RulesAdminView(
            state.Active,
            state.Versions,
            state.Document,
            state.Store,
            permissions.IsRecentlyReauthenticated(user, clock.GetUtcNow()),
            (int)permissions.ReauthenticationWindow.TotalMinutes,
            RulesLimitsView.Current);
    }

    public Task<RulesVersionDocumentResponse?> GetVersionAsync(string id, CancellationToken cancellationToken = default)
        => system.GetRulesVersionAsync(id, cancellationToken);

    public Task<RulesSimulationResponse> SimulateAsync(RulesSimulationRequest request, CancellationToken cancellationToken = default)
        => system.SimulateRulesAsync(request, cancellationToken);

    public Task<RulesPublishResponse> PublishAsync(RulesPublishRequest request, CancellationToken cancellationToken = default)
        => system.PublishRulesAsync(request, cancellationToken);
}
