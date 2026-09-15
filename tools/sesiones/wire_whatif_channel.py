import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- View models de la pantalla 04
append = '''

// ------------------------------------------------------------- 04 What-If Lab

/// <summary>
/// Pantalla del What-If Lab. Es el requisito 5 del encargo: que sube el score y
/// cuanto, expresado en acciones que un equipo puede ejecutar de verdad.
/// </summary>
public sealed record WhatIfLabView(
    ApplicationHeaderView Application,
    decimal BaselineScore,
    decimal BaselineWeight,
    decimal CeilingScore,
    decimal Headroom,
    IReadOnlyList<LeverView> QuickWins,
    IReadOnlyList<LeverView> Actionable,
    IReadOnlyList<LeverView> AlreadyAchieved,
    IReadOnlyList<LeverView> NotActionable);

public sealed record LeverView(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter,
    decimal Delta,
    decimal ProjectedScore,
    decimal WeightAdded,
    bool IsAlreadyAchieved,
    string? NotActionableReason);

/// <summary>Resultado de simular un conjunto de palancas.</summary>
public sealed record ScenarioView(
    decimal BaselineScore,
    decimal ProjectedScore,
    decimal Delta,
    decimal BaselineWeight,
    decimal ProjectedWeight,
    decimal SumOfIndividualDeltas,
    decimal InteractionEffect,
    IReadOnlyList<AppliedLeverView> Applied,
    IReadOnlyList<LeverView> IndividualEffects,
    string? InteractionNote);

public sealed record AppliedLeverView(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    int? Parameter);

/// <summary>Resultado de pedir un score objetivo.</summary>
public sealed record TargetView(
    decimal Target,
    decimal BaselineScore,
    decimal CeilingScore,
    bool IsReachable,
    bool IsAlreadyMet,
    ScenarioView? Scenario,
    string Explanation);
'''
p = os.path.join(root, r"src\channel\Modernization.ChannelApi\ViewModels\ScreenViewModels.cs")
s = io.open(p, encoding='utf-8').read()
assert "WhatIfLabView" not in s
io.open(p, 'w', encoding='utf-8').write(s.rstrip() + append)
print("ok ScreenViewModels.cs")

# --- Cliente: los tres endpoints nuevos
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs"), [
    ("""    public Task<RulesetResponse> GetRulesetAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<RulesetResponse>("/system/ruleset", cancellationToken);""",
     """    public Task<RulesetResponse> GetRulesetAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<RulesetResponse>("/system/ruleset", cancellationToken);

    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)
        => GetOrNullAsync<LeverCatalogResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/levers",
            cancellationToken);

    public Task<ScenarioResponse?> PostScenarioAsync(
        string eaiCode,
        IReadOnlyList<LeverRequest> levers,
        CancellationToken cancellationToken = default)
        => PostAsync<ScenarioRequest, ScenarioResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/scenario",
            new ScenarioRequest(levers),
            cancellationToken);

    public Task<TargetResponse?> PostTargetAsync(
        string eaiCode,
        decimal targetScore,
        CancellationToken cancellationToken = default)
        => PostAsync<TargetRequest, TargetResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/target",
            new TargetRequest(targetScore),
            cancellationToken);"""),

    ("""    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken cancellationToken)""",
     """    /// <summary>
    /// Un 400 de la capa System es un escenario invalido, no un fallo del servicio:
    /// se propaga con su detalle para que el usuario sepa que corregir.
    /// </summary>
    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var response = await http.PostAsJsonAsync(url, body, Json, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            var problem = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new SystemApiException($"La capa System rechazo la peticion: {problem}");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken cancellationToken)"""),
])

# --- Endpoints del BFF
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Endpoints\ChannelEndpoints.cs"), [
    ("""        api.MapGet("/lineage", GetLineage)
            .WithSummary("Pantalla 07 — estado y cobertura de cada fuente.");""",
     """        api.MapGet("/lineage", GetLineage)
            .WithSummary("Pantalla 07 — estado y cobertura de cada fuente.");

        api.MapGet("/applications/{eaiCode}/whatif", GetWhatIfLab)
            .WithSummary("Pantalla 04 — catalogo de palancas con su ganancia y el techo alcanzable.");

        api.MapPost("/applications/{eaiCode}/whatif/scenario", PostScenario)
            .WithSummary("Simula un conjunto de palancas.");

        api.MapPost("/applications/{eaiCode}/whatif/target", PostTarget)
            .WithSummary("Conjunto minimo de palancas para alcanzar un score objetivo.");"""),

    ("""    private static async Task<IResult> GetLineage(ScreenComposer composer, CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeLineageAsync(cancellationToken).ConfigureAwait(false));""",

     """    private static async Task<IResult> GetLineage(ScreenComposer composer, CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeLineageAsync(cancellationToken).ConfigureAwait(false));

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
            return Results.Problem(
                detail: "Elige al menos una palanca para simular.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Escenario vacio");
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
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Escenario invalido");
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
            return Results.Problem(
                detail: "El score objetivo debe estar entre 20 y 100.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Objetivo invalido");
        }

        try
        {
            var view = await composer.ComposeTargetAsync(eaiCode, body.TargetScore, cancellationToken)
                .ConfigureAwait(false);

            return view is null ? NotFound() : Results.Ok(view);
        }
        catch (SystemApiException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Objetivo invalido");
        }
    }"""),

    ("""    private static IResult InvalidEaiCode() => Results.Problem(
        detail: "Un EAI code es una secuencia de hasta 10 digitos.",
        statusCode: StatusCodes.Status400BadRequest,
        title: "EAI code invalido");
}""",

     """    private static IResult InvalidEaiCode() => Results.Problem(
        detail: "Un EAI code es una secuencia de hasta 10 digitos.",
        statusCode: StatusCodes.Status400BadRequest,
        title: "EAI code invalido");
}

/// <summary>Cuerpo de la peticion de escenario que envia la SPA.</summary>
public sealed record ScenarioRequestBody(IReadOnlyList<LeverSelectionBody> Levers);

public sealed record LeverSelectionBody(string Key, int? Parameter);

/// <summary>Cuerpo de la peticion de score objetivo.</summary>
public sealed record TargetRequestBody(decimal TargetScore);"""),
])

# --- Registro en el contenedor
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Program.cs"), [
    ("builder.Services.AddScoped<ScreenComposer>();",
     "builder.Services.AddScoped<ScreenComposer>();\nbuilder.Services.AddScoped<WhatIfComposer>();"),
])
