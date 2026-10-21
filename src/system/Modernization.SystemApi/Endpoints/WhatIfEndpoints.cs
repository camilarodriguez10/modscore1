using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modernization.Rules;
using Modernization.Scoring.Engine;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;
using Modernization.WhatIf;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// Simulacion de escenarios. Los resultados son proyecciones: nunca tocan el
/// snapshot real, que se mantiene inmutable en el servicio de portafolio.
/// </summary>
/// <remarks>
/// Cada peticion toma las reglas vigentes una vez y usa ese mismo juego de principio a
/// fin: el score base y el simulado nunca salen de dos versiones distintas.
/// </remarks>
public static class WhatIfEndpoints
{
    public static void MapWhatIfEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system/applications/{eaiCode}/whatif")
            .RequireAuthorization()
            .WithTags("What-If");

        group.MapGet("/levers", GetLevers)
            .WithSummary("Catalogo de palancas con el delta de cada una, ordenado por retorno.");

        group.MapPost("/scenario", PostScenario)
            .WithSummary("Aplica un conjunto de palancas y recalcula el score desde cero.");

        group.MapPost("/target", PostTarget)
            .WithSummary("Conjunto minimo de palancas que alcanza un score objetivo.");
    }

    private static async Task<IResult> GetLevers(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
        => await WithApplication(
            eaiCode,
            portfolio,
            scope,
            // El objetivo de los planes automáticos viaja con el catálogo: el laboratorio parte de
            // él, así que «Find the minimum plan» da el future plan sin mover nada.
            (snapshot, rules) => Results.Ok(LeverCatalogResponse.From(rules.WhatIf.EvaluateAllLevers(snapshot)) with
            {
                AutomaticTarget = rules.Roadmap.Parameters.AutomaticPlanTarget,
            }),
            cancellationToken).ConfigureAwait(false);

    private static async Task<IResult> PostScenario(
        string eaiCode,
        ScenarioRequest request,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
    {
        if (request?.Levers is null || request.Levers.Count == 0)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "empty-scenario",
                "Empty scenario",
                "A scenario needs at least one lever.");
        }

        return await WithApplication(
            eaiCode,
            portfolio,
            scope,
            (snapshot, rules) =>
            {
                var selections = request.Levers
                    .Select(l => new LeverSelection(l.Key, l.Parameter))
                    .ToList();

                return Results.Ok(ScenarioResponse.From(rules.WhatIf.Evaluate(snapshot, selections)));
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> PostTarget(
        string eaiCode,
        TargetRequest request,
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
        => await WithApplication(
            eaiCode,
            portfolio,
            scope,
            (snapshot, rules) => Results.Ok(TargetResponse.From(rules.WhatIf.ReachTarget(snapshot, request.TargetScore))),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Valida el EAI, lo resuelve dentro del alcance y traduce los fallos de
    /// dominio a respuestas. Un escenario invalido es 400; un dato que no encaja en
    /// el ruleset es 422; una aplicacion fuera de alcance es 404, igual que una
    /// inexistente.
    /// </summary>
    private static async Task<IResult> WithApplication(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        Func<Domain.ApplicationSnapshot, RulesModel, IResult> handler,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-eai-code",
                "Invalid EAI code",
                "An EAI code is a sequence of up to 10 digits.");
        }

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

        try
        {
            return handler(snapshot, rules);
        }
        catch (WhatIfException ex)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                ex.Code,
                "Invalid scenario",
                ex.Message);
        }
        catch (RulesetException)
        {
            return ApiProblem.Create(
                StatusCodes.Status422UnprocessableEntity,
                "ruleset-mismatch",
                "The application's data does not fit the ruleset",
                "One of the application's values falls outside the bands declared by the active ruleset.");
        }
    }
}
