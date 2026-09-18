using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modernization.Domain;
using Modernization.Insights;
using Modernization.Roadmap;
using Modernization.Scoring.Engine;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// El dashboard ejecutivo: las seis preguntas en una vista, sobre el portafolio filtrado.
/// </summary>
/// <remarks>
/// Acepta los mismos filtros que el inventario y el eje del mapa de calor. Se calcula
/// sobre lo que el alcance deja ver (ADR 0012), con las definiciones de
/// <c>contracts/dashboard-v1.json</c> (ADR 0024).
/// </remarks>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("Dashboard")
            .MapGet("/dashboard", GetDashboard)
            .WithSummary("Indicadores, seis paneles, tendencia por carga y filas de detalle del portafolio filtrado.");
    }

    private static async Task<IResult> GetDashboard(
        PortfolioService portfolio,
        PortfolioScope scope,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        string? axis = null,
        DateOnly? startDate = null)
    {
        var filter = filters.ToFilter();

        if (filter.Validate() is { } invalid)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "Invalid filter",
                invalid);
        }

        var chosenAxis = string.IsNullOrWhiteSpace(axis) ? "country" : axis.Trim();

        if (!DashboardOptions.Axes.Contains(chosenAxis, StringComparer.Ordinal))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-axis",
                "Invalid axis",
                $"The heatmap axes are: {string.Join(", ", DashboardOptions.Axes)}.");
        }

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await portfolio.GetSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        var applications = await portfolio.FilterAsync(scope, filter, cancellationToken).ConfigureAwait(false);
        var start = startDate ?? DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime);

        var scores = new List<ScoreBreakdown>(applications.Count);
        var plans = new List<ApplicationRoadmapPlan>(applications.Count);

        foreach (var application in applications)
        {
            try
            {
                scores.Add(rules.Engine.Calculate(application));
                plans.Add(rules.Planner.Build(application, start));
            }
            catch (RulesetException)
            {
                // Una aplicacion que el motor no puede puntuar queda fuera del dashboard;
                // su detalle individual si muestra el error.
            }
        }

        var definitions = DashboardRules.Default;
        var dashboard = DashboardAnalyzer.Analyze(
            applications,
            scores,
            plans,
            definitions,
            new DashboardOptions(chosenAxis, rules.Roadmap.Parameters.SignificantUserBase));

        var trend = await portfolio.GetDashboardTrendAsync(scope, filter, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new DashboardResponse(
            rules.Info.Id,
            definitions.Version,
            definitions.Status,
            snapshot.CapturedAt,
            trend,
            DashboardResponse.KnownSources,
            dashboard));
    }
}
