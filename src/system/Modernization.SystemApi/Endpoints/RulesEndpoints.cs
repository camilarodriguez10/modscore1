using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Modernization.Domain;
using Modernization.Domain.Rules;
using Modernization.Rules;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// Administracion de las reglas de scoring.
/// </summary>
/// <remarks>
/// Quien edita las reglas mueve el score de todo el portafolio, asi que el liston es
/// el mas alto del sistema. La capa Channel exige el rol, el alcance completo y una
/// reautenticacion reciente; esta capa vuelve a exigir las tres cosas, declaradas por
/// un llamador con token de servicio. Si una comprobacion fallara, la otra no.
/// </remarks>
public static class RulesEndpoints
{
    public static void MapRulesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system/rules")
            .RequireAuthorization()
            .WithTags("Rules");

        group.MapGet(string.Empty, GetState)
            .WithSummary("Reglas vigentes, su documento editable y las versiones publicadas.");

        group.MapGet("/versions/{id}", GetVersion)
            .WithSummary("El documento de una version, para verla o restaurarla.");

        group.MapPost("/simulate", PostSimulate)
            .WithSummary("Valida un borrador y simula su impacto sobre todo el portafolio.")
            .DisableAntiforgery();

        group.MapPost("/publish", PostPublish)
            .WithSummary("Publica un borrador como version nueva. Exige reautenticacion reciente.")
            .DisableAntiforgery();

        group.MapGet("/compare", GetCompare)
            .WithSummary("Dos versiones comparadas: cambios, impacto y, con eai, una aplicacion metrica por metrica.");

        group.MapGet("/compare/applications", GetCompareApplications)
            .WithSummary("Cada aplicacion antes y despues entre dos versiones, para exportar.");

