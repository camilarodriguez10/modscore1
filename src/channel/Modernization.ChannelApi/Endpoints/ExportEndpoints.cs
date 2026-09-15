using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.Composition;
using Modernization.Domain.Rules;
using Modernization.Security;

namespace Modernization.ChannelApi.Endpoints;

/// <summary>
/// Descargas en CSV con los filtros de la pantalla.
/// </summary>
/// <remarks>
/// Exportar es leer muchas filas de una vez: lleva su propio limite de frecuencia y queda
/// auditado con cuantas filas salieron. El archivo viaja como adjunto y sin cache (el
/// middleware de cabeceras pone <c>no-store</c> y <c>nosniff</c>): el navegador lo
/// descarga, no lo interpreta.
/// </remarks>
public static class ExportEndpoints
{
    private const string CsvContentType = "text/csv; charset=utf-8";

    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var api = app.MapGroup("/api")
            .RequireAuthorization(ChannelPermissions.ReadPolicy)
            .RequireRateLimiting(ChannelLimits.ExportPolicy)
            .WithTags("Channel");

        api.MapGet("/portfolio/export.csv", GetPortfolio)
            .WithSummary("El portafolio filtrado, en CSV.");

        api.MapGet("/roadmap/export.csv", GetRoadmap)
            .WithSummary("El plan del portafolio filtrado y agrupado, en CSV.");

        app.MapGet("/api/admin/rules/compare/export.csv", GetRulesComparison)
            .RequireAuthorization(ChannelPermissions.RulesAdminPolicy)
            .RequireRateLimiting(ChannelLimits.ExportPolicy)
            .WithTags("Channel")
            .WithSummary("Cada aplicacion antes y despues de un cambio de reglas, en CSV.");
    }

    private static async Task<IResult> GetPortfolio(
        HttpContext context,
        ExportComposer exports,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? lob = null,
        string? country = null,
        string? cio = null,
        string? disposition = null,
        string? roadmap = null,
        string? band = null,
        bool? criticalOnly = null)
    {
        if (ChannelEndpoints.InvalidFilters(lob, country, cio, disposition, roadmap, band) is { } problem)
        {
            return problem;
        }

        var file = await exports.PortfolioAsync(
            new ApplicationQuery
            {
                Lob = lob,
                Country = country,
                Cio = cio,
                Disposition = disposition,
                Roadmap = roadmap,
                Band = band,
                CriticalOnly = criticalOnly,
            },
            cancellationToken).ConfigureAwait(false);

        return Deliver(context, loggers, "portfolio", file);
    }

    private static async Task<IResult> GetRoadmap(
        HttpContext context,
        ExportComposer exports,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? groupBy = null,
        string? lob = null,
        string? country = null,
        string? cio = null,
        string? disposition = null,
        string? roadmap = null,
        string? band = null,
        bool? criticalOnly = null)
    {
        if (groupBy is not null && !ChannelEndpoints.Groupings.Contains(groupBy, StringComparer.OrdinalIgnoreCase))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-grouping",
                "Invalid grouping",
                $"The available groupings are: {string.Join(", ", ChannelEndpoints.Groupings)}.");
        }

        if (ChannelEndpoints.InvalidFilters(lob, country, cio, disposition, roadmap, band) is { } problem)
        {
            return problem;
        }

        var file = await exports.RoadmapAsync(
            new ApplicationQuery
            {
                Lob = lob,
                Country = country,
                Cio = cio,
                Disposition = disposition,
                Roadmap = roadmap,
                Band = band,
                CriticalOnly = criticalOnly,
            },
            groupBy,
            cancellationToken).ConfigureAwait(false);

        return Deliver(context, loggers, "roadmap", file);
    }

    private static async Task<IResult> GetRulesComparison(
        HttpContext context,
        ExportComposer exports,
        ILoggerFactory loggers,
        CancellationToken cancellationToken,
        string? from = null,
        string? to = null)
    {
        if (!RulesVersionId.IsWellFormed(from) || !RulesVersionId.IsWellFormed(to))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-rules-version",
                "Invalid rules version",
                "A rules version looks like v1.4.");
        }

        var file = await exports.RulesComparisonAsync(from!, to!, cancellationToken).ConfigureAwait(false);

        return file is null
            ? ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "rules-version-not-found",
                "Rules version not found",
                "There is no published rules version with that identifier.")
            : Deliver(context, loggers, "rules-comparison", file);
    }

    private static IResult Deliver(HttpContext context, ILoggerFactory loggers, string kind, ExportFile file)
    {
        SecurityAudit.DataExported(
            SecurityAudit.CreateLogger(loggers),
            ChannelPermissions.SubjectOf(context.User),
            kind,
            file.Document.RowCount);

        return Results.File(file.Document.ToBytes(), CsvContentType, file.FileName);
    }
}
