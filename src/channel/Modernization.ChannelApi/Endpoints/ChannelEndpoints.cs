using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.Composition;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;
using Modernization.Security;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Endpoints;

/// <summary>
/// Endpoints del BFF. Uno por pantalla: cada uno devuelve todo lo que esa pantalla
/// necesita en una sola llamada, para que el front no encadene peticiones.
/// </summary>
public static class ChannelEndpoints
{
    private const int MaxPageSize = 100;
    private const int MaxFilterLength = 100;
    private const int MaxLeversPerRequest = 20;
    private const int MaxLeverKeyLength = 64;
    private const int MaxVersionIdLength = 64;
    private const int MaxAssignmentLength = 64;

    // Topes del mapping de columnas, iguales a los de la capa System.
    private const int MaxColumnMapLength = 32 * 1024;
    private const int MaxColumnMapEntries = 200;
    private const int MaxMappingFiles = 12;
    private const int MaxHeaderLineLength = 64 * 1024;
    private const int MaxHeaderNameLength = 256;
    private const int MaxMappingFileNameLength = 256;

    /// <summary>Dimensiones por las que se puede agrupar el plan del portafolio.</summary>
    internal static readonly string[] Groupings =
        ["portfolio", "application", "country", "cio", "lob", "disposition", "roadmap"];

    public static void MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Consultar exige el rol de lectura y algun alcance. Que aplicaciones entran
        // en ese alcance lo decide la capa System, sobre los datos (ADR 0012).
        var api = app.MapGroup("/api")
            .RequireAuthorization(ChannelPermissions.ReadPolicy)
            .WithTags("Channel");

        api.MapGet("/portfolio", GetPortfolio)
            .WithSummary("Pantalla 01 — portafolio con filtros, distribucion y agregados.");

        api.MapGet("/applications/{eaiCode}/overview", GetOverview)
            .WithSummary("Pantalla 02 — score global y tarjetas de pilar.");

        api.MapGet("/applications/{eaiCode}/explain", GetExplain)
            .WithSummary("Pantalla 03 — como se calculo: formula, waterfall, metricas y procedencia.");

        // El linaje expone estadisticas de archivos completos y los EAI de las filas
        // rechazadas: es de quien ve todo el portafolio.
        api.MapGet("/lineage", GetLineage)
            .RequireAuthorization(ChannelPermissions.FullPortfolioPolicy)
            .WithSummary("Pantalla 07 — estado y cobertura de cada fuente.");

        api.MapGet("/applications/{eaiCode}/whatif", GetWhatIfLab)
            .WithSummary("Pantalla 04 — catalogo de palancas con su ganancia y el techo alcanzable.");

        api.MapPost("/applications/{eaiCode}/whatif/scenario", PostScenario)
            .RequireRateLimiting(ChannelLimits.SimulationPolicy)
            .WithSummary("Simula un conjunto de palancas.");

        api.MapPost("/applications/{eaiCode}/whatif/target", PostTarget)
            .RequireRateLimiting(ChannelLimits.SimulationPolicy)
            .WithSummary("Conjunto minimo de palancas para alcanzar un score objetivo.");

        api.MapGet("/applications/{eaiCode}/roadmap", GetRoadmapPlan)
            .WithSummary("Pantalla 05 — plan futuro: hitos, proyeccion y alertas de coherencia. Con target, el plan hacia el objetivo del What-If.");

        api.MapGet("/roadmap", GetPortfolioRoadmap)
            .RequireFeature(f => f.PortfolioRoadmap)
            .WithSummary("Pantalla 06 — el portafolio visto por disposition y roadmap.");

        api.MapGet("/dashboard", GetDashboard)
            .WithSummary("Dashboard ejecutivo — las seis preguntas en una vista, con los mismos filtros.");

        api.MapGet("/code", GetCode)
            .RequireFeature(f => f.Code)
            .WithSummary("Pantalla Code — obsolescencia y riesgo segun los repositorios, con los mismos filtros.");

        api.MapGet("/code/applications/{eaiCode}", GetCodeApplication)
            .RequireFeature(f => f.Code)
            .WithSummary("Repositorios, componentes y dependencias de una aplicacion.");

