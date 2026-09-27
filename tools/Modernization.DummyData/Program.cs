using System.Globalization;
using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;

// Genera el portafolio sintetico, lo exporta como CSV con los nombres de columna
// reales y reporta la cobertura de cada fuente. Ademas escribe la linea de tiempo: el
// mismo portafolio en los tres trimestres anteriores y en el actual, cada uno como una
// carga lista para leer (CSV + manifest.json), en "timeline" junto al directorio.
//
//   dotnet run --project tools/Modernization.DummyData -- [directorio] [semilla] [cantidad]

static void Line(FormattableString text) => Console.WriteLine(FormattableString.Invariant(text));

var directory = args.Length > 0 ? args[0] : Path.Combine("data", "dummy");
var seed = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : DummyPortfolio.DefaultSeed;
var count = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : DummyPortfolio.DefaultApplicationCount;

var provider = new DummySnapshotProvider(seed, count);
var snapshot = await provider.GetAllAsync().ConfigureAwait(false);
var files = CsvSourceExporter.Export(provider.Portfolio, directory);

Line($"Portafolio dummy — semilla {seed}, {snapshot.Applications.Count} aplicaciones");
Line($"Exportado a {Path.GetFullPath(directory)}");
Console.WriteLine();

Console.WriteLine("ARCHIVO                            FILAS   FUENTE                 COBERTURA   RECHAZOS");
foreach (var file in files)
{
    var status = snapshot.Sources.Single(s => s.SourceName == file.SourceName);
    var coverage = snapshot.CoverageOf(file.SourceName);
    Line($"{file.FileName,-33} {file.RowCount,6}   {file.SourceName,-20} {coverage,9:0.0}%   {status.RejectedCount,8}");
}

Console.WriteLine();

// Verificacion extremo a extremo: el EAI de referencia debe reproducir el BRD.
var engine = new ScoringEngine(RulesetCatalog.Default);
var reference = snapshot.Find(DummyPortfolio.ReferenceEaiCode);

if (reference is null)
{
    Line($"ERROR: falta el EAI de referencia {DummyPortfolio.ReferenceEaiCode}.");
    return 1;
}

var result = engine.Calculate(reference);
var ok = result.Score == 79.67m;

// Linea de tiempo: tres trimestres hacia atras mas el actual, del mas viejo al mas nuevo.
var timeline = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(directory)) ?? ".", "timeline");
Console.WriteLine();
Line($"Linea de tiempo en {timeline}");
Console.WriteLine("CARGA                    CAPTURA      SCORE   CURRENCY   FLAWS ABIERTOS (C+H)");

const int TimelineQuarters = 3;

for (var quarters = TimelineQuarters; quarters >= 0; quarters--)
{
    var portfolio = DummyTimeline.Rewind(provider.Portfolio, quarters);
    var at = portfolio.CapturedAt;
    var quarter = ((at.Month - 1) / 3) + 1;
    // Identificador fijo por trimestre: regenerar no crea cargas nuevas en el repositorio.
    var id = string.Create(CultureInfo.InvariantCulture, $"{at.UtcDateTime:yyyyMMdd-HHmmss}-t{quarter}{at.Year % 100:00}");
    var folder = Path.Combine(timeline, id);

    if (Directory.Exists(folder))
    {
        Directory.Delete(folder, recursive: true);
    }

    var exported = CsvSourceExporter.Export(portfolio, folder);

    new UploadManifest
    {
        Id = id,
        Label = string.Create(CultureInfo.InvariantCulture, $"Q{quarter} {at.Year} (sample timeline)"),
        CapturedAt = at,
        ApplicationCount = portfolio.Eai.Count,
        Files = [.. exported.Select(f => f.FileName)],
        UploadedBy = "sample-timeline",
    }.Write(folder);

    var assembled = await new DummySnapshotProvider(portfolio).GetAllAsync().ConfigureAwait(false);
    var quarterScores = assembled.Applications.Select(a => engine.Calculate(a).Score).ToList();
    var currencies = portfolio.Eai.Where(e => e.RawTechScore is > 0m).Select(e => e.RawTechScore!.Value).ToList();
    var openFlaws = portfolio.ScaFlaws.Count(f => f.FindingStatus == "Open" && f.SeverityDescription is "1-Critical" or "2-High");

    Line($"{id,-24} {at:yyyy-MM-dd}   {quarterScores.Average(),5:0.00}   {currencies.Average(),8:0.00}   {openFlaws,8}");
}


Line($"EAI {reference.EaiCode} — numerador {result.Numerator}, denominador {result.Denominator}, score {result.Score} (BRD 79,67) {(ok ? "OK" : "FALLA")}");

var scores = snapshot.Applications.Select(a => engine.Calculate(a).Score).ToList();
Line($"Portafolio — min {scores.Min():0.00} · promedio {scores.Average():0.00} · max {scores.Max():0.00}");

var denominators = snapshot.Applications.Select(a => engine.Calculate(a).Denominator).ToList();
Line($"Peso disponible — min {denominators.Min():0} · promedio {denominators.Average():0.0} · max {denominators.Max():0} de 100");

return ok ? 0 : 1;
