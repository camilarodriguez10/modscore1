using System.Globalization;
using System.Text;
using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;
using Modernization.Domain;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Fase 10, punto 6: un archivo se reconoce por su encabezado, no por su nombre.
/// </summary>
public sealed class SourceFileMatcherTests
{
    private readonly SourceFileMatcher _matcher = new(SourceSchemaCatalog.Default);

    /// <summary>Cada export real, con un nombre que no se parece en nada al canonico.</summary>
    [Theory]
    [InlineData("eai_master.csv")]
    [InlineData("architectural_value_by_eai.csv")]
    [InlineData("product_strategy.csv")]
    [InlineData("tech_debt.csv")]
    [InlineData("veracode_sca_open_flaws.csv")]
    [InlineData("oss_score_by_eai.csv")]
    [InlineData("monitoreo.csv")]
    [InlineData("apim_onboarded_eais.csv")]
    public void EveryContractIsRecognisedByItsHeaderAlone(string canonical)
    {
        var schema = SourceSchemaCatalog.Default.Require(canonical);
        var headers = schema.Columns.Select(c => c.Name).ToList();

        var match = _matcher.Match(headers, "export (3).csv", assignedFileName: null);

        Assert.Equal(canonical, match.Schema?.FileName);
        Assert.Null(match.Problem);
    }

    /// <summary>
    /// Los alias cuentan igual que el nombre: un export de Power BI con "EAI Code" y
    /// "Country" sigue siendo el maestro EAI si trae el resto de sus columnas.
    /// </summary>
    [Fact]
    public void AliasesIdentifyTheSourceToo()
    {
        var headers = new[] { "EAI Code", "Application Name", "LOB", "CIO", "Country", "Region", "Roadmap" };

        var match = _matcher.Match(headers, "inventario.csv", assignedFileName: null);

        Assert.Equal("eai_master.csv", match.Schema?.FileName);
    }

    /// <summary>
    /// Un encabezado que encaja igual de bien en dos contratos no se decide por el
    /// orden del catalogo: se devuelve lo leido y las dos opciones, para preguntar.
    /// </summary>
    [Fact]
    public void AnAmbiguousHeaderIsReportedWithWhatWasReadAndTheOptions()
    {
        var match = _matcher.Match(["EAI", "Country"], "datos.csv", assignedFileName: null);

        Assert.Null(match.Schema);
        Assert.Equal(["EAI", "Country"], match.DetectedColumns);
        Assert.Contains("eai_master.csv", match.Candidates);
        Assert.Contains("apim_onboarded_eais.csv", match.Candidates);
        Assert.Equal(NoticeCodes.MatchAmbiguous, match.Problem?.Code);
    }

    /// <summary>Ante un empate, el nombre desempata entre los que empataron.</summary>
    [Fact]
    public void TheNameBreaksATieOnlyAmongTheTiedContracts()
    {
        var match = _matcher.Match(["EAI", "Country"], "0809 apim_onboarded_eais.csv", assignedFileName: null);

        Assert.Equal("apim_onboarded_eais.csv", match.Schema?.FileName);
    }

    [Fact]
    public void AnExplicitAssignmentWinsOverTheHeader()
    {
        var match = _matcher.Match(["EAI", "Country"], "datos.csv", assignedFileName: "apim_onboarded_eais.csv");

        Assert.Equal("apim_onboarded_eais.csv", match.Schema?.FileName);
    }

    /// <summary>La asignacion se valida contra la lista blanca; nunca se vuelve una ruta.</summary>
    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("inventado.csv")]
    public void AnAssignmentOutsideTheCatalogIsRejected(string assigned)
    {
        var match = _matcher.Match(["EAI", "Country"], "datos.csv", assigned);

        Assert.Null(match.Schema);
        Assert.Equal(NoticeCodes.MatchAssignmentUnknown, match.Problem?.Code);
    }

    /// <summary>
    /// Lo leido vuelve limpio y acotado: es contenido del cliente que termina en una
    /// pantalla y en un log.
    /// </summary>
    [Fact]
    public void DetectedColumnsAreCleanedAndBounded()
    {
        var headers = Enumerable.Range(0, 100).Select(i => $"col{i}[31m" + new string('x', 200)).ToList();

        var match = _matcher.Match(headers, "raro.csv", assignedFileName: null);

        Assert.Null(match.Schema);
        Assert.Equal(40, match.DetectedColumns.Count);
        Assert.All(match.DetectedColumns, column =>
        {
            Assert.True(column.Length <= 64);
            Assert.DoesNotContain('', column);
        });
    }

    [Fact]
    public void AFileWithoutHeaderIsUnresolvedNotAServiceFailure()
    {
        using var reader = new StringReader(string.Empty);

        var match = _matcher.MatchReader(reader, "vacio.csv", assignedFileName: null);

        Assert.Null(match.Schema);
        Assert.Equal(NoticeCodes.CsvNoHeader, match.Problem?.Code);
    }
}