        api.MapGet("/ingestion", GetIngestion)
            .WithSummary("Pantalla 08 — archivos esperados, cargas existentes y evolucion del portafolio.");

        api.MapGet("/applications/{eaiCode}/history", GetApplicationHistory)
            .WithSummary("Evolucion del score de una aplicacion a lo largo de las cargas.");

        // Publicar reemplaza el portafolio de toda la organizacion: rol propio,
        // alcance completo y una frecuencia mucho mas baja que el resto.
        api.MapPost("/ingestion/upload", PostUpload)
            .RequireAuthorization(ChannelPermissions.UploadPolicy)
            .RequireRateLimiting(ChannelLimits.UploadPolicy)
            .DisableAntiforgery()
            .WithSummary("Sube archivos, recalcula y publica una carga nueva.");

        // Leer las fuentes de sus bases es publicar una carga: el mismo rol, el mismo alcance
        // y el mismo limite de frecuencia (ADR 0037).
        api.MapPost("/ingestion/pull", PostPull)
            .RequireAuthorization(ChannelPermissions.UploadPolicy)
            .RequireRateLimiting(ChannelLimits.UploadPolicy)
            .WithSummary("Lee las fuentes de sus bases de datos y publica una carga nueva.");

        // Probar las conexiones no publica nada, pero describe la configuracion del despliegue
        // y abre conexiones a bases de la organizacion: lo hace quien puede publicar.
        api.MapPost("/ingestion/connectors/test", PostConnectorsTest)
            .RequireAuthorization(ChannelPermissions.UploadPolicy)
            .RequireRateLimiting(ChannelLimits.UploadPolicy)
            .WithSummary("Abre cada conexion a las bases de las fuentes y ejecuta la consulta minima.");

        // De donde sale cada campo de la base. Describe cargas completas, como el linaje.
        api.MapGet("/ingestion/mapping", GetMapping)
            .RequireAuthorization(ChannelPermissions.FullPortfolioPolicy)
            .WithSummary("Pestaña Mapping — archivo y columna que cargan cada campo de la base.");

        // El mapping se revisa antes de publicar y es parte de publicar: el mismo rol y el
        // mismo alcance. Solo recibe encabezados; ninguna fila de datos.
        api.MapPost("/ingestion/mapping/preview", PostMappingReview)
            .RequireAuthorization(ChannelPermissions.UploadPolicy)
            .RequireRateLimiting(ChannelLimits.SimulationPolicy)
            .WithSummary("Propone qué columna de cada archivo carga cada campo, antes de cargar.");

        // La sesion solo exige estar autenticado: la SPA la consulta para saber que
        // mostrar, incluido un "sin acceso" a quien no tiene permisos.
        app.MapGet("/api/session", GetSession)
            .RequireAuthorization()
            .WithTags("Channel")
            .WithSummary("Identidad, permisos y token CSRF de la sesion.");

        // El login no puede exigir estar autenticado.
        app.MapGet("/api/login", Login)
            .AllowAnonymous()
            .WithTags("Channel")
            .WithSummary("Inicia el flujo OIDC, o firma la sesion local en desarrollo.");

