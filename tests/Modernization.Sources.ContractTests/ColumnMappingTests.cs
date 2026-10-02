using System.Globalization;
using System.Text;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// El mapping de columnas previo a la carga: qué columna del archivo carga cada campo.
/// </summary>
public sealed class ColumnMappingTests
{
    private static SourceSchema Master => SourceSchemaCatalog.Default.Require("eai_master.csv");

    private static readonly string[] ExportHeader =
        ["EAI_Code", "Application_Name", "Total Users", "Users", "Kenna Score", "Comments"];

    [Fact]
    public void WithoutDecisionsEachFieldIsRecognizedByNameOrAliasLikeTheReader()
    {
        var resolution = ColumnMapping.Resolve(Master, ExportHeader);

        var byColumn = resolution.Columns.ToDictionary(c => c.Column.Name);

        Assert.Equal(ColumnMatch.Name, byColumn["EAI_Code"].How);
        Assert.Equal(ColumnMatch.Alias, byColumn["User Count"].How);
        Assert.Equal("Users", byColumn["User Count"].Header);
        Assert.Equal(ColumnMatch.Alias, byColumn["Kenna_Score"].How);
        Assert.Equal(ColumnMatch.Missing, byColumn["Raw_Tech_Score"].How);
        Assert.True(resolution.IsValid);
        Assert.Equal(["Total Users", "Comments"], resolution.UnusedHeaders);
    }

    /// <summary>
    /// El caso que motivó el módulo: el export trae el dato en "Total Users" y una columna
    /// "Users" con otra cosa, que por alias se habría cargado en User Count sin aviso.
    /// </summary>
    [Fact]
    public void AManualChoiceWinsAndTheColumnThatWouldMatchByAliasStopsLoading()
    {
        var resolution = ColumnMapping.Resolve(Master, ExportHeader, new Dictionary<string, string?> { ["User Count"] = "Total Users" });

        var userCount = resolution.Columns.Single(c => c.Column.Name == "User Count");
        Assert.Equal(ColumnMatch.Manual, userCount.How);
        Assert.Equal("Total Users", userCount.Header);

        var rewritten = resolution.RewrittenHeaders();
        Assert.Equal("User Count", rewritten[2]);
        Assert.Equal(ColumnMapping.NotLoadedPrefix + "Users", rewritten[3]);
        Assert.Equal("Kenna_Score", rewritten[4]);
        Assert.Equal("Comments", rewritten[5]);
    }

    [Fact]
    public void TheRewrittenFileIsReadExactlyAsMapped()
    {
        var csv = string.Join(',', ExportHeader) + "\n1001,Claims,350,internal,120,ok\n";
        var resolution = ColumnMapping.Resolve(Master, ExportHeader, new Dictionary<string, string?> { ["User Count"] = "Total Users" });

        using var reader = ColumnMapping.Open(new StringReader(csv), resolution);
        var outcome = new CsvSourceReader(Master).Read(reader);

        var row = Assert.Single(outcome.Rows);
        Assert.Equal(350, row.Int("User Count"));
        Assert.Equal(120, row.Int("Kenna_Score"));
        Assert.Empty(outcome.Rejections);

        // Sin el mapping, "Users" entra por alias y su texto deja User Count sin dato.
        using var plain = new StringReader(csv);
        var unmapped = new CsvSourceReader(Master).Read(plain);
        Assert.Null(Assert.Single(unmapped.Rows).Int("User Count"));
        Assert.Equal("User Count", Assert.Single(unmapped.IgnoredValues).Reason.Args["column"]);
    }

    [Fact]
    public void AnOptionalFieldMarkedNotInTheFileIsLeftEmptyEvenIfAColumnMatchesByAlias()
    {
        var csv = "EAI_Code,Users\n1001,internal\n";
        var headers = new[] { "EAI_Code", "Users" };
        var resolution = ColumnMapping.Resolve(Master, headers, new Dictionary<string, string?> { ["User Count"] = null });

        Assert.Equal(ColumnMatch.Excluded, resolution.Columns.Single(c => c.Column.Name == "User Count").How);

        using var reader = ColumnMapping.Open(new StringReader(csv), resolution);
        var row = Assert.Single(new CsvSourceReader(Master).Read(reader).Rows);

        Assert.Null(row.Int("User Count"));
    }

    [Fact]
    public void DecisionsAreValidatedAgainstTheContractAndTheFile()
    {
        var resolution = ColumnMapping.Resolve(Master, ExportHeader, new Dictionary<string, string?>
        {
            ["Not_A_Field"] = "Comments",
            ["Kenna_Score"] = "Missing column",
            ["User Count"] = "Total Users",
            ["Operational_Score"] = "Total Users",
            ["EAI_Code"] = null,
        });

        var codes = resolution.Problems.Select(p => p.Code).ToList();

        Assert.False(resolution.IsValid);
        Assert.Contains(NoticeCodes.MappingUnknownColumn, codes);
        Assert.Contains(NoticeCodes.MappingHeaderNotInFile, codes);
        Assert.Contains(NoticeCodes.MappingHeaderTwice, codes);
        Assert.Contains(NoticeCodes.MappingRequiredExcluded, codes);
    }

