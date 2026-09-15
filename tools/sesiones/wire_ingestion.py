import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- PortfolioService: historia + invalidacion de cache
edit(os.path.join(root, r"src\system\Modernization.SystemApi\Services\PortfolioService.cs"), [
    ("""    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, ScoreBreakdown> _scores = new(StringComparer.Ordinal);
    private SnapshotSet? _snapshot;""",
     """    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, ScoreBreakdown> _scores = new(StringComparer.Ordinal);
    private SnapshotSet? _snapshot;"""),

    ("""    public void Dispose() => _gate.Dispose();""",
     """    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// Descarta el portafolio cacheado. Se llama tras publicar una carga: los
    /// scores calculados corresponden a datos que ya no son los vigentes.
    /// </summary>
    public void Invalidate()
    {
        _snapshot = null;
        _scores.Clear();
    }

    /// <summary>Versiones disponibles, de la mas reciente a la mas antigua.</summary>
    public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
        => provider.GetVersionsAsync(cancellationToken);

    /// <summary>
    /// Evolucion del score de una aplicacion a lo largo de las cargas. Incluye el
    /// peso disponible de cada version: un salto de score con el mismo peso es
    /// merito real, y con mas peso puede ser solo mas evidencia.
    /// </summary>
    public async Task<IReadOnlyList<ScoreHistoryPoint>> GetHistoryAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
    {
        var versions = await provider.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var points = new List<ScoreHistoryPoint>(versions.Count);

        foreach (var version in versions.OrderBy(v => v.CapturedAt))
        {
            var snapshot = await provider.GetAllAsync(version.Id, cancellationToken).ConfigureAwait(false);
            var application = snapshot.Find(eaiCode);

            decimal? score = null;
            decimal? weight = null;

            if (application is not null)
            {
                try
                {
                    var breakdown = engine.Calculate(application);
                    score = breakdown.EffectiveScore;
                    weight = breakdown.Denominator;
                }
                catch (RulesetException)
                {
                    // Un dato que no encaja en el ruleset deja el punto vacio en
                    // vez de romper la serie completa.
                }
            }

            points.Add(new ScoreHistoryPoint
            {
                VersionId = version.Id,
                Label = version.Label,
                CapturedAt = version.CapturedAt,
                Score = score,
                AvailableWeight = weight,
            });
        }

        return points;
    }

    /// <summary>Score promedio del portafolio en cada carga.</summary>
    public async Task<IReadOnlyList<ScoreHistoryPoint>> GetPortfolioHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = await provider.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var points = new List<ScoreHistoryPoint>(versions.Count);

        foreach (var version in versions.OrderBy(v => v.CapturedAt))
        {
            var snapshot = await provider.GetAllAsync(version.Id, cancellationToken).ConfigureAwait(false);
            var scores = new List<decimal>(snapshot.Applications.Count);
            var weights = new List<decimal>(snapshot.Applications.Count);

            foreach (var application in snapshot.Applications)
            {
                try
                {
                    var breakdown = engine.Calculate(application);
                    scores.Add(breakdown.EffectiveScore);
                    weights.Add(breakdown.Denominator);
                }
                catch (RulesetException)
                {
                    // Se omite del promedio, como en el resto de los agregados.
                }
            }

            points.Add(new ScoreHistoryPoint
            {
                VersionId = version.Id,
                Label = version.Label,
                CapturedAt = version.CapturedAt,
                Score = scores.Count == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
                AvailableWeight = weights.Count == 0 ? null : Math.Round(weights.Average(), 1, MidpointRounding.AwayFromZero),
            });
        }

        return points;
    }"""),
])

# --- Contratos de version e historia
append = '''

/// <summary>Una carga de datos identificada en el tiempo.</summary>
public sealed record VersionResponse(
    string Id,
    string Label,
    DateTimeOffset CapturedAt,
    string Provider,
    int ApplicationCount,
    bool IsCurrent)
{
    public static VersionResponse From(Domain.Sources.SnapshotVersion v) =>
        new(v.Id, v.Label, v.CapturedAt, v.Provider, v.ApplicationCount, v.IsCurrent);
}

/// <summary>Punto de la serie temporal de score.</summary>
public sealed record HistoryPointResponse(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? AvailableWeight)
{
    public static HistoryPointResponse From(Domain.Sources.ScoreHistoryPoint p) =>
        new(p.VersionId, p.Label, p.CapturedAt, p.Score, p.AvailableWeight);
}

/// <summary>Resultado de una carga, archivo por archivo.</summary>
public sealed record UploadResponse(
    bool Accepted,
    VersionResponse? Version,
    int ApplicationCount,
    IReadOnlyList<UploadFileResponse> Files,
    IReadOnlyList<string> Errors)
{
    public static UploadResponse From(Sources.Files.UploadService.UploadOutcome o) => new(
        o.Accepted,
        o.Version is null ? null : VersionResponse.From(o.Version),
        o.ApplicationCount,
        [.. o.Files.Select(f => new UploadFileResponse(
            f.ClientFileName, f.CanonicalFileName, f.Source, f.Bytes, f.RowCount, f.RejectedCount, f.Error))],
        o.Errors);
}

public sealed record UploadFileResponse(
    string ClientFileName,
    string? CanonicalFileName,
    string Source,
    long Bytes,
    int RowCount,
    int RejectedCount,
    string? Error);
'''
p = os.path.join(root, r"src\system\Modernization.SystemApi\Contracts\SystemContracts.cs")
s = io.open(p, encoding='utf-8').read()
assert "VersionResponse" not in s
io.open(p, 'w', encoding='utf-8').write(s.rstrip() + append)
print("ok SystemContracts.cs")

# --- Program: registrar el adaptador de archivos y la carga
edit(os.path.join(root, r"src\system\Modernization.SystemApi\Program.cs"), [
    ("using Modernization.Sources.Dummy;",
     "using Modernization.Sources.Dummy;\nusing Modernization.Sources.Files;"),

    ("""    case "File":
        throw new InvalidOperationException("El adaptador de archivos llega en la Fase 07.");""",
     """    case "File":
        var dataDirectory = builder.Configuration["DataSource:Directory"]
            ?? throw new InvalidOperationException(
                "El adaptador de archivos necesita 'DataSource:Directory'.");

        builder.Services.AddSingleton<IApplicationSnapshotProvider>(
            _ => new FileSnapshotProvider(dataDirectory));
        builder.Services.AddSingleton(_ => new UploadService(dataDirectory));
        break;"""),

    ("builder.Services.AddSingleton<PortfolioService>();",
     """builder.Services.AddSingleton<PortfolioService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => SourceSchemaCatalog.Default);

// La carga solo existe con el adaptador de archivos. Con Dummy el servicio se
// registra igual pero responde que no admite cargas, en lugar de fallar al
// resolver dependencias.
builder.Services.AddSingleton(sp => new IngestionService(
    sp.GetService<UploadService>(),
    sp.GetRequiredService<SourceSchemaCatalog>(),
    sp.GetRequiredService<TimeProvider>()));"""),

    ("app.MapRoadmapEndpoints();", "app.MapRoadmapEndpoints();\napp.MapIngestionEndpoints();"),

    ("""builder.Services.AddOpenApi();""",
     """// Limite duro de tamano antes de leer un solo byte del cuerpo.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = UploadService.MaxTotalBytes;
    options.ValueLengthLimit = 8 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});

builder.Services.AddOpenApi();"""),
])
