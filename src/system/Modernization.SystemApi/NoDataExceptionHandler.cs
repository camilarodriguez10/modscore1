using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Modernization.Domain.Sources;
using Modernization.Security;

namespace Modernization.SystemApi;

/// <summary>
/// Traduce "todavia no hay datos" a un 409, no a un 500.
/// </summary>
/// <remarks>
/// La distincion importa aguas arriba: un 500 es un fallo que el usuario no puede
/// resolver, mientras que un 409 aqui significa exactamente una cosa — falta la
/// primera carga — y la interfaz puede ofrecer la accion que lo arregla en vez de
/// una pantalla de error.
/// </remarks>
public sealed class NoDataExceptionHandler : IExceptionHandler
{
    /// <summary>Marca en el ProblemDetails para que la capa Channel no adivine por el estado.</summary>
    public static readonly string ProblemType = ApiProblem.TypeOf("no-data");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not NoSnapshotAvailableException noData)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        await ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "no-data",
                "No published uploads",
                noData.Message)
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);

        return true;
    }
}
