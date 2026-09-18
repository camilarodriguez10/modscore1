using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Modernization.Scoring.Engine;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// Endpoints canonicos de la capa System. No conocen pantallas: devuelven el
/// recurso completo y dejan que la capa Channel lo componga.
/// </summary>
/// <remarks>
/// Cada operacion que lee el portafolio recibe el <see cref="PortfolioScope"/> de
/// quien pregunta, y todo lo que devuelve ya esta recortado a el (ADR 0012).
/// </remarks>
public static class SystemEndpoints
{
    private const int MaxPageSize = 200;

    public static void MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("System");

        group.MapGet("/applications", GetApplications)
            .WithSummary("Inventario de aplicaciones, paginado y filtrable.");

        group.MapGet("/applications/{eaiCode}", GetApplication)
            .WithSummary("Una aplicacion del inventario.");

        group.MapGet("/applications/{eaiCode}/score", GetScore)
            .WithSummary("Desglose completo del score, con procedencia por metrica.");

        group.MapGet("/scores", GetScores)
            .WithSummary("Scores de todo el portafolio, para agregados.");

        group.MapGet("/sources", GetSources)
            .WithSummary("Estado y cobertura de cada fuente, en la carga pedida. Exige alcance completo.");

        group.MapGet("/ruleset", GetRuleset)
            .WithSummary("Ruleset activo: pesos, bandas y procedencia.");
    }

    private static async Task<IResult> GetApplications(
        PortfolioService portfolio,
        PortfolioScope scope,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 50)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-pagination",
                "Invalid pagination parameters",
                $"'page' must be 1 or greater and 'pageSize' must be between 1 and {MaxPageSize}.");
        }

        var filter = filters.ToFilter();

        if (filter.Validate() is { } invalid)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "Invalid filter",
                invalid);
        }

        var filtered = await portfolio.FilterAsync(scope, filter, cancellationToken).ConfigureAwait(false);

        var items = filtered
            .OrderBy(a => a.EaiCode, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ApplicationResponse.From)
            .ToList();

        return Results.Ok(new PagedResponse<ApplicationResponse>(items, page, pageSize, filtered.Count));
    }

    private static async Task<IResult> GetApplication(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var application = await portfolio.FindAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        return application is null
            ? NotFound()
            : Results.Ok(ApplicationResponse.From(application));
    }

    private static async Task<IResult> GetScore(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        try
        {
            var score = await portfolio.GetScoreAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

            return score is null
                ? NotFound()
                : Results.Ok(ScoreResponse.From(score));
        }
        catch (RulesetException)
        {
            // Un dato que no encaja en el ruleset es un problema de datos, no un
            // fallo del servicio: se reporta con detalle accionable.
            return ApiProblem.Create(
                StatusCodes.Status422UnprocessableEntity,
                "ruleset-mismatch",
                "The application's data does not fit the ruleset",
                "One of the application's values falls outside the bands declared by the active ruleset.");
        }
    }

    private static async Task<IResult> GetScores(
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 100)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-pagination",
                "Invalid pagination parameters",
                $"'page' must be 1 or greater and 'pageSize' must be between 1 and {MaxPageSize}.");
        }

        var scores = await portfolio.GetAllScoresAsync(scope, cancellationToken).ConfigureAwait(false);

        var items = scores
            .OrderBy(s => s.EaiCode, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ScoreResponse.From)
            .ToList();

        return Results.Ok(new PagedResponse<ScoreResponse>(items, page, pageSize, scores.Count));
    }

    /// <summary>
    /// Las estadisticas de fuente son de archivos completos e incluyen los EAI de
    /// las filas rechazadas: no se pueden recortar a un alcance parcial sin mentir
    /// en los totales, asi que exigen alcance completo.
    /// </summary>
    private static async Task<IResult> GetSources(
        HttpContext context,
        PortfolioService portfolio,
        PortfolioScope scope,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? version = null)
    {
        if (!scope.IsFull)
        {
            SecurityAudit.ScopeDenied(
                SecurityAudit.CreateLogger(loggers),
                ForwardedUser.Read(context),
                SecurityAudit.Clean(context.Request.Path.Value));

            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "insufficient-scope",
                "Insufficient scope",
                "Source status requires scope over the whole portfolio.");
        }

        // El identificador llega del cliente y en el adaptador de archivos es un
        // directorio. Se acepta solo si esta en la lista de cargas publicadas: no
        // hay forma de construir una ruta con un valor que no salio de ahi.
        if (!string.IsNullOrWhiteSpace(version))
        {
            var versions = await portfolio.GetVersionsAsync(cancellationToken).ConfigureAwait(false);

            if (!versions.Any(v => string.Equals(v.Id, version, StringComparison.Ordinal)))
            {
                return ApiProblem.Create(
                    StatusCodes.Status404NotFound,
                    "version-not-found",
                    "Upload not found",
                    "There is no upload with that identifier.");
            }
        }

        var snapshot = await portfolio.GetSnapshotAsync(version, scope, cancellationToken).ConfigureAwait(false);

        var items = snapshot.Sources
            .Select(s => SourceStatusResponse.From(s, snapshot.CoverageOf(s.SourceName)))
            .ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> GetRuleset(PortfolioService portfolio, CancellationToken cancellationToken)
    {
        var ruleset = (await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false)).Ruleset;

        return Results.Ok(new RulesetResponse(
            ruleset.Version,
            ruleset.Description,
            ruleset.Normalization.Floor,
            ruleset.Normalization.Range,
            [.. ruleset.Pillars.Select(p => new RulesetPillarResponse(p.Key, p.Name, p.NominalWeight))],
            [.. ruleset.Metrics.Select(m => new RulesetMetricResponse(
                m.Key,
                m.Name,
                m.Pillar,
                m.Weight,
                m.Source.System,
                m.Source.Report,
                m.Source.Field,
                m.Mapping.Kind.ToString(),
                m.Note))]));
    }

    private static IResult NotFound() => ApiProblem.Create(
        StatusCodes.Status404NotFound,
        "application-not-found",
        "Application not found",
        "There is no application with that EAI code in the loaded portfolio.");

    private static IResult InvalidEaiCode() => ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-eai-code",
        "Invalid EAI code",
        "An EAI code is a sequence of up to 10 digits.");
}
