using System.Security.Claims;
using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.Composition;
using Modernization.Domain.Rules;
using Modernization.Rules;
using Modernization.Security;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Endpoints;

/// <summary>
/// Pantalla 09 — administracion de reglas, y la reautenticacion que exige publicar.
/// </summary>
/// <remarks>
/// Cada endpoint exige el rol <c>ruleset.admin</c> con alcance completo. Publicar
/// exige ademas haberse autenticado hace poco (ASVS 3.7.1), y tiene su propio limite
/// de frecuencia. Como toda mutacion, pasa antes por la validacion CSRF.
/// </remarks>
public static class RulesAdminEndpoints
{
    public static void MapRulesAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var rules = app.MapGroup("/api/admin/rules")
            .RequireAuthorization(ChannelPermissions.RulesAdminPolicy)
            .WithTags("Channel");

        rules.MapGet(string.Empty, GetRules)
            .WithSummary("Pantalla 09 — reglas vigentes, documento editable y versiones publicadas.");

        rules.MapGet("/versions/{id}", GetVersion)
            .WithSummary("El documento de una version anterior, para restaurarla como borrador.");

        rules.MapPost("/simulate", PostSimulate)
            .RequireRateLimiting(ChannelLimits.RulesSimulationPolicy)
            .WithSummary("Valida un borrador y simula su impacto sobre todo el portafolio.");

        rules.MapPost("/publish", PostPublish)
            .RequireRateLimiting(ChannelLimits.RulesPublishPolicy)
            .WithSummary("Publica un borrador como version nueva. Exige reautenticacion reciente.");

        // Solo exige sesion: quien no administra reglas no llega a la pantalla que
        // enlaza aqui, y reautenticarse no concede nada que no tuviera.
        app.MapGet("/api/login/step-up", StepUpAsync)
            .RequireAuthorization()
            .WithTags("Channel")
            .WithSummary("Vuelve a autenticar al usuario antes de una operacion sensible.");
    }

    private static async Task<IResult> GetRules(
        ClaimsPrincipal user,
        RulesComposer composer,
        CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeAsync(user, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetVersion(
        string id,
        RulesComposer composer,
        CancellationToken cancellationToken)
    {
        if (!RulesVersionId.IsWellFormed(id))
        {
            return InvalidVersion();
        }

        var view = await composer.GetVersionAsync(id, cancellationToken).ConfigureAwait(false);

        return view is null
            ? ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "rules-version-not-found",
                "Rules version not found",
                "There is no published rules version with that identifier.")
            : Results.Ok(view);
    }

    private static async Task<IResult> PostSimulate(
        HttpRequest request,
        RulesComposer composer,
        CancellationToken cancellationToken)
    {
        var body = await BoundedJson
            .ReadAsync<RulesSimulationRequest>(request, RulesLimits.MaxRequestBytes, cancellationToken)
            .ConfigureAwait(false);

        if (body.Problem is not null)
        {
            return body.Problem;
        }

        if (!RulesVersionId.IsWellFormed(body.Value!.BasedOn))
        {
            return InvalidVersion();
        }

        try
        {
            return Results.Ok(await composer.SimulateAsync(body.Value, cancellationToken).ConfigureAwait(false));
        }
        catch (SystemApiException)
        {
            return RejectedBody();
        }
    }

    private static async Task<IResult> PostPublish(
        HttpRequest request,
        ChannelPermissions permissions,
        TimeProvider clock,
        ILoggerFactory loggers,
        RulesComposer composer,
        CancellationToken cancellationToken)
    {
        var user = request.HttpContext.User;

        // Antes de leer el cuerpo: sin reautenticacion reciente no hay nada que validar.
        if (!permissions.IsRecentlyReauthenticated(user, clock.GetUtcNow()))
        {
            SecurityAudit.StepUpRequired(
                SecurityAudit.CreateLogger(loggers),
                ChannelPermissions.SubjectOf(user),
                SecurityAudit.Clean(request.Path.Value));

            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "step-up-required",
                "Reauthentication required",
                $"Sign in again to publish rules. A sign-in is valid for {(int)permissions.ReauthenticationWindow.TotalMinutes} minutes.");
        }

        var body = await BoundedJson
            .ReadAsync<RulesPublishRequest>(request, RulesLimits.MaxRequestBytes, cancellationToken)
            .ConfigureAwait(false);

        if (body.Problem is not null)
        {
            return body.Problem;
        }

        if (!RulesVersionId.IsWellFormed(body.Value!.BasedOn))
        {
            return InvalidVersion();
        }

        try
        {
            return Results.Ok(await composer.PublishAsync(body.Value, cancellationToken).ConfigureAwait(false));
        }
        catch (SystemApiException)
        {
            return RejectedBody();
        }
    }

    /// <summary>
    /// Reautenticacion. Lo que hace depende del proveedor de identidad y vive en
    /// <see cref="IReauthenticationFlow"/>; aqui solo se valida a donde volver.
    /// </summary>
    private static Task<IResult> StepUpAsync(
        HttpContext context,
        IReauthenticationFlow reauthentication,
        string? returnTo = null)
        => reauthentication.StartAsync(context, StepUp.SafeReturnPath(returnTo));

    private static IResult InvalidVersion() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-rules-version",
        "Invalid rules version",
        "A rules version looks like v1.4.");

    private static IResult RejectedBody() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-body",
        "Invalid request body",
        "The rules draft was not accepted. Reload the page and try again.");
}
