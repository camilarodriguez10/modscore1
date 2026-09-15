import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- Cliente: historia, versiones y reenvio de la carga
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs"), [
    ("""    public Task<RoadmapPlanResponse?> GetRoadmapPlanAsync(""",
     """    public Task<IReadOnlyList<VersionResponse>> GetVersionsAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<VersionResponse>>("/system/versions", cancellationToken);

    public Task<IReadOnlyList<HistoryPointResponse>> GetApplicationHistoryAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<HistoryPointResponse>>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/history",
            cancellationToken);

    public Task<IReadOnlyList<HistoryPointResponse>> GetPortfolioHistoryAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<HistoryPointResponse>>("/system/history/portfolio", cancellationToken);

    public Task<IReadOnlyList<ExpectedFileResponse>> GetExpectedFilesAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<ExpectedFileResponse>>(
            "/system/ingestion/expected-files",
            cancellationToken);

    /// <summary>
    /// Reenvia la carga a la capa System como multipart. El contenido no se
    /// bufferiza: se copia stream a stream.
    /// </summary>
    public async Task<UploadResponse> UploadAsync(
        MultipartFormDataContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var response = await http.PostAsync(
            new Uri("/system/ingestion/upload", UriKind.Relative),
            content,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UploadResponse>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new SystemApiException("La capa System no devolvio resultado de la carga.");
    }

    public Task<RoadmapPlanResponse?> GetRoadmapPlanAsync("""),
])

# --- View models
append = '''

// ------------------------------------------------------------- 08 Cargas

/// <summary>Una carga de datos identificada en el tiempo.</summary>
public sealed record VersionView(
    string Id,
    string Label,
    DateTimeOffset CapturedAt,
    int ApplicationCount,
    bool IsCurrent);

/// <summary>Punto de la evolucion del score.</summary>
public sealed record HistoryPointView(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? AvailableWeight,
    decimal? DeltaFromPrevious);

/// <summary>
/// Pantalla de cargas: que archivos se esperan, que versiones existen y como
/// evoluciona el portafolio.
/// </summary>
public sealed record IngestionView(
    bool UploadEnabled,
    string Provider,
    string? DisabledReason,
    IReadOnlyList<ExpectedFileView> ExpectedFiles,
    IReadOnlyList<VersionView> Versions,
    IReadOnlyList<HistoryPointView> PortfolioHistory,
    long MaxFileBytes,
    long MaxTotalBytes);

public sealed record ExpectedFileView(
    string Source,
    string FileName,
    string? Description,
    string KeyColumn,
    IReadOnlyList<string> RequiredColumns,
    int OptionalColumnCount);

/// <summary>Resultado de una carga, archivo por archivo.</summary>
public sealed record UploadResultView(
    bool Accepted,
    VersionView? Version,
    int ApplicationCount,
    IReadOnlyList<UploadFileView> Files,
    IReadOnlyList<string> Errors,
    string Summary);

public sealed record UploadFileView(
    string FileName,
    string? CanonicalFileName,
    string Source,
    long Bytes,
    int RowCount,
    int RejectedCount,
    string? Error);
'''
p = os.path.join(root, r"src\channel\Modernization.ChannelApi\ViewModels\ScreenViewModels.cs")
s = io.open(p, encoding='utf-8').read()
assert "IngestionView" not in s
io.open(p, 'w', encoding='utf-8').write(s.rstrip() + append)
print("ok ScreenViewModels.cs")

# --- Endpoints del BFF
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Endpoints\ChannelEndpoints.cs"), [
    ("""        api.MapGet("/roadmap", GetPortfolioRoadmap)
            .WithSummary("Pantalla 06 — el portafolio visto por disposition y roadmap.");""",
     """        api.MapGet("/roadmap", GetPortfolioRoadmap)
            .WithSummary("Pantalla 06 — el portafolio visto por disposition y roadmap.");

        api.MapGet("/ingestion", GetIngestion)
            .WithSummary("Pantalla 08 — archivos esperados, cargas existentes y evolucion del portafolio.");

        api.MapGet("/applications/{eaiCode}/history", GetApplicationHistory)
            .WithSummary("Evolucion del score de una aplicacion a lo largo de las cargas.");

        api.MapPost("/ingestion/upload", PostUpload)
            .WithSummary("Sube archivos, recalcula y publica una carga nueva.")
            .DisableAntiforgery();"""),

    ("""    private static async Task<IResult> GetRoadmapPlan(""",
     """    private static async Task<IResult> GetIngestion(
        IngestionComposer composer,
        CancellationToken cancellationToken)
        => Results.Ok(await composer.ComposeAsync(cancellationToken).ConfigureAwait(false));

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
        if (!request.HasFormContentType)
        {
            return Results.Problem(
                detail: "Envia los archivos como multipart/form-data.",
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Formato de peticion invalido");
        }

        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);

        if (form.Files.Count == 0)
        {
            return Results.Problem(
                detail: "Selecciona al menos un archivo para cargar.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Carga vacia");
        }

        var view = await composer.UploadAsync(form.Files, form["label"].ToString(), cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(view);
    }

    private static async Task<IResult> GetRoadmapPlan("""),
])

edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Program.cs"), [
    ("builder.Services.AddScoped<RoadmapComposer>();",
     "builder.Services.AddScoped<RoadmapComposer>();\nbuilder.Services.AddScoped<IngestionComposer>();"),

    ("""builder.Services.AddProblemDetails();""",
     """// Limite duro de tamano en el borde, antes de que el cuerpo llegue al handler.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 256L * 1024 * 1024;
    options.ValueLengthLimit = 8 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});

builder.Services.AddProblemDetails();"""),
])
