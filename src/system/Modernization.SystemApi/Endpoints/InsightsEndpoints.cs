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
/// Las seis preguntas del portafolio, respondidas sobre el portafolio filtrado.
/// </summary>
/// <remarks>
/// Acepta los mismos filtros que el inventario, así que la respuesta habla siempre del
/// mismo recorte que la persona está mirando. Los umbrales de "sano" y de "base de
/// usuarios" salen de las reglas vigentes: cambiarlas desde el portal cambia también
/// este análisis, sin desplegar.
/// </remarks>
public static class InsightsEndpoints
{
    public static void MapInsightsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("Insights")
            .MapGet("/insights", GetInsights)
            .WithSummary("Salud, prioridades, complejidad, riesgo, obsolescencia y resiliencia del portafolio filtrado.");
    }

    private static async Task<IResult> GetInsights(
        PortfolioService portfolio,
        PortfolioScope scope,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        int? duplicates = null,
        int? top = null,
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
                // Una aplicación que el motor no puede puntuar queda fuera del análisis;
                // su detalle individual sí muestra el error.
            }
        }

        var options = new InsightsOptions(
            rules.Roadmap.Parameters.HealthyScore,
            rules.Roadmap.Parameters.SignificantUserBase,
            duplicates ?? 3,
            top ?? 10);

        var insights = InsightsAnalyzer.Analyze(applications, scores, plans, options);

        return Results.Ok(InsightsResponse.From(rules.Info.Id, snapshot.CapturedAt, insights));
    }
}