    [Fact]
    public void TheCurrentUploadsMappingIsProposedOnlyWhereTheFileStillHasThatColumn()
    {
        var remembered = new SourceColumnMap("eai_master.csv",
        [
            new ColumnAssignment("User Count", "Total Users", Manual: true),
            new ColumnAssignment("Raw_Tech_Score", "Tech Score (raw)", Manual: true),
            new ColumnAssignment("Kenna_Score", "Kenna Score", Manual: false),
        ]);

        var resolution = ColumnMapping.Resolve(Master, ExportHeader, remembered: remembered);
        var byColumn = resolution.Columns.ToDictionary(c => c.Column.Name);

        Assert.Equal(ColumnMatch.Remembered, byColumn["User Count"].How);
        Assert.Equal("Total Users", byColumn["User Count"].Header);

        // La columna recordada ya no existe: no se inventa, queda como siempre.
        Assert.Equal(ColumnMatch.Missing, byColumn["Raw_Tech_Score"].How);

        // Lo que se reconoció por alias no es una decisión y no se "recuerda".
        Assert.Equal(ColumnMatch.Alias, byColumn["Kenna_Score"].How);
    }

    [Fact]
    public void WhatIsRecordedDistinguishesDecisionsFromRecognition()
    {
        var resolution = ColumnMapping.Resolve(Master, ExportHeader, new Dictionary<string, string?> { ["User Count"] = "Total Users" });

        var map = resolution.ToColumnMap();

        Assert.Equal("eai_master.csv", map.FileName);
        Assert.Contains(map.Columns, a => a is { Column: "User Count", Header: "Total Users", Manual: true });
        Assert.Contains(map.Columns, a => a is { Column: "Kenna_Score", Header: "Kenna Score", Manual: false });
        Assert.Contains(map.Columns, a => a is { Column: "Raw_Tech_Score", Header: null, Manual: false });
    }

    [Fact]
    public void AHeaderThatSpansLinesIsRejectedInsteadOfGuessed()
    {
        var csv = "EAI_Code,\"Total\nUsers\"\n1001,350\n";
        var headers = CsvSourceReader.ReadHeader(new StringReader(csv));
        var resolution = ColumnMapping.Resolve(Master, headers, new Dictionary<string, string?> { ["User Count"] = headers[1] });

        var ex = Assert.Throws<SourceSchemaException>(() => ColumnMapping.Open(new StringReader(csv), resolution));

        Assert.Equal(NoticeCodes.MappingHeaderMultiline, ex.Notice?.Code);
    }

    [Fact]
    public void RewrittenNamesWithCommasOrQuotesStayOneColumn()
    {
        var headers = new[] { "EAI_Code", "Users, total \"all\"" };
        var csv = "EAI_Code,\"Users, total \"\"all\"\"\"\n1001,42\n";
        var resolution = ColumnMapping.Resolve(Master, headers, new Dictionary<string, string?> { ["User Count"] = headers[1] });

        using var reader = ColumnMapping.Open(new StringReader(csv), resolution);
        var row = Assert.Single(new CsvSourceReader(Master).Read(reader).Rows);

        Assert.Equal(42, row.Int("User Count"));
    }

    [Fact]
    public void OnlyTheHeaderLineIsReadForTheReviewAndItIsBounded()
    {
        Assert.Equal(["EAI_Code", "Users"], ColumnMapping.ParseHeaderLine("EAI_Code,Users\n1001,secret-row"));

        var tooMany = string.Join(',', Enumerable.Range(0, ColumnMapping.MaxHeaderColumns + 1).Select(i => "c" + i.ToString(CultureInfo.InvariantCulture)));
        var ex = Assert.Throws<SourceSchemaException>(() => ColumnMapping.ParseHeaderLine(tooMany));
        Assert.Equal(NoticeCodes.MappingHeaderTooLarge, ex.Notice?.Code);

        Assert.Throws<SourceSchemaException>(() => ColumnMapping.ParseHeaderLine("   "));
    }
}

