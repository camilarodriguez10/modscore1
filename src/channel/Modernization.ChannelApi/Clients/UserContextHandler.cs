using Modernization.ChannelApi.Auth;
using Modernization.Security;

namespace Modernization.ChannelApi.Clients;

/// <summary>
/// Adjunta a cada llamada a la capa System el alcance, el subject y los permisos
/// resueltos del usuario de la peticion en curso.
/// </summary>
/// <remarks>
/// Los headers se borran antes de escribirse: nunca viaja un valor que no haya
/// calculado esta capa a partir de la sesion. Sin usuario autenticado no se
/// adjunta nada, y la capa System responde con el alcance vacio.
/// </remarks>
public sealed class UserContextHandler(
    IHttpContextAccessor accessor,
    ChannelPermissions permissions,
    TimeProvider clock) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Remove(PortfolioScope.HeaderName);
        request.Headers.Remove(ForwardedUser.HeaderName);
        request.Headers.Remove(ForwardedGrants.HeaderName);

        var user = accessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            request.Headers.TryAddWithoutValidation(PortfolioScope.HeaderName, permissions.ScopeOf(user).ToHeaderValue());
            request.Headers.TryAddWithoutValidation(ForwardedUser.HeaderName, ChannelPermissions.SubjectOf(user));

            // Administrar reglas y haberse reautenticado hace poco son dos permisos
            // distintos: la capa System exige el segundo solo para publicar.
            if (permissions.CanAdministerRules(user))
            {
                var grants = new List<string>(2) { ForwardedGrants.RulesAdmin };

                if (permissions.IsRecentlyReauthenticated(user, clock.GetUtcNow()))
                {
                    grants.Add(ForwardedGrants.Reauthenticated);
                }

                request.Headers.TryAddWithoutValidation(ForwardedGrants.HeaderName, ForwardedGrants.ToHeaderValue(grants));
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
