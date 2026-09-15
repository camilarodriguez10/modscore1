using System.Globalization;
using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;
using Modernization.Domain;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// El adaptador de archivos corriendo contra el mismo contrato que el adaptador
/// Dummy. La suite es la misma clase abstracta: si migrar de fuente cambiara algo,
/// falla aqui.
/// </summary>
public sealed class FileSnapshotProviderTests : SnapshotProviderContract, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-files-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private readonly DummyPortfolio _portfolio = DummyPortfolio.Generate(applicationCount: 60);

    public FileSnapshotProviderTests()
    {
        // Los CSV que el generador exporta son la entrada del adaptador de
        // archivos. Es exactamente lo que hace barata esta fase: el lector se
        // desarrolla contra ellos antes de que llegue un archivo productivo.
        WriteVersion(_portfolio, "Carga de prueba");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override IApplicationSnapshotProvider CreateProvider() => new FileSnapshotProvider(_root);

    protected override string ExpectedProviderName => "File";

    private string WriteVersion(DummyPortfolio portfolio, string label)
    {
        var id = UploadManifest.NewId(portfolio.CapturedAt);
        var directory = Path.Combine(_root, id);
        var files = CsvSourceExporter.Export(portfolio, directory);

        new UploadManifest
        {
            Id = id,
            Label = label,
            CapturedAt = portfolio.CapturedAt,
            ApplicationCount = portfolio.Eai.Count,
            Files = [.. files.Select(f => f.FileName)],
            UploadedBy = "tests",
        }.Write(directory);

        return id;
    }

    // ------------------------------------------- criterio de aceptacion Fase 7

    /// <summary>
    /// El mismo portafolio leido desde memoria y desde sus CSV produce el mismo
    /// score, aplicacion por aplicacion. Es la prueba de que el formato de
    /// entrega no puede alterar un resultado.
    /// </summary>
    [Fact]
    public async Task ReadingFromCsvProducesTheSameScoresAsReadingFromMemory()
    {
        var engine = new ScoringEngine(RulesetCatalog.Default);

        var fromMemory = await new DummySnapshotProvider(_portfolio).GetAllAsync();
        var fromFiles = await CreateProvider().GetAllAsync();

        Assert.Equal(fromMemory.Applications.Count, fromFiles.Applications.Count);

        var memoryScores = fromMemory.Applications.ToDictionary(
            a => a.EaiCode,
            a => engine.Calculate(a).Score,
            StringComparer.Ordinal);

        var divergences = new List<string>();

        foreach (var application in fromFiles.Applications)
        {
            var fileScore = engine.Calculate(application).Score;

            if (!memoryScores.TryGetValue(application.EaiCode, out var memoryScore))
            {
                divergences.Add($"{application.EaiCode}: solo aparece en los archivos.");
            }
            else if (memoryScore != fileScore)
            {
                divergences.Add($"{application.EaiCode}: memoria {memoryScore}, archivos {fileScore}.");
            }
        }

        Assert.Empty(divergences);
    }

    [Fact]
    public async Task ReferenceApplicationKeepsItsScoreThroughTheFiles()
    {
        var engine = new ScoringEngine(RulesetCatalog.Default);
        var snapshot = await CreateProvider().GetAllAsync();

        var reference = snapshot.Find(DummyPortfolio.ReferenceEaiCode);
        Assert.NotNull(reference);

        var result = engine.Calculate(reference);

        Assert.Equal(44.75m, result.Numerator);
        Assert.Equal(60m, result.Denominator);
        Assert.Equal(79.67m, result.Score);
    }

    // ------------------------------------------------------------- esquemas

    [Fact]
    public void SchemaCatalogCoversEveryExportedFile()
    {
        var catalog = SourceSchemaCatalog.Default;
        var exported = CsvSourceExporter
            .Export(_portfolio, Path.Combine(_root, "schema-check"))
            .Select(f => f.FileName)
            .ToList();

        Assert.Equal(exported.Count, catalog.Files.Count);
        Assert.All(exported, fileName => Assert.NotNull(catalog.Require(fileName)));
    }

    /// <summary>
    /// Una columna obligatoria ausente invalida el archivo entero. Seguir leyendo
    /// produciria un portafolio incompleto sin que nadie lo note.
    /// </summary>
    [Fact]
    public void MissingRequiredColumnFailsTheWholeFile()
    {
        var schema = SourceSchemaCatalog.Default.Require("product_strategy.csv");
        using var reader = new StringReader("Invest_Count,Tolerate_Count\n3,1\n");

        var ex = Assert.Throws<SourceSchemaException>(() => new CsvSourceReader(schema).Read(reader));

        Assert.Contains("EAI_CODE", ex.Message, StringComparison.Ordinal);
        Assert.Contains("product_strategy.csv", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Una fila mal formada se rechaza con su motivo y la lectura continua: un
    /// dato malo en una fila no puede dejar sin score al resto del portafolio.
    /// </summary>
    [Fact]
    public void MalformedRowIsRejectedWithoutStoppingTheRead()
    {
        var schema = SourceSchemaCatalog.Default.Require("product_strategy.csv");
        using var reader = new StringReader(
            "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n"
            + "1001,0,3,0,1\n"
            + "1002,0,no-es-un-numero,0,1\n"
            + "1003,1,2,0,0\n");

        var outcome = new CsvSourceReader(schema).Read(reader);

        Assert.Equal(2, outcome.Rows.Count);
        var rejection = Assert.Single(outcome.Rejections);
        Assert.Equal(3, rejection.RowNumber);
        Assert.Equal("1002", rejection.Key);
        Assert.Equal("Invest_Count", rejection.Reason.Args["column"]);
    }

    [Fact]
    public void RowWithoutKeyIsRejected()
    {
        var schema = SourceSchemaCatalog.Default.Require("monitoreo.csv");
        using var reader = new StringReader("EAI,Agent,APM,Monitoring,Synthetics\n,TRUE,TRUE,TRUE,TRUE\n");

        var outcome = new CsvSourceReader(schema).Read(reader);

        Assert.Empty(outcome.Rows);
        Assert.Equal(NoticeCodes.RowMissingKey, Assert.Single(outcome.Rejections).Reason.Code);
    }

    /// <summary>
    /// Los alias existen porque el mismo dato llega con nombres distintos segun
    /// quien lo exporte: Power BI escribe "Suma de Invest_Count".
    /// </summary>
    [Fact]
    public void ColumnAliasesAreAccepted()
    {
        var schema = SourceSchemaCatalog.Default.Require("product_strategy.csv");
        using var reader = new StringReader(
            "EAI Code,Suma de Eliminate_Count,Suma de Invest_Count,Suma de Migrate_Count,Suma de Tolerate_Count\n"
            + "1001,0,4,1,2\n");

        var outcome = new CsvSourceReader(schema).Read(reader);

        var row = Assert.Single(outcome.Rows);
        Assert.Equal("1001", row.Text("EAI_CODE"));
        Assert.Equal(4, row.Int("Invest_Count"));
    }

    /// <summary>Un export en configuracion europea escribe 1,75 en vez de 1.75.</summary>
    [Fact]
    public void DecimalWithCommaSeparatorIsParsed()
    {
        var schema = SourceSchemaCatalog.Default.Require("tech_debt.csv");
        using var reader = new StringReader(
            "APP_ID,COMBINED_DENIAL_SCORE,COMBINED_ACCEPTED_SCORE,COMBINED_APP_SCORE,LAST_UPDATED_DATE\n"
            + "1081,\"2,5\",1,\"1,75\",16/04/2026 16:06\n");

        var row = Assert.Single(new CsvSourceReader(schema).Read(reader).Rows);

        Assert.Equal(2.5m, row.Decimal("COMBINED_DENIAL_SCORE"));
        Assert.Equal(1.75m, row.Decimal("COMBINED_APP_SCORE"));
        Assert.Equal(2026, row.Date("LAST_UPDATED_DATE")!.Value.Year);
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    [InlineData("Yes", true)]
    [InlineData("N", false)]
    [InlineData("1", true)]
    public void BooleanVariantsAreAccepted(string raw, bool expected)
    {
        var schema = SourceSchemaCatalog.Default.Require("monitoreo.csv");
        using var reader = new StringReader($"EAI,Agent,APM,Monitoring,Synthetics\n1001,{raw},,,\n");

        var row = Assert.Single(new CsvSourceReader(schema).Read(reader).Rows);

        Assert.Equal(expected, row.Bool("Agent"));
    }

    // ---------------------------------------------------------- historia

    /// <summary>
    /// Cada carga es una version. La mas reciente es la actual, y las anteriores
    /// siguen consultables: es lo que convierte un score en una tendencia.
    /// </summary>
    [Fact]
    public async Task EachUploadBecomesAQueryableVersion()
    {
        var older = DummyPortfolio.Generate(
            seed: 99,
            applicationCount: 40,
            capturedAt: _portfolio.CapturedAt.AddMonths(-3));

        var olderId = WriteVersion(older, "Carga anterior");
        var provider = CreateProvider();

        var versions = await provider.GetVersionsAsync();

        Assert.Equal(2, versions.Count);
        Assert.True(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
        Assert.True(versions[0].CapturedAt > versions[1].CapturedAt);

        // La version antigua sigue devolviendo su portafolio, no el actual.
        var historical = await provider.GetAllAsync(olderId);
        Assert.Equal(40, historical.Applications.Count);
        Assert.Equal(olderId, historical.Version.Id);
    }

    [Fact]
    public async Task UnknownVersionIsRejected()
    {
        var provider = CreateProvider();

        await Assert.ThrowsAsync<SourceSchemaException>(() => provider.GetAllAsync("no-existe"));
    }

    /// <summary>
    /// Sin ninguna carga, el adaptador no falla: declara que no hay datos. La
    /// distincion es la que permite que la pantalla de cargas siga funcionando y
    /// el usuario pueda salir del estado inicial.
    /// </summary>
    [Fact]
    public async Task EmptyRootIsReportedAsSuch()
    {
        var provider = new FileSnapshotProvider(Path.Combine(_root, "vacio"));

        Assert.Empty(await provider.GetVersionsAsync());
        await Assert.ThrowsAsync<NoSnapshotAvailableException>(() => provider.GetAllAsync());
    }

    /// <summary>
    /// Un archivo que no se entrego no es un error: sus metricas quedan sin dato,
    /// igual que una aplicacion ausente de un archivo presente.
    /// </summary>
    [Fact]
    public async Task MissingFileLeavesItsMetricsWithoutData()
    {
        var versions = await CreateProvider().GetVersionsAsync();
        File.Delete(Path.Combine(_root, versions[0].Id, "monitoreo.csv"));

        var snapshot = await CreateProvider().GetAllAsync();

        Assert.NotEmpty(snapshot.Applications);
        Assert.All(snapshot.Applications, a => Assert.Null(a.Monitoring));

        var monitoring = snapshot.Sources.Single(s => s.SourceName == "Monitoring");
        Assert.Equal(0, monitoring.RecordCount);
        Assert.Contains("ausente", monitoring.Origin, StringComparison.Ordinal);
    }
}
