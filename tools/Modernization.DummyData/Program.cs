using System.Globalization;
using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;

// Genera el portafolio sintetico, lo exporta como CSV con los nombres de columna
// reales y reporta la cobertura de cada fuente.
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

Line($"EAI {reference.EaiCode} — numerador {result.Numerator}, denominador {result.Denominator}, score {result.Score} (BRD 79,67) {(ok ? "OK" : "FALLA")}");

var scores = snapshot.Applications.Select(a => engine.Calculate(a).Score).ToList();
Line($"Portafolio — min {scores.Min():0.00} · promedio {scores.Average():0.00} · max {scores.Max():0.00}");

var denominators = snapshot.Applications.Select(a => engine.Calculate(a).Denominator).ToList();
Line($"Peso disponible — min {denominators.Min():0} · promedio {denominators.Average():0.0} · max {denominators.Max():0} de 100");

return ok ? 0 : 1;
