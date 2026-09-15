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

    /// <summary>Dimensiones por las que se puede agrupar el plan del portafolio.</summary>
    private static readonly string[] Groupings =
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
            .WithSummary("Pantalla 05 — plan futuro: hitos, proyeccion y alertas de coherencia.");

        api.MapGet("/roadmap", GetPortfolioRoadmap)
            .WithSummary("Pantalla 06 — el portafolio visto por disposition y roadmap.");

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

    private static IResult GetSession(HttpContext context, ChannelPermissions permissions)
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
                permissions.CanAdministerRules(user))));
    }

    private static async Task<IResult> GetPortfolio(
        ScreenComposer composer,
        CancellationToken cancellationToken,
        string? lob = null,
        string? country = null,
        string? cio = null,
        string? disposition = null,
        string? roadmap = null,
        string? band = null,
        bool? criticalOnly = null,
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

        if (InvalidFilters(lob, country, cio, disposition, roadmap, band) is { } problem)
        {
            return problem;
        }

        var view = await composer.ComposePortfolioAsync(
            new ApplicationQuery
            {
                Lob = lob,
                Country = country,
                Cio = cio,
                Disposition = disposition,
                Roadmap = roadmap,
                Band = band,
                CriticalOnly = criticalOnly,
                Page = page,
                PageSize = pageSize,
            },
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(view);
    }

    /// <summary>
    /// Los filtros que comparten todas las pantallas de consulta, validados en el
    /// borde. Un filtro solo acota dentro del alcance del usuario: la capa System lo
    /// aplica sobre un conjunto ya recortado, asi que ninguna combinacion de estos
    /// parametros amplia lo que se ve (ADR 0012).
    /// </summary>
    private static IResult? InvalidFilters(
        string? lob,
        string? country,
        string? cio,
        string? disposition,
        string? roadmap,
        string? band)
    {
        if (new[] { lob, country, cio, disposition, roadmap, band }.Any(f => f is { Length: > MaxFilterLength }))
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

        var view = await composer.UploadAsync(form.Files, label, assignments, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(view);
    }

    private static async Task<IResult> GetRoadmapPlan(
        string eaiCode,
        RoadmapComposer composer,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var view = await composer.ComposePlanAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return view is null ? NotFound() : Results.Ok(view);
    }

    private static async Task<IResult> GetPortfolioRoadmap(
        RoadmapComposer composer,
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
        if (groupBy is not null && !Groupings.Contains(groupBy, StringComparer.OrdinalIgnoreCase))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "invalid-grouping",
                "Invalid grouping",
                $"The available groupings are: {string.Join(", ", Groupings)}.");
        }

        if (InvalidFilters(lob, country, cio, disposition, roadmap, band) is { } problem)
        {
            return problem;
        }

        var view = await composer.ComposePortfolioAsync(
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

/// <summary>Cuerpo de la peticion de escenario que envia la SPA.</summary>
public sealed record ScenarioRequestBody(IReadOnlyList<LeverSelectionBody> Levers);

public sealed record LeverSelectionBody(string Key, int? Parameter);

/// <summary>Cuerpo de la peticion de score objetivo.</summary>
public sealed record TargetRequestBody(decimal TargetScore);