        // Lo lee quien consulta, no solo quien administra: marca la tendencia y avisa de
        // una version nueva. No dice quien publico.
        group.MapGet("/timeline", GetTimeline)
            .WithSummary("Cuando cambiaron las reglas y por que.");
    }

    private static async Task<IResult> GetTimeline(RulesService rules, CancellationToken cancellationToken)
    {
        var active = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var versions = await rules.ListAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new RulesTimelineResponse(
            RulesTimelineEntryResponse.From(active.Info, active.Id),
            [.. versions.Select(v => RulesTimelineEntryResponse.From(v, active.Id))]));
    }

    private static async Task<IResult> GetCompare(
        HttpContext context,
        PortfolioScope scope,
        RulesService rules,
        PortfolioService portfolio,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? from = null,
        string? to = null,
        string? eai = null)
    {
        if (Deny(context, scope, loggers, requireReauthentication: false) is { } denied)
        {
            return denied;
        }

        if (!RulesVersionId.IsWellFormed(from) || !RulesVersionId.IsWellFormed(to))
        {
            return InvalidVersion();
        }

        if (eai is not null && !InputValidation.IsValidEaiCode(eai))
        {
            return InvalidEaiCode();
        }

        var snapshot = await portfolio.GetSnapshotAsync(PortfolioScope.Full, cancellationToken).ConfigureAwait(false);
        ApplicationSnapshot? application = null;

        if (eai is not null && (application = snapshot.Find(eai)) is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "application-not-found",
                "Application not found",
                "There is no application with that EAI code in the loaded portfolio.");
        }

        RulesComparisonOutcome? outcome;

        try
        {
            outcome = await rules.CompareAsync(
                from!,
                to!,
                snapshot.Applications,
                DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime),
                cancellationToken).ConfigureAwait(false);
        }
        catch (RulesStoreUnavailableException)
        {
            return StoreUnavailable();
        }

        if (outcome is null)
        {
            return VersionNotFound();
        }

        var active = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new RulesCompareResponse(
            RulesVersionResponse.From(outcome.From.Info, active.Id),
            RulesVersionResponse.From(outcome.To.Info, active.Id),
            outcome.Changes,
            outcome.Impact,
            application is null ? null : RulesComparison.Compare(application, outcome.From, outcome.To)));
    }

    private static async Task<IResult> GetCompareApplications(
        HttpContext context,
        PortfolioScope scope,
        RulesService rules,
        PortfolioService portfolio,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? from = null,
        string? to = null)
    {
        if (Deny(context, scope, loggers, requireReauthentication: false) is { } denied)
        {
            return denied;
        }

        if (!RulesVersionId.IsWellFormed(from) || !RulesVersionId.IsWellFormed(to))
        {
            return InvalidVersion();
        }

        var snapshot = await portfolio.GetSnapshotAsync(PortfolioScope.Full, cancellationToken).ConfigureAwait(false);

        try
        {
            var outcome = await rules.CompareAsync(
                from!,
                to!,
                snapshot.Applications,
                DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime),
                cancellationToken).ConfigureAwait(false);

            return outcome is null ? VersionNotFound() : Results.Ok(outcome.Impact.Shifts);
        }
        catch (RulesStoreUnavailableException)
        {
            return StoreUnavailable();
        }
    }

    private static async Task<IResult> GetState(
        HttpContext context,
        PortfolioScope scope,
        RulesService rules,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (Deny(context, scope, loggers, requireReauthentication: false) is { } denied)
        {
            return denied;
        }

        var active = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var versions = await rules.ListAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new RulesStateResponse(
            RulesVersionResponse.From(active.Info, active.Id),
            [.. versions.Select(v => RulesVersionResponse.From(v, active.Id))],
            RulesDocument.From(active),
            rules.StoreKind));
    }

    private static async Task<IResult> GetVersion(
        string id,
        HttpContext context,
        PortfolioScope scope,
        RulesService rules,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (Deny(context, scope, loggers, requireReauthentication: false) is { } denied)
        {
            return denied;
        }

        // El identificador se valida por forma antes de buscarlo: en el almacen de
        // archivos es un directorio.
        if (!RulesVersionId.IsWellFormed(id))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-rules-version",
                "Invalid rules version",
                "A rules version looks like v1.4.");
        }

        RulesModel? model;

        try
        {
            model = await rules.GetVersionAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (RulesStoreUnavailableException)
        {
            return StoreUnavailable();
        }

        if (model is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "rules-version-not-found",
                "Rules version not found",
                "There is no published rules version with that identifier.");
        }

        var active = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new RulesVersionDocumentResponse(
            RulesVersionResponse.From(model.Info, active.Id),
            RulesDocument.From(model)));
    }

    private static async Task<IResult> PostSimulate(
        HttpRequest request,
        PortfolioScope scope,
        RulesService rules,
        PortfolioService portfolio,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (Deny(request.HttpContext, scope, loggers, requireReauthentication: false) is { } denied)
        {
            return denied;
        }

        var body = await BoundedJson
            .ReadAsync<RulesSimulationRequest>(request, RulesLimits.MaxRequestBytes, cancellationToken)
            .ConfigureAwait(false);

        if (body.Problem is not null)
        {
            return body.Problem;
        }

        if (body.Value!.EaiCode is { } eai && !InputValidation.IsValidEaiCode(eai))
        {
            return InvalidEaiCode();
        }

        // El impacto es sobre todo el portafolio: por eso administrar reglas exige
        // alcance completo, y por eso aqui no se recorta nada.
        var snapshot = await portfolio.GetSnapshotAsync(PortfolioScope.Full, cancellationToken).ConfigureAwait(false);

        // Una aplicacion que no esta en la carga no invalida el borrador: la simulacion
        // sale igual, sin el detalle de esa aplicacion.
        var focus = body.Value.EaiCode is { } focusCode ? snapshot.Find(focusCode) : null;

        var outcome = await rules.SimulateAsync(
            body.Value.BasedOn,
            body.Value.Draft,
            snapshot.Applications,
            DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime),
            focus,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(RulesSimulationResponse.From(outcome));
    }

    private static async Task<IResult> PostPublish(
        HttpRequest request,
        PortfolioScope scope,
        RulesService rules,
        PortfolioService portfolio,
        PortfolioWarmup warmup,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (Deny(request.HttpContext, scope, loggers, requireReauthentication: true) is { } denied)
        {
            return denied;
        }

        var body = await BoundedJson
            .ReadAsync<RulesPublishRequest>(request, RulesLimits.MaxRequestBytes, cancellationToken)
            .ConfigureAwait(false);

        if (body.Problem is not null)
        {
            return body.Problem;
        }

        var audit = SecurityAudit.CreateLogger(loggers);
        var user = ForwardedUser.Read(request.HttpContext);
        var snapshot = await portfolio.GetSnapshotAsync(PortfolioScope.Full, cancellationToken).ConfigureAwait(false);

        var outcome = await rules.PublishAsync(
            body.Value!.BasedOn,
            body.Value.Reason,
            body.Value.Draft,
            user,
            snapshot.Applications,
            DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime),
            cancellationToken).ConfigureAwait(false);

        if (outcome is { Published: true, Version: { } version, Impact: { } impact })
        {
            // Las reglas nuevas dejan frios scores e historia: se calculan ya, no cuando
            // alguien abra la tendencia.
            warmup.Request();

            SecurityAudit.RulesPublished(
                audit,
                user,
                version.Id,
                version.BasedOn,
                outcome.Changes.Count,
                impact.Up,
                impact.Down,
                impact.AverageDelta);
        }
        else
        {
            SecurityAudit.RulesRejected(audit, user, outcome.Errors.Count > 0 ? outcome.Errors[0].Code : "unknown");
        }

        return Results.Ok(RulesPublishResponse.From(outcome));
    }

    private static IResult InvalidVersion() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-rules-version",
        "Invalid rules version",
        "A rules version looks like v1.4.");

    private static IResult VersionNotFound() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "rules-version-not-found",
        "Rules version not found",
        "There is no published rules version with that identifier.");

    private static IResult InvalidEaiCode() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-eai-code",
        "Invalid EAI code",
        "An EAI code is a sequence of up to 10 digits.");

    internal static IResult StoreUnavailable() => ApiProblem.Create(
        StatusCodes.Status503ServiceUnavailable,
        "rules-store-unavailable",
        "Rules store unavailable",
        "The rules store did not respond. Scores keep using the active rules; try again in a moment.");

    /// <summary>
    /// Alcance completo, permiso de administrar reglas y, para publicar, reautenticacion
    /// reciente. Toda negacion queda auditada.
    /// </summary>
    private static IResult? Deny(
        HttpContext context,
        PortfolioScope scope,
        ILoggerFactory loggers,
        bool requireReauthentication)
    {
        var permitted = scope.IsFull && ForwardedGrants.Has(context, ForwardedGrants.RulesAdmin);
        var reauthenticated = !requireReauthentication || ForwardedGrants.Has(context, ForwardedGrants.Reauthenticated);

        if (permitted && reauthenticated)
        {
            return null;
        }

        SecurityAudit.ScopeDenied(
            SecurityAudit.CreateLogger(loggers),
            ForwardedUser.Read(context),
            SecurityAudit.Clean(context.Request.Path.Value));

        return permitted
            ? ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "step-up-required",
                "Reauthentication required",
                "Publishing rules requires having signed in again recently.")
            : ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "insufficient-permission",
                "Insufficient permission",
                "Administering rules requires the rules administrator role and scope over the whole portfolio.");
    }
}
