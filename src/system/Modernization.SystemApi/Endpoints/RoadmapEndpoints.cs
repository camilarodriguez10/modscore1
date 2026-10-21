using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modernization.Roadmap;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// Plan futuro segun disposition y roadmap. La fecha de inicio entra por
/// parametro para que la planificacion siga siendo determinista y reproducible.
/// </summary>
/// <remarks>
/// El catalogo MAPS y sus parametros forman parte de las reglas vigentes desde la
/// Fase 12: se leen de ellas en cada peticion, no de un singleton fijado al arrancar.
/// </remarks>
public static class RoadmapEndpoints
{
    public static void MapRoadmapEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("Roadmap");

        group.MapGet("/applications/{eaiCode}/roadmap", GetPlan)
            .WithSummary("Plan futuro de una aplicacion: hitos, proyeccion y alertas de coherencia. Con target, el plan hacia ese objetivo.");

        group.MapGet("/roadmap/portfolio", GetPortfolio)
            .WithSummary("El plan del portafolio filtrado, agrupado por la dimension que se pida.");

        group.MapGet("/roadmap/catalog", GetCatalog)
            .WithSummary("Modelo MAPS: las tres dispositions y los siete roadmaps.");
    }

    private static async Task<IResult> GetPlan(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken,
        DateOnly? startDate = null,
        decimal? target = null)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-eai-code",
                "Invalid EAI code",
                "An EAI code is a sequence of up to 10 digits.");
        }

        // El objetivo del What-If Lab, en la escala del modelo. Fuera de ella no es un objetivo
        // difícil: es una entrada inválida, como en el laboratorio.
        if (target is < 20m or > 100m)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-target",
                "Invalid target",
                "The target score must be between 20 and 100.");
        }

        var goal = target is { } value ? Math.Round(value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        var snapshot = await portfolio.FindAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        if (snapshot is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "application-not-found",
                "Application not found",
                "There is no application with that EAI code in the loaded portfolio.");
        }

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var start = startDate ?? DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime);

        return Results.Ok(RoadmapPlanResponse.From(
            rules.Planner.Build(snapshot, start, goal),
            rules.Planner.ActionPlan(snapshot, goal)));
    }

    /// <summary>
    /// El plan del portafolio. Acepta los mismos filtros que el inventario y una
    /// dimension de agrupacion: la proyeccion sale del mismo motor, lo unico que
    /// cambia es por que criterio se promedia.
    /// </summary>
    private static async Task<IResult> GetPortfolio(
        PortfolioService portfolio,
        PortfolioScope scope,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        string? groupBy = null,
        DateOnly? startDate = null)
    {
        if (!TryReadGrouping(groupBy, out var grouping))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-grouping",
                "Invalid grouping",
                "Unknown grouping. Options: portfolio, application, country, cio, lob, disposition, roadmap.");
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

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await portfolio.GetSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        var start = startDate ?? DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime);
        var filtered = await portfolio.FilterAsync(scope, filter, cancellationToken).ConfigureAwait(false);

        return Results.Ok(PortfolioRoadmapResponse.From(rules.Analyzer.Summarize(filtered, start, grouping)));
    }

    /// <summary>
    /// Lee la agrupacion por lista blanca. <c>Enum.TryParse</c> aceptaria tambien un
    /// numero cualquiera y devolveria un valor que no existe en el enum.
    /// </summary>
    private static bool TryReadGrouping(string? value, out RoadmapGrouping grouping)
    {
        grouping = RoadmapGrouping.Portfolio;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "portfolio": grouping = RoadmapGrouping.Portfolio; return true;
            case "application": grouping = RoadmapGrouping.Application; return true;
            case "country": grouping = RoadmapGrouping.Country; return true;
            case "cio": grouping = RoadmapGrouping.Cio; return true;
            case "lob": grouping = RoadmapGrouping.Lob; return true;
            case "disposition": grouping = RoadmapGrouping.Disposition; return true;
            case "roadmap": grouping = RoadmapGrouping.Roadmap; return true;
            default: return false;
        }
    }

    private static async Task<IResult> GetCatalog(PortfolioService portfolio, CancellationToken cancellationToken)
    {
        var catalog = (await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false)).Roadmap;

        return Results.Ok(new
        {
            catalog.Version,
            Dispositions = catalog.Dispositions.Select(d => new
            {
                Key = d.Key.ToString(),
                d.Name,
                d.Definition,
            }),
            Roadmaps = catalog.Roadmaps.Select(r => new
            {
                Key = r.Key.ToString(),
                r.Name,
                Disposition = r.Disposition.ToString(),
                r.Definition,
                r.MappedScore,
                r.RemovesFromPortfolio,
                r.LeverRationale,
                r.TotalWeeks,
                Milestones = r.Milestones.Select(m => new { m.Name, m.Owner, m.Weeks }),
            }),
        });
    }
}
