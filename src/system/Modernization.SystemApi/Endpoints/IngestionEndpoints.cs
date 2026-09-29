using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Modernization.Domain.Sources;
using Modernization.Security;
using Modernization.Sources.Files;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Endpoints;

/// <summary>
/// Carga de archivos e historia de versiones.
/// </summary>
/// <remarks>
/// La carga es el endpoint mas peligroso del sistema. Los controles viven en
/// varias capas: limite de tamano en el servidor antes de leer un byte, alcance
/// completo, lista blanca de nombres esperados, validacion de esquema antes de
/// aceptar, una publicacion a la vez y escritura atomica desde staging.
/// </remarks>
public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/system")
            .RequireAuthorization()
            .WithTags("Ingestion");

        group.MapGet("/versions", GetVersions)
            .WithSummary("Cargas disponibles, de la mas reciente a la mas antigua.");

        group.MapGet("/applications/{eaiCode}/history", GetApplicationHistory)
            .WithSummary("Score de una aplicacion en cada carga.");

        group.MapGet("/history/portfolio", GetPortfolioHistory)
            .WithSummary("Score promedio del portafolio en cada carga, dentro del alcance.");

        group.MapGet("/ingestion/expected-files", GetExpectedFiles)
            .WithSummary("Archivos que la carga espera, con sus columnas obligatorias.");

        group.MapGet("/ingestion/capabilities", GetCapabilities)
            .WithSummary("Adaptador activo y si admite cargas.");

        group.MapPost("/ingestion/upload", PostUpload)
            .WithSummary("Publica una carga nueva y recalcula el portafolio. Exige alcance completo.")
            .DisableAntiforgery();

        group.MapPost("/ingestion/mapping/preview", PostMappingPreview)
            .WithSummary("Propone qué columna de cada archivo carga cada campo, solo con los encabezados. No publica nada.");

        group.MapGet("/ingestion/mapping", GetMapping)
            .WithSummary("De dónde sale cada campo de la base: nombres aceptados y columna de la carga vigente.");
    }

    /// <summary>
    /// El mapping vigente, fuente por fuente. Describe cargas completas, como el linaje:
    /// exige alcance sobre todo el portafolio.
    /// </summary>
    private static async Task<IResult> GetMapping(
        HttpContext context,
        PortfolioScope scope,
        IngestionService ingestion,
        PortfolioService portfolio,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!scope.IsFull)
        {
            var audit = SecurityAudit.CreateLogger(loggers);
            SecurityAudit.ScopeDenied(audit, ForwardedUser.Read(context), SecurityAudit.Clean(context.Request.Path.Value));

            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "insufficient-scope",
                "Insufficient scope",
                "The column mapping describes whole uploads and requires scope over the whole portfolio.");
        }

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var metrics = rules.Ruleset.Metrics.Select(m => (m.Name, m.Source.Field)).ToList();

        return Results.Ok(await ingestion.DescribeMappingAsync(metrics, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Revisión del mapping antes de cargar. Llegan solo los encabezados, pero es parte de
    /// publicar: exige lo mismo que la carga.
    /// </summary>
    private static async Task<IResult> PostMappingPreview(
        MappingPreviewRequest? request,
        HttpContext context,
        PortfolioScope scope,
        IngestionService ingestion,
        PortfolioService portfolio,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!scope.IsFull)
        {
            var audit = SecurityAudit.CreateLogger(loggers);
            SecurityAudit.ScopeDenied(audit, ForwardedUser.Read(context), SecurityAudit.Clean(context.Request.Path.Value));

            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "insufficient-scope",
                "Insufficient scope",
                "Reviewing a column mapping requires scope over the whole portfolio, like publishing an upload.");
        }

        if (request?.Files is not { Count: > 0 } files
            || files.Count > IngestionService.MaxFiles
            || files.Any(f => f is null
                || (f.Header?.Length ?? 0) > ColumnMapping.MaxHeaderLineLength
                || (f.Columns?.Count ?? 0) > IngestionService.MaxColumnMapEntries))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-mapping-preview",
                "Invalid mapping review",
                $"Send between 1 and {IngestionService.MaxFiles} file headers, each up to {ColumnMapping.MaxHeaderLineLength} characters.");
        }

        var rules = await portfolio.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        var metrics = rules.Ruleset.Metrics.Select(m => (m.Name, m.Source.Field)).ToList();

        var preview = await ingestion.PreviewMappingAsync(request, metrics, cancellationToken).ConfigureAwait(false);

        return Results.Ok(preview);
    }

    private static async Task<IResult> GetVersions(
        IApplicationSnapshotProvider provider,
        CancellationToken cancellationToken)
    {
        var versions = await provider.GetVersionsAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(versions.Select(VersionResponse.From));
    }

    private static async Task<IResult> GetApplicationHistory(
        string eaiCode,
        PortfolioService portfolio,
        PortfolioScope scope,
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

        var history = await portfolio.GetHistoryAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        return Results.Ok(history.Select(HistoryPointResponse.From));
    }

    private static async Task<IResult> GetPortfolioHistory(
        PortfolioService portfolio,
        PortfolioScope scope,
        CancellationToken cancellationToken)
    {
        var history = await portfolio.GetPortfolioHistoryAsync(scope, cancellationToken).ConfigureAwait(false);

        return Results.Ok(history.Select(HistoryPointResponse.From));
    }

    private static IResult GetExpectedFiles(IngestionService ingestion)
        => Results.Ok(ingestion.DescribeExpectedFiles());

    private static IResult GetCapabilities(IApplicationSnapshotProvider provider, IngestionService ingestion)
        => Results.Ok(new IngestionCapabilitiesResponse(provider.Provider, ingestion.IsEnabled));

    private static async Task<IResult> PostUpload(
        HttpRequest request,
        PortfolioScope scope,
        IngestionService ingestion,
        PortfolioService portfolio,
        PortfolioWarmup warmup,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var audit = SecurityAudit.CreateLogger(loggers);
        var user = ForwardedUser.Read(request.HttpContext);

        // Defensa en profundidad: la capa Channel ya exige alcance completo para
        // publicar. Si esa comprobacion fallara, esta no.
        if (!scope.IsFull)
        {
            SecurityAudit.ScopeDenied(audit, user, SecurityAudit.Clean(request.Path.Value));

            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "insufficient-scope",
                "Insufficient scope",
                "Publishing an upload requires scope over the whole portfolio.");
        }

        // El limite general es 1 MB; la carga lo amplia aqui, antes de leer un byte.
        var bodyLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false })
        {
            bodyLimit.MaxRequestBodySize = SystemLimits.UploadMaxRequestBytes;
        }

        if (!request.HasFormContentType)
        {
            return ApiProblem.Create(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported-media-type",
                "Invalid request format",
                "The upload must be sent as multipart/form-data.");
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return ApiProblem.Create(
                StatusCodes.Status413PayloadTooLarge,
                "upload-too-large",
                "Upload too large",
                "The upload exceeds the accepted maximum size.");
        }
        catch (InvalidDataException)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-upload",
                "Invalid upload",
                "The upload is not valid multipart content or exceeds the accepted limits.");
        }

        // Una asignacion por archivo y en su mismo orden, cuando el usuario tuvo que
        // desambiguar un encabezado. Vacio significa que lo decida el encabezado.
        var assignments = form["assign"].Count == 0
            ? null
            : form["assign"].Select(v => v ?? string.Empty).ToList();

        // El mapping de columnas, uno por archivo y en su mismo orden, cuando se revisó
        // antes de cargar. Vacío significa que lo decidan el nombre y los alias.
        var columnMaps = form["map"].Count == 0
            ? null
            : form["map"].Select(v => v ?? string.Empty).ToList();

        var outcome = await ingestion
            .PublishAsync(form.Files, form["label"].ToString(), user, assignments, columnMaps, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.Accepted || outcome.Version is null)
        {
            SecurityAudit.UploadRejected(audit, user, outcome.Errors.Count);
            return Results.Ok(UploadResponse.From(outcome));
        }

        // La carga cambio el portafolio: el cache de scores queda obsoleto, y se vuelve a
        // calcular en segundo plano antes de que alguien lo pida.
        portfolio.Invalidate();
        warmup.Request();

        SecurityAudit.UploadPublished(audit, user, outcome.Version.Id, outcome.Files.Count, outcome.ApplicationCount);

        return Results.Ok(UploadResponse.From(outcome));
    }
}
