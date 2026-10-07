using System.Globalization;
using System.Text;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// ADR 0034, contra los dos publicadores: varios archivos de una fuente se cargan juntos, el
/// mismo archivo dos veces se rechaza y un libro de Excel carga lo mismo que su CSV.
/// </summary>
public abstract class MultiFileUploadContract
{
    protected const string MasterHeader =
        "EAI_Code,APP_ID,Application_Name,Application Status,Application_Disposition,Roadmap,CIO,CRE,LOB,SUB_LOB,Region Name,Country Name,Critical Application Indicator,Internet_Facing,Cloud_Indicator,Cloud_Type,Cloud_Vendor,Coding_Language,DR_In_Scope,Does this Application Utilize MFA?,Primary_Business_Capability,L1_Business_Capability_Count,Recovery_Point_Objective_Range,Recovery_Time_Objective_Range,User Count,Kenna_Score,Raw_Tech_Score,Operational_Score";

    protected const string Reference =
        "14381,9768,Reference Application,Active,Tolerate,Maintain (Patch),A. Moreno,B. Cardoso,Individual,Life,LATAM,Chile,N,N,No,,On-Premise,Java,,Yes,Policy Servicing,2,4-24 hrs.,48-72 hrs.,350,120,97.67,92";

    protected const string Portal =
        "10969,10969,Customer Portal,Active,Invest,Maintain (Patch),A. Moreno,B. Cardoso,Individual,Life,LATAM,Mexico,Y,Y,Yes,Public,Azure,C#,Y,Yes,Policy Servicing,3,0-4 hrs.,0-4 hrs.,1200,400,81.5,88";

    protected static readonly string MasterCsv = MasterHeader + "\n" + Reference + "\n" + Portal + "\n";

    /// <summary>El encabezado del export de APIM que se recibió el 2026-10-07.</summary>
    protected const string ApimHeader = "EAI,Country,Application Short Name,Application Name\n";

    protected abstract PublishOutcome Publish(params (string Name, byte[] Content)[] files);

    protected abstract SnapshotSet Current();

    protected static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void RegionalFilesOfOneSourceAreLoadedTogether()
    {
        var outcome = Publish(
            ("20261005_master.csv", Utf8(MasterCsv)),
            ("APIM_Onboarded_EAIs_EMEA.csv", Utf8("EAI,Country,Application Short Name,Application Name,API Type\n14381,Chile,RA,Reference Application,APIM\n")),
            ("APIM_Onboarded_EAIs_US.csv", Utf8("EAI,Country,Application Short Name,Application Name,API Type\n10969,Mexico,CP,Customer Portal,APIC\n")),
            ("APIM_Onboarded_EAIs_LATAM.xlsx", TestWorkbooks.Single(
                ["EAI", "Country", "Application Short Name", "Application Name"],
                [77777, "Peru", "XX", "Not in the master"])));

        Assert.True(outcome.Accepted, string.Join(" · ", outcome.Errors));
        Assert.Equal(4, outcome.Files.Count);
        Assert.All(outcome.Files.Skip(1), f => Assert.Equal("APIM Onboarding", f.Source));
        Assert.Equal([1, 1, 1], outcome.Files.Skip(1).Select(f => f.RowCount));

        var current = Current();
        var apim = current.Sources.Single(s => s.SourceName == "APIM Onboarding");

        Assert.Equal(3, apim.RecordCount);
        Assert.Equal(2, apim.MatchedApplications);
        Assert.Equal(ApiExposure.Apim, current.Find("14381")!.NativeApi);
        Assert.Equal(ApiExposure.Apic, current.Find("10969")!.NativeApi);
    }

    [Fact]
    public void TheSameFileTwiceIsRefusedBecauseItWouldCountItsRowsTwice()
    {
        var apim = Utf8(ApimHeader + "14381,Chile,RA,Reference Application\n");

        var outcome = Publish(
            ("master.csv", Utf8(MasterCsv)),
            ("APIM_EMEA.csv", apim),
            ("APIM_EMEA (1).csv", apim));

        Assert.False(outcome.Accepted);

        var identical = outcome.Files[2];
        Assert.Equal(NoticeCodes.FileIdentical, identical.Error?.Code);
        Assert.Equal("APIM_EMEA.csv", identical.Error?.Args["other"]);
        Assert.Null(outcome.Files[1].Error);
    }

    [Fact]
    public void AnApplicationRepeatedInALaterMasterFileKeepsTheRowOfThatFile()
    {
        var outcome = Publish(
            ("master_latam.csv", Utf8(MasterCsv)),
            ("master_update.csv", Utf8(MasterHeader + "\n" + Reference.Replace("Tolerate", "Invest", StringComparison.Ordinal) + "\n")));

        Assert.True(outcome.Accepted, string.Join(" · ", outcome.Errors));

        var first = outcome.Files[0];
        Assert.Equal(1, first.RowCount);
        Assert.Equal(1, first.RejectedCount);
        Assert.Contains(first.RejectionReasons, r => r.Reason.Code == NoticeCodes.RowDuplicateKeyInFile);

        var current = Current();
        Assert.Equal(2, current.Applications.Count);
        Assert.Equal(ApplicationDisposition.Invest, current.Find("14381")!.Disposition);
    }

