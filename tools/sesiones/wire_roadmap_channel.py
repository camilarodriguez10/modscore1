import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- Cliente
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs"), [
    ('''    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)''',
     '''    public Task<RoadmapPlanResponse?> GetRoadmapPlanAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
        => GetOrNullAsync<RoadmapPlanResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/roadmap",
            cancellationToken);

    public Task<PortfolioRoadmapResponse> GetPortfolioRoadmapAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<PortfolioRoadmapResponse>("/system/roadmap/portfolio", cancellationToken);

    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)'''),
])

# --- View models
append = '''

// ---------------------------------------------------------------- 05 Roadmap

/// <summary>
/// Plan futuro de una aplicacion. Es el requisito 6 del encargo: que le va a
/// pasar a esta aplicacion, por que pasos y con que resultado.
/// </summary>
public sealed record RoadmapPlanView(
    ApplicationHeaderView Application,
    string? Disposition,
    string? DispositionDefinition,
    string? Roadmap,
    string? RoadmapDefinition,
    string? LeverRationale,
    bool LeavesPortfolio,
    decimal BaselineScore,
    string BaselineBand,
    decimal BaselineWeight,
    decimal? ProjectedScore,
    string? ProjectedBand,
    decimal ProjectedDelta,
    IReadOnlyList<AppliedLeverView> DeliveredBy,
    IReadOnlyList<MilestoneView> Milestones,
    IReadOnlyList<AlertView> Alerts,
    DateOnly StartDate,
    DateOnly? CompletionDate,
    int TotalWeeks,
    string Outlook);

public sealed record MilestoneView(
    int Order,
    string Name,
    string Owner,
    int Weeks,
    DateOnly Start,
    DateOnly End,
    decimal SharePercent);

public sealed record AlertView(
    string RuleKey,
    string Severity,
    string Title,
    string Detail,
    string Evidence);

/// <summary>Pantalla 06 — el portafolio visto por su plan futuro.</summary>
public sealed record PortfolioRoadmapView(
    int TotalApplications,
    int WithoutDisposition,
    int LeavingPortfolio,
    IReadOnlyList<DispositionGroupView> Dispositions,
    IReadOnlyList<AlertSummaryView> Alerts);

/// <summary>
/// Una disposition con sus roadmaps dentro. MAPS solo define siete pares validos,
/// asi que agrupar por disposition dice mas que una matriz 3x7 casi vacia.
/// </summary>
public sealed record DispositionGroupView(
    string Disposition,
    string Name,
    string Definition,
    int Count,
    decimal SharePercent,
    decimal AverageScore,
    IReadOnlyList<RoadmapRowView> Roadmaps);

public sealed record RoadmapRowView(
    string Roadmap,
    string Name,
    string Definition,
    int Count,
    decimal AverageScore,
    decimal? AverageProjectedScore,
    decimal? AverageDelta,
    int TotalWeeks,
    bool RemovesFromPortfolio);

public sealed record AlertSummaryView(
    string RuleKey,
    string Severity,
    string Title,
    int Count,
    IReadOnlyList<string> SampleEaiCodes);
'''
p = os.path.join(root, r"src\channel\Modernization.ChannelApi\ViewModels\ScreenViewModels.cs")
s = io.open(p, encoding='utf-8').read()
assert "RoadmapPlanView" not in s
io.open(p, 'w', encoding='utf-8').write(s.rstrip() + append)
print("ok ScreenViewModels.cs")

# --- Endpoints
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Endpoints\ChannelEndpoints.cs"), [
    ('''        api.MapPost("/applications/{eaiCode}/whatif/target", PostTarget)
            .WithSummary("Conjunto minimo de palancas para alcanzar un score objetivo.");''',
     '''        api.MapPost("/applications/{eaiCode}/whatif/target", PostTarget)
            .WithSummary("Conjunto minimo de palancas para alcanzar un score objetivo.");

        api.MapGet("/applications/{eaiCode}/roadmap", GetRoadmapPlan)
            .WithSummary("Pantalla 05 — plan futuro: hitos, proyeccion y alertas de coherencia.");

        api.MapGet("/roadmap", GetPortfolioRoadmap)
            .WithSummary("Pantalla 06 — el portafolio visto por disposition y roadmap.");'''),

    ('''    private static async Task<IResult> GetWhatIfLab(''',
     '''    private static async Task<IResult> GetRoadmapPlan(
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
        CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposePortfolioAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetWhatIfLab('''),
])

edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Program.cs"), [
    ("builder.Services.AddScoped<WhatIfComposer>();",
     "builder.Services.AddScoped<WhatIfComposer>();\nbuilder.Services.AddScoped<RoadmapComposer>();"),
])