/// <summary>El mismo mapping en el adaptador de base de datos: se aplica al leer y queda en la fila de la versión.</summary>
public sealed class DatabaseColumnMappingTests
{
    [Fact]
    public async Task TheDatabaseAdapterAppliesAndRemembersTheMappingLikeTheFileAdapter()
    {
        using var harness = new DatabaseHarness(seed: false);
        var content = "EAI_Code,Application_Name,Total Users,Users\n1001,Claims,350,internal\n";
        var bytes = Encoding.UTF8.GetBytes(content);

        var outcome = await harness.Publisher.PublishAsync(
            [new IncomingSourceFile("export.csv", bytes.Length, () => new MemoryStream(bytes))
            {
                AssignedFileName = "eai_master.csv",
                ColumnMap = new Dictionary<string, string?> { ["User Count"] = "Total Users" },
            }],
            "con mapping",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.True(outcome.Accepted, string.Join(" | ", outcome.Errors));

        var snapshot = await harness.Provider.GetAllAsync();
        Assert.Equal(350, snapshot.Applications.Single().UserCount);

        var map = Assert.Single(await harness.Publisher.GetCurrentColumnMapsAsync());
        Assert.Contains(map.Columns, a => a is { Column: "User Count", Header: "Total Users", Manual: true });
    }

    [Fact]
    public async Task AnUploadBeforeTheMappingWasRecordedRemembersNothing()
    {
        using var harness = new DatabaseHarness(seed: false);

        Assert.Empty(await harness.Publisher.GetCurrentColumnMapsAsync());
    }
}

/// <summary>El mapping aplicado en la carga del adaptador de archivos, y lo que queda registrado.</summary>
public sealed class UploadWithColumnMappingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-mapping-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private const string Export = "EAI_Code,Application_Name,Total Users,Users\n1001,Claims,350,internal\n1002,Billing,90,external\n";

    private static IncomingSourceFile File(string content, string? assigned, IReadOnlyDictionary<string, string?>? map)
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        return new IncomingSourceFile("export 0928.csv", bytes.Length, () => new MemoryStream(bytes))
        {
            AssignedFileName = assigned,
            ColumnMap = map,
        };
    }

    [Fact]
    public async Task TheMappingIsAppliedTheFileIsStoredWithCanonicalNamesAndTheNextUploadRemembersIt()
    {
        using var service = new UploadService(_root);

        var outcome = await service.PublishAsync(
            [File(Export, "eai_master.csv", new Dictionary<string, string?> { ["User Count"] = "Total Users" })],
            "con mapping",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.True(outcome.Accepted, string.Join(" | ", outcome.Errors));
        var file = Assert.Single(outcome.Files);
        Assert.Equal(2, file.RowCount);
        Assert.Equal(0, file.RejectedCount);

        // El archivo guardado dice por sí mismo qué columna cargó cada campo.
        var stored = await System.IO.File.ReadAllLinesAsync(Path.Combine(_root, outcome.Version!.Id, "eai_master.csv"));
        Assert.Equal($"EAI_Code,Application_Name,User Count,{ColumnMapping.NotLoadedPrefix}Users", stored[0]);

        // Y la versión se lee con el valor de la columna elegida.
        var snapshot = await new FileSnapshotProvider(_root).GetAllAsync();
        Assert.Equal(350, snapshot.Applications.Single(a => a.EaiCode == "1001").UserCount);

        var remembered = await service.GetCurrentColumnMapsAsync();
        var map = Assert.Single(remembered);
        Assert.Contains(map.Columns, a => a is { Column: "User Count", Header: "Total Users", Manual: true });
    }

    [Fact]
    public async Task AMappingWithoutTheSourceIsRejected()
    {
        using var service = new UploadService(_root);

        var outcome = await service.PublishAsync(
            [File(Export, null, new Dictionary<string, string?> { ["User Count"] = "Total Users" })],
            "sin fuente",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.False(outcome.Accepted);
        Assert.Equal(NoticeCodes.MappingNeedsSource, Assert.Single(outcome.Files).Error?.Code);
    }

    [Fact]
    public async Task AnInvalidMappingRejectsTheFileWithTheReason()
    {
        using var service = new UploadService(_root);

        var outcome = await service.PublishAsync(
            [File(Export, "eai_master.csv", new Dictionary<string, string?> { ["User Count"] = "Not a column" })],
            "mapping malo",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.False(outcome.Accepted);
        Assert.Equal(NoticeCodes.MappingHeaderNotInFile, Assert.Single(outcome.Files).Error?.Code);
        Assert.DoesNotContain(Directory.EnumerateDirectories(_root), d => !d.EndsWith(".staging", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithoutAMappingEveryUploadStillRecordsWhichColumnLoadedEachField()
    {
        using var service = new UploadService(_root);

        var outcome = await service.PublishAsync(
            [File("EAI_Code,Application_Name,User Count\n1001,Claims,350\n", null, null)],
            "sin mapping",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.True(outcome.Accepted, string.Join(" | ", outcome.Errors));

        var map = Assert.Single(await service.GetCurrentColumnMapsAsync());
        Assert.All(map.Columns, a => Assert.False(a.Manual));
        Assert.Contains(map.Columns, a => a is { Column: "User Count", Header: "User Count" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