    [Fact]
    public void AnExcelWorkbookLoadsTheSameApplicationsAsItsCsv()
    {
        var rows = new List<object?[]> { MasterHeader.Split(',') };
        rows.AddRange(new[] { Reference, Portal }.Select(line => line.Split(',').Select(Cell).ToArray()));

        var outcome = Publish(("EAI master.xlsx", TestWorkbooks.Single([.. rows])));

        Assert.True(outcome.Accepted, string.Join(" · ", outcome.Errors));
        Assert.Equal("eai_master.csv", outcome.Files[0].CanonicalFileName);

        var reference = Current().Find("14381")!;

        Assert.Equal(120, reference.KennaScore);
        Assert.Equal(97.67m, reference.RawTechScore);
        Assert.Equal(92m, reference.OperationalScore);
        Assert.Equal(350, reference.UserCount);
        Assert.Equal(2, reference.L1BusinessCapabilityCount);
        Assert.Equal("Chile", reference.Country);
        Assert.Equal(ApplicationDisposition.Tolerate, reference.Disposition);
        Assert.False(reference.CriticalApplicationIndicator);
    }

    [Fact]
    public void AProtectedWorkbookIsRejectedWithItsReason()
    {
        var outcome = Publish(
            ("master.csv", Utf8(MasterCsv)),
            ("APIM.xlsx", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0]));

        Assert.False(outcome.Accepted);
        Assert.Equal(NoticeCodes.XlsxProtected, outcome.Files[1].Error?.Code);
        Assert.Contains(outcome.Errors, e => e.Code == NoticeCodes.XlsxProtected && e.Args["file"] == "APIM.xlsx");
    }

    /// <summary>Un valor numérico del CSV como número en la hoja, como lo guarda Excel.</summary>
    private static object? Cell(string value)
        => value.Length == 0
            ? null
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && !value.Contains('-', StringComparison.Ordinal)
                ? number
                : value;
}

public sealed class FileMultiFileUploadTests : MultiFileUploadContract, IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-partes-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private readonly UploadService _service;

    public FileMultiFileUploadTests() => _service = new UploadService(_root);

    public void Dispose()
    {
        _service.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override PublishOutcome Publish(params (string Name, byte[] Content)[] files)
        => _service.PublishAsync(
            [.. files.Select(f => new IncomingSourceFile(f.Name, f.Content.Length, () => new MemoryStream(f.Content)))],
            "Prueba",
            "tests",
            DateTimeOffset.UtcNow).GetAwaiter().GetResult();

    protected override SnapshotSet Current()
        => new FileSnapshotProvider(_root).GetAllAsync().GetAwaiter().GetResult();

    [Fact]
    public void EachFileIsStoredApartAndTheManifestSaysWhereItCameFrom()
    {
        var outcome = Publish(
            ("master.csv", Utf8(MasterCsv)),
            ("APIM_EMEA.csv", Utf8(ApimHeader + "14381,Chile,RA,Reference Application\n")),
            ("APIM_US.csv", Utf8(ApimHeader + "10969,Mexico,CP,Customer Portal\n")));

        Assert.True(outcome.Accepted);

        var version = Directory.EnumerateDirectories(_root).Single(d => !Path.GetFileName(d).StartsWith('.'));
        var manifest = UploadManifest.TryRead(version)!;

        Assert.True(File.Exists(Path.Combine(version, "apim_onboarded_eais.csv")));
        Assert.True(File.Exists(Path.Combine(version, "apim_onboarded_eais.part2.csv")));
        Assert.Equal(
            [("eai_master.csv", "eai_master.csv", "master.csv"), ("apim_onboarded_eais.csv", "apim_onboarded_eais.csv", "APIM_EMEA.csv"), ("apim_onboarded_eais.csv", "apim_onboarded_eais.part2.csv", "APIM_US.csv")],
            manifest.Parts.Select(p => (p.FileName, p.StoredAs, p.ClientFileName!)));
        Assert.Equal(3, manifest.ColumnMaps.Count);
    }

    [Fact]
    public void AManifestThatPointsOutsideTheUploadIsIgnored()
    {
        Assert.True(Publish(("master.csv", Utf8(MasterCsv))).Accepted);

        var version = Directory.EnumerateDirectories(_root).Single(d => !Path.GetFileName(d).StartsWith('.'));
        var manifest = UploadManifest.TryRead(version)!;
        File.WriteAllText(Path.Combine(_root, "secret.csv"), MasterCsv.Replace("14381", "99999", StringComparison.Ordinal));

        (manifest with
        {
            Parts = [.. manifest.Parts, new UploadPart("eai_master.csv", "../secret.csv", "evil.csv")],
        }).Write(version);

        var current = Current();

        Assert.Null(current.Find("99999"));
        Assert.Equal(2, current.Applications.Count);
    }
}

public sealed class DatabaseMultiFileUploadTests : MultiFileUploadContract, IDisposable
{
    private readonly DatabaseHarness _database = new(seed: false);

    public void Dispose() => _database.Dispose();

    protected override PublishOutcome Publish(params (string Name, byte[] Content)[] files)
        => _database.Publisher.PublishAsync(
            [.. files.Select(f => new IncomingSourceFile(f.Name, f.Content.Length, () => new MemoryStream(f.Content)))],
            "Prueba",
            "tests",
            DateTimeOffset.UtcNow).GetAwaiter().GetResult();

    protected override SnapshotSet Current()
        => _database.Provider.GetAllAsync().GetAwaiter().GetResult();
}