        app.MapPost("/api/logout", Logout)
            .RequireAuthorization()
            .WithTags("Channel")
            .WithSummary("Cierra la sesion y la elimina del lado servidor.");
    }

    private static async Task<IResult> Login(HttpContext context, IConfiguration configuration)
    {
        if (!ChannelAuthentication.UsesDevelopmentScheme)
        {
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/" });
        }

        // Con una sesion abierta no se firma otra encima: el manejador de cookies
        // renovaria la misma clave en lugar de emitir una nueva, que es exactamente
        // la fijacion de sesion. Con OIDC no ocurre: el callback del IdP es una
        // peticion cross-site y la cookie SameSite=Strict no viaja en ella, asi que
        // el inicio de sesion siempre crea una clave nueva (ADR 0011).
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return Results.NoContent();
        }

        // Firmar la sesion es un acto explicito de este endpoint, nunca un efecto
        // colateral de fallar la autorizacion en otro.
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ChannelAuthentication.CreateDevelopmentPrincipal(configuration)).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext context, ILoggerFactory loggers)
    {
        var subject = ChannelPermissions.SubjectOf(context.User);

        // Con la sesion en el servidor, esto la elimina: una copia de la cookie deja
        // de valer en el mismo instante.
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);

        // Un cookie __Host- solo se puede borrar con los mismos atributos con que se
        // emitio; sin Secure el navegador ignora el borrado.
        context.Response.Cookies.Delete(CsrfProtection.CookieName, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        SecurityAudit.SessionEnded(SecurityAudit.CreateLogger(loggers), subject);

        return Results.NoContent();
    }

    private static IResult GetSession(
        HttpContext context,
        ChannelPermissions permissions,
        Microsoft.Extensions.Options.IOptionsMonitor<ChannelFeatures> features)
    {
        var user = context.User;
        CsrfProtection.EnsureToken(context);

        return Results.Ok(new SessionView(
            user.FindFirst("name")?.Value ?? user.Identity?.Name ?? string.Empty,
            user.FindFirst("email")?.Value,
            [.. user.FindAll(ChannelPermissions.RoleClaimType).Select(c => c.Value)],
            user.Identity?.IsAuthenticated ?? false,
            new SessionPermissionsView(
                permissions.CanRead(user),
                permissions.HasFullPortfolio(user),
                permissions.CanUpload(user),
                permissions.CanAdministerRules(user)),
            new SessionFeaturesView(features.CurrentValue.Code, features.CurrentValue.PortfolioRoadmap)));
    }

    private static async Task<IResult> GetPortfolio(
        ScreenComposer composer,
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

        if (InvalidFilters(filters) is { } problem)
        {
            return problem;
        }

        var view = await composer.ComposePortfolioAsync(
            filters.ToQuery(page, pageSize),
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(view);
    }

    /// <summary>
    /// Los filtros que comparten todas las pantallas de consulta, validados en el
    /// borde. Un filtro solo acota dentro del alcance del usuario: la capa System lo
    /// aplica sobre un conjunto ya recortado, asi que ninguna combinacion de estos
    /// parametros amplia lo que se ve (ADR 0012).
    /// </summary>
    internal static IResult? InvalidFilters(FilterParameters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);

        var band = filters.Band;

        if (filters.TextValues.Any(f => f is { Length: > MaxFilterLength }))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "Invalid filter",
                $"Each filter accepts up to {MaxFilterLength} characters.");
        }

        // Un tramo inexistente devolveria cero resultados y haria pensar que el
        // portafolio esta vacio. Se rechaza en el borde, con las opciones validas.
        if (!ScoreBands.IsKnown(band))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-band",
                "Invalid score band",
                $"The score bands are: {string.Join(", ", ScoreBands.All.Select(b => b.Code))}.");
        }

        return null;
    }

    private static async Task<IResult> GetOverview(
        string eaiCode,
        ScreenComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var view = await composer.ComposeOverviewAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return view is null ? NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> GetExplain(
        string eaiCode,
        ScreenComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var view = await composer.ComposeExplainAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return view is null ? NotFound() : Results.Ok(view);
    }

    /// <summary>
    /// Linaje de una carga. El identificador se acota por forma antes de salir de
    /// aqui; que exista lo comprueba la capa System contra sus cargas publicadas.
    /// </summary>
    private static async Task<IResult> GetLineage(
        ScreenComposer composer,
        CancellationToken cancellationToken,
        string? version = null)
    {
        if (version is not null && !IsValidVersionId(version))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-version",
                "Invalid upload identifier",
                "The upload identifier is not well formed.");
        }

        var view = await composer.ComposeLineageAsync(version, cancellationToken).ConfigureAwait(false);

        return view is null
            ? ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "version-not-found",
                "Upload not found",
                "There is no upload with that identifier.")
            : Results.Ok(view);
    }

    private static async Task<IResult> GetIngestion(
        ClaimsPrincipal user,
        IngestionComposer composer,
        CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeAsync(user, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetApplicationHistory(
        string eaiCode,
        IngestionComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        return Results.Ok(await composer.ComposeHistoryAsync(eaiCode, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Carga de archivos. Es una mutacion, asi que pasa por la validacion CSRF
    /// como cualquier otra: el middleware la aplica antes de llegar aqui.
    /// </summary>
    private static async Task<IResult> PostUpload(
        HttpRequest request,
        IngestionComposer composer,
        CancellationToken cancellationToken)
    {
        // El unico endpoint con cuerpo grande amplia el limite de 1 MB aqui, y solo
        // aqui, antes de leer un byte.
        var bodyLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false })
        {
            bodyLimit.MaxRequestBodySize = ChannelLimits.UploadMaxRequestBytes;
        }

        if (!request.HasFormContentType)
        {
            return ApiProblem.Create(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported-media-type",
                "Invalid request format",
                "Send the files as multipart/form-data.");
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        catch (InvalidDataException)
        {
            // El limite multipart se reporta asi, igual que un cuerpo mal formado.
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-upload",
                "Invalid upload",
                $"The upload is not valid multipart content or exceeds {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.");
        }

        if (form.Files.Count == 0)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "empty-upload",
                "Empty upload",
                "Select at least one file to upload.");
        }

        if (!UploadLabel.TryNormalize(form["label"].ToString(), out var label, out var labelError))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-label",
                "Invalid label",
                labelError ?? "The label is not valid.");
        }

        // Asignacion manual de fuente, una por archivo y en su mismo orden. Una lista
        // desalineada asignaria el contenido de un archivo a la fuente de otro, asi
        // que se rechaza en vez de reenviarse a medias.
        var assign = form["assign"];

        if (assign.Count > 0 && assign.Count != form.Files.Count)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-assignment",
                "Invalid source assignment",
                "Send one source assignment per file, in the same order.");
        }

        if (assign.Any(value => value is { Length: > MaxAssignmentLength }))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-assignment",
                "Invalid source assignment",
                $"An assigned source accepts up to {MaxAssignmentLength} characters.");
        }

        var assignments = assign.Count == 0 ? null : assign.Select(v => v ?? string.Empty).ToList();

        // El mapping de columnas, uno por archivo y en su mismo orden, igual que la fuente.
        // Que sea un mapping valido lo decide la capa System contra el contrato.
        var map = form["map"];

        if (map.Count > 0 && (map.Count != form.Files.Count || assign.Count != form.Files.Count))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-mapping",
                "Invalid column mapping",
                "Send one column mapping and one source per file, in the same order.");
        }

        if (map.Any(value => value is { Length: > MaxColumnMapLength }))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-mapping",
                "Invalid column mapping",
                $"A column mapping accepts up to {MaxColumnMapLength} characters per file.");
        }

        var columnMaps = map.Count == 0 ? null : map.Select(v => v ?? string.Empty).ToList();

        var view = await composer.UploadAsync(form.Files, label, assignments, columnMaps, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(view);
    }

    private static async Task<IResult> PostPull(
        PullRequestBody? body,
        IngestionComposer composer,
        CancellationToken cancellationToken)
    {
        if (!UploadLabel.TryNormalize(body?.Label, out var label, out var labelError))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-label",
                "Invalid label",
                labelError ?? "The label is not valid.");
        }

        return Results.Ok(await composer.PullAsync(label, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> PostConnectorsTest(IngestionComposer composer, CancellationToken cancellationToken)
        => Results.Ok(await composer.TestConnectorsAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetMapping(IngestionComposer composer, CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeMappingAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> PostMappingReview(
        MappingReviewRequestBody? body,
        IngestionComposer composer,
        CancellationToken cancellationToken)
    {
        if (body?.Files is not { Count: > 0 } files
            || files.Count > MaxMappingFiles
            || files.Any(f => f is null || !IsValidMappingFile(f)))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-mapping-review",
                "Invalid mapping review",
                $"Send between 1 and {MaxMappingFiles} files, each with its header line (up to {MaxHeaderLineLength} characters) and no data rows.");
        }

        try
        {
            var view = await composer.PreviewMappingAsync(files, cancellationToken).ConfigureAwait(false);
            return Results.Ok(view);
        }
        catch (SystemApiException ex)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-mapping-review",
                "Invalid mapping review",
                ex.Message);
        }
    }

    /// <summary>
    /// Forma de lo que llega para revisar el mapping, acotada antes de reenviarlo. Solo se
    /// acepta la línea de encabezado: un salto de línea con algo detrás sería una fila de
    /// datos, que no tiene por qué salir del navegador.
    /// </summary>
    private static bool IsValidMappingFile(MappingReviewFileBody file)
    {
        var header = file.Header ?? string.Empty;
        var lineBreak = header.IndexOfAny(['\r', '\n']);

        return header.Length is > 0 and <= MaxHeaderLineLength
            && (lineBreak < 0 || header[lineBreak..].Trim().Length == 0)
            && (file.FileName?.Length ?? 0) <= MaxMappingFileNameLength
            && (file.AssignedFileName?.Length ?? 0) <= MaxAssignmentLength
            && (file.Columns?.Count ?? 0) <= MaxColumnMapEntries
            && (file.Columns ?? new Dictionary<string, string?>()).All(entry =>
                entry.Key.Length is > 0 and <= 128
                && (entry.Value is null || entry.Value.Length <= MaxHeaderNameLength));
    }

    private static async Task<IResult> GetRoadmapPlan(
        string eaiCode,
        RoadmapComposer composer,
        CancellationToken cancellationToken,
        string? target = null)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        // El objetivo llega del What-If Lab por la URL: un número de 20 a 100, como en el
        // laboratorio. Se lee como texto para que cualquier otra cosa sea un 400 explicado.
        decimal? goal = null;

        if (!string.IsNullOrEmpty(target))
        {
            if (target.Length > 6
                || !decimal.TryParse(target, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value)
                || value is < 20m or > 100m)
            {
                return ApiProblem.Create(
                    StatusCodes.Status400BadRequest,
                    "invalid-target",
                    "Invalid target",
                    "The target score must be a number between 20 and 100.");
            }

            goal = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        var view = await composer.ComposePlanAsync(eaiCode, goal, cancellationToken).ConfigureAwait(false);

        return view is null ? NotFound() : Results.Ok(view);
    }

    /// <summary>
    /// El dashboard ejecutivo. La capa Channel no calcula nada: valida los filtros,
    /// pregunta y entrega.
    /// </summary>
    private static async Task<IResult> GetDashboard(
        SystemApiClient system,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        string? axis = null)
    {
        if (InvalidFilters(filters) is { } problem)
        {
            return problem;
        }

        // El eje se valida en el borde, como los filtros: un eje desconocido es un 400
        // para quien lo pidio, no un fallo de la capa System que se vuelva un 500.
        if (axis is not null && !Modernization.Insights.DashboardOptions.Axes.Contains(axis, StringComparer.Ordinal))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-axis",
                "Invalid axis",
                $"The heatmap axes are: {string.Join(", ", Modernization.Insights.DashboardOptions.Axes)}.");
        }

        var dashboard = await system.GetDashboardAsync(filters.ToQuery(), axis, cancellationToken).ConfigureAwait(false);

        return Results.Ok(dashboard);
    }

    /// <summary>
    /// La pantalla Code. Como el dashboard, la capa Channel no calcula: valida los filtros,
    /// pregunta y entrega (ADR 0026).
    /// </summary>
    private static async Task<IResult> GetCode(
        SystemApiClient system,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken)
    {
        if (InvalidFilters(filters) is { } problem)
        {
            return problem;
        }

        var code = await system.GetCodeAsync(filters.ToQuery(), cancellationToken).ConfigureAwait(false);

        return Results.Ok(code);
    }

    private static async Task<IResult> GetCodeApplication(
        string eaiCode,
        SystemApiClient system,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var detail = await system.GetCodeApplicationAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return detail is null ? NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> GetPortfolioRoadmap(
        RoadmapComposer composer,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken,
        string? groupBy = null)
    {
        if (groupBy is not null && !Groupings.Contains(groupBy, StringComparer.OrdinalIgnoreCase))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-grouping",
                "Invalid grouping",
                $"The available groupings are: {string.Join(", ", Groupings)}.");
        }

        if (InvalidFilters(filters) is { } problem)
        {
            return problem;
        }

        var view = await composer.ComposePortfolioAsync(
            filters.ToQuery(),
            groupBy,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(view);
    }

    private static async Task<IResult> GetWhatIfLab(
        string eaiCode,
        WhatIfComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var view = await composer.ComposeLabAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return view is null ? NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> PostScenario(
        string eaiCode,
        ScenarioRequestBody body,
        WhatIfComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        if (body?.Levers is null || body.Levers.Count == 0)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "empty-scenario",
                "Empty scenario",
                "Choose at least one lever to simulate.");
        }

        if (body.Levers.Count > MaxLeversPerRequest || body.Levers.Any(l => !IsValidLeverKey(l?.Key)))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-scenario",
                "Invalid scenario",
                $"Each lever is identified by its catalog key, and a scenario accepts up to {MaxLeversPerRequest}.");
        }

        try
        {
            var view = await composer.ComposeScenarioAsync(
                eaiCode,
                [.. body.Levers.Select(l => new LeverRequest(l.Key, l.Parameter))],
                cancellationToken).ConfigureAwait(false);

            return view is null ? NotFound() : Results.Ok(view);
        }
        catch (SystemApiException ex)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-scenario",
                "Invalid scenario",
                ex.Message);
        }
    }

    private static async Task<IResult> PostTarget(
        string eaiCode,
        TargetRequestBody body,
        WhatIfComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        // La escala del modelo es 20-100: fuera de ahi no es una busqueda dificil,
        // es una entrada invalida. Se rechaza en el borde.
        if (body is null || body.TargetScore is < 20m or > 100m)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-target",
                "Invalid target",
                "The target score must be between 20 and 100.");
        }

        try
        {
            var view = await composer.ComposeTargetAsync(eaiCode, body.TargetScore, cancellationToken)
                .ConfigureAwait(false);

            return view is null ? NotFound() : Results.Ok(view);
        }
        catch (SystemApiException ex)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-target",
                "Invalid target",
                ex.Message);
        }
    }

    /// <summary>
    /// Identificador de carga por lista blanca. En el adaptador de archivos una
    /// version es un directorio, asi que una cadena con separadores de ruta no tiene
    /// por que llegar siquiera a la capa System.
    /// </summary>
    private static bool IsValidVersionId(string value)
    {
        if (value.Length is 0 or > MaxVersionIdLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Clave de palanca por lista blanca. Una clave con otros caracteres no existe
    /// en el catalogo, asi que no hay motivo para enviarla a la capa System.
    /// </summary>
    private static bool IsValidLeverKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLeverKeyLength)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// No se hace eco del valor recibido. El cliente ya sabe que pidio, y devolver
    /// entrada del usuario dentro de un mensaje de error es como nacen los XSS
    /// reflejados el dia que alguien renderiza ese texto como HTML.
    /// </summary>
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

    private static IResult TooLarge() => ApiProblem.Create(
        StatusCodes.Status413PayloadTooLarge,
        "upload-too-large",
        "Upload too large",
        $"The upload exceeds the maximum of {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.");
}

/// <summary>Pedido de lectura de las bases: solo la etiqueta de la carga, opcional.</summary>
public sealed record PullRequestBody(string? Label);

/// <summary>
/// Revisión del mapping antes de cargar: el encabezado de cada archivo, nunca sus filas.
/// </summary>
public sealed record MappingReviewRequestBody(IReadOnlyList<MappingReviewFileBody>? Files);

public sealed record MappingReviewFileBody(
    string? FileName,
    string? Header,
    string? AssignedFileName,
    IReadOnlyDictionary<string, string?>? Columns);

/// <summary>Cuerpo de la peticion de escenario que envia la SPA.</summary>
public sealed record ScenarioRequestBody(IReadOnlyList<LeverSelectionBody> Levers);

public sealed record LeverSelectionBody(string Key, int? Parameter);

/// <summary>Cuerpo de la peticion de score objetivo.</summary>
public sealed record TargetRequestBody(decimal TargetScore);
