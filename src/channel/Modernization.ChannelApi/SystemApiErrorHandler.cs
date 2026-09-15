using Microsoft.AspNetCore.Diagnostics;
using Modernization.ChannelApi.Clients;
using Modernization.Security;

namespace Modernization.ChannelApi;

/// <summary>
/// Traduce las respuestas de la capa System que tienen significado para quien mira
/// la pantalla.
/// </summary>
/// <remarks>
/// Solo dos: falta la primera carga (409), que tiene una accion asociada, y el
/// recurso esta fuera del alcance del usuario (403). Cualquier otra falla sigue
/// siendo un 500 sin detalle.
/// </remarks>
public sealed class SystemApiErrorHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (status, code, title, detail) = exception switch
        {
            SystemApiNoDataException noData =>
                (StatusCodes.Status409Conflict, "no-data", "No published uploads", noData.Message),
            SystemApiForbiddenException =>
                (StatusCodes.Status403Forbidden, "out-of-scope", "Out of scope",
                    "Your user does not have access to this part of the portfolio."),
            _ => (0, string.Empty, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        await ApiProblem.Create(status, code, title, detail)
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);

        return true;
    }
}