/// <summary>Los dos publicadores aceptan una carga completa con nombres cualquiera.</summary>
public sealed class RenamedUploadTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-renamed-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private string ExportRenamed()
    {
        var directory = Path.Combine(_root, "export");
        CsvSourceExporter.Export(DummyPortfolio.Generate(applicationCount: 40), directory);
        return directory;
    }

    private static List<IncomingSourceFile> Renamed(string directory)
        => [.. Directory.EnumerateFiles(directory, "*.csv")
            .Order(StringComparer.Ordinal)
            .Select((path, index) => new IncomingSourceFile(
                string.Create(CultureInfo.InvariantCulture, $"export-sin-nombre-{index}.csv"),
                new FileInfo(path).Length,
                () => File.OpenRead(path)))];

    /// <summary>
    /// Criterio de la Fase 10: los exports suben sin renombrar. En disco quedan con el
    /// nombre canonico —nunca el del cliente— y el caso de referencia sigue dando 80,70 (79,67 con las bandas del BRD, ADR 0032).
    /// </summary>
    [Fact]
    public async Task TheFileAdapterPublishesRenamedExportsUnderCanonicalNames()
    {
        var versions = Path.Combine(_root, "versions");
        using var service = new UploadService(versions);

        var outcome = await service.PublishAsync(Renamed(ExportRenamed()), "renombrados", "tests", DateTimeOffset.UtcNow);

        Assert.True(outcome.Accepted, string.Join(" · ", outcome.Errors));
        Assert.All(outcome.Files, f => Assert.NotNull(f.CanonicalFileName));

        var written = Directory.EnumerateFiles(Path.Combine(versions, outcome.Version!.Id), "*.csv")
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(SourceSchemaCatalog.Default.ExpectedFileNames.Order(), written.Order());
        Assert.DoesNotContain(written, name => name!.StartsWith("export-", StringComparison.Ordinal));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(versions, outcome.Version.Id), "*.part"));

        var snapshot = await new FileSnapshotProvider(versions, SourceSchemaCatalog.Default).GetAllAsync();
        var golden = new ScoringEngine(RulesetCatalog.Default).Calculate(snapshot.Find("14381")!);

        Assert.Equal(80.70m, golden.Score);
    }

    [Fact]
    public async Task TheDatabaseAdapterPublishesRenamedExportsToo()
    {
        using var harness = new DatabaseHarness(seed: false);

        var outcome = await harness.Publisher.PublishAsync(
            Renamed(ExportRenamed()), "renombrados", "tests", DateTimeOffset.UtcNow);

        Assert.True(outcome.Accepted, string.Join(" · ", outcome.Errors));

        var snapshot = await harness.Provider.GetAllAsync();
        Assert.Equal(40, snapshot.Applications.Count);
    }

    /// <summary>
    /// Un archivo que no se deja identificar no escribe nada con el nombre del cliente
    /// y devuelve lo que se leyo, en los dos adaptadores por igual.
    /// </summary>
    [Fact]
    public async Task AnUnidentifiedFileLeavesNothingBehindAndSaysWhatWasRead()
    {
        var versions = Path.Combine(_root, "versions-unidentified");
        using var service = new UploadService(versions);
        var content = Encoding.UTF8.GetBytes("EAI,Country\n1001,Chile\n");

        var outcome = await service.PublishAsync(
            [new IncomingSourceFile("datos.csv", content.Length, () => new MemoryStream(content))],
            "ambigua",
            "tests",
            DateTimeOffset.UtcNow);

        var file = Assert.Single(outcome.Files);

        Assert.False(outcome.Accepted);
        Assert.Null(file.CanonicalFileName);
        Assert.Equal(["EAI", "Country"], file.DetectedColumns);
        Assert.Equal(2, file.Candidates.Count);

        var staged = Directory.Exists(versions)
            ? Directory.EnumerateFiles(versions, "*", SearchOption.AllDirectories).ToList()
            : [];

        Assert.DoesNotContain(staged, path => path.EndsWith("datos.csv", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AManualAssignmentResolvesTheAmbiguityInBothAdapters()
    {
        var content = Encoding.UTF8.GetBytes("EAI,Country\n1001,Chile\n");
        var file = new IncomingSourceFile("datos.csv", content.Length, () => new MemoryStream(content))
        {
            AssignedFileName = "apim_onboarded_eais.csv",
        };

        using var service = new UploadService(Path.Combine(_root, "versions-assigned"));
        var onFiles = await service.PublishAsync([file], "asignada", "tests", DateTimeOffset.UtcNow);

        using var harness = new DatabaseHarness(seed: false);
        var onDatabase = await harness.Publisher.PublishAsync([file], "asignada", "tests", DateTimeOffset.UtcNow);

        foreach (var outcome in new[] { onFiles, onDatabase })
        {
            var result = Assert.Single(outcome.Files);

            // La carga sigue rechazada —falta el maestro—, pero este archivo ya quedo
            // identificado y validado contra su contrato.
            Assert.Equal("apim_onboarded_eais.csv", result.CanonicalFileName);
            Assert.Null(result.Error);
        }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
