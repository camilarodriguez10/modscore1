using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modernization.Domain.Code;
using Modernization.Insights;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// La pantalla Code: obsolescencia y riesgo según lo que declaran los repositorios de cada
/// aplicación, cruzado con las fechas de fin de vida de endoflife.date (ADR 0026).
/// </summary>
/// <remarks>
/// Los mismos filtros y el mismo alcance que el inventario: un repositorio solo cuenta si
/// su aplicación es visible para quien pregunta (ADR 0012). Las reglas son las del área
/// <c>code</c> de las definiciones vigentes, editables desde el portal.
/// </remarks>
public static class CodeEndpoints
{
    public static void MapCodeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("Code");

        group.MapGet("/code", GetCode)
            .WithSummary("Componentes, fin de vida, bibliotecas y riesgo de codigo del portafolio filtrado.");

        group.MapGet("/code/applications/{eaiCode}", GetApplication)
            .WithSummary("Repositorios, componentes y dependencias de una aplicacion, con su indice de riesgo.");
    }

    private static async Task<IResult> GetCode(
        PortfolioService portfolio,
        PortfolioScope scope,
        ICodeInventoryProvider inventories,
        EndOfLifeCatalog catalog,
        TimeProvider time,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken)
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
        var applications = await portfolio.FilterAsync(scope, filter, cancellationToken).ConfigureAwait(false);
        var inventory = await inventories.GetAsync(cancellationToken).ConfigureAwait(false);

        var analysis = CodeAnalyzer.Analyze(applications, inventory, catalog, rules.Analytics, Today(time));
        var trend = await portfolio.GetDashboardTrendAsync(scope, filter, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new CodeResponse(
            rules.Info.Id,
            CodeSourceResponse.From(inventory, catalog),
            analysis,
            trend,
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));
    }

    private static async Task<IResult> GetApplication(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
        ICodeInventoryProvider inventories,
        EndOfLifeCatalog catalog,
        TimeProvider time,
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

        // Fuera del alcance es indistinguible de inexistente, igual que en el resto del API.
        var application = await portfolio.FindAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        if (application is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "application-not-found",
                "Application not found",
                "There is no application with that EAI code in the loaded portfolio.");
        }

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var inventory = await inventories.GetAsync(cancellationToken).ConfigureAwait(false);
        var today = Today(time);
        var detail = CodeAnalyzer.Detail(application, inventory, catalog, rules.Analytics, today);
        var history = await portfolio.GetHistoryAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new CodeApplicationResponse(
            rules.Info.Id,
            today,
            CodeSourceResponse.From(inventory, catalog),
            detail,
            [.. history.Select(CodeHistoryPoint.From)],
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));
    }

    /// <summary>El fin de vida se evalúa contra el día de hoy, en UTC.</summary>
    private static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
