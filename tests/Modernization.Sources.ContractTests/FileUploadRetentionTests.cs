using System.Globalization;
using System.Text;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// ASVS 8.3.4 (SEC-07): el adaptador de archivos conserva las N cargas mas recientes, como
/// ya hacia el de base de datos. Lo que se borra es inventario de aplicaciones, asi que la
/// politica tiene que ser explicita y no puede tocar nada que no sea una carga.
/// </summary>
public sealed class FileUploadRetentionTests : IDisposable
{
    private const string MasterCsv =
        "EAI_Code,APP_ID,Application_Name,Application Status,Application_Disposition,Roadmap,CIO,CRE,LOB,SUB_LOB,Region Name,Country Name,Critical Application Indicator,Internet_Facing,Cloud_Indicator,Cloud_Type,Cloud_Vendor,Coding_Language,DR_In_Scope,Does this Application Utilize MFA?,Primary_Business_Capability,L1_Business_Capability_Count,Recovery_Point_Objective_Range,Recovery_Time_Objective_Range,User Count,Kenna_Score,Raw_Tech_Score,Operational_Score\n"
        + "14381,9768,Reference Application - BRD Worked Example,Active,Tolerate,Maintain (Patch),A. Moreno,B. Cardoso,Individual,Life,LATAM,Chile,N,N,No,,On-Premise,Java,,Yes,Policy Servicing,2,4-24 hrs.,48-72 hrs.,350,120,97.67,92\n";

    private static readonly DateTimeOffset Start = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-retencion-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task OnlyTheMostRecentUploadsSurvive()
    {
        using var service = new UploadService(_root, retainVersions: 2);

        var first = await PublishAsync(service, Start);
        await PublishAsync(service, Start.AddHours(1));
        var third = await PublishAsync(service, Start.AddHours(2));

        var kept = Uploads();

        Assert.Equal(2, kept.Count);
        Assert.Contains(third, kept);
        Assert.DoesNotContain(first, kept);
    }

    [Fact]
    public async Task WithoutRetentionEveryUploadIsKept()
    {
        using var service = new UploadService(_root);

        await PublishAsync(service, Start);
        await PublishAsync(service, Start.AddHours(1));
        await PublishAsync(service, Start.AddHours(2));

        Assert.Equal(3, Uploads().Count);
    }

    /// <summary>
    /// Las reglas publicadas viven en el mismo volumen que las cargas. Borrar cargas
    /// antiguas no puede llevarselas por delante.
    /// </summary>
    [Fact]
    public async Task RetentionDoesNotTouchWhatIsNotAnUpload()
    {
        var rules = Path.Combine(_root, FileRulesStore.DefaultDirectoryName);
        Directory.CreateDirectory(rules);
        await File.WriteAllTextAsync(Path.Combine(rules, "v1.4.json"), "{}");

        using var service = new UploadService(_root, retainVersions: 1);

        await PublishAsync(service, Start);
        await PublishAsync(service, Start.AddHours(1));

        Assert.Single(Uploads());
        Assert.True(File.Exists(Path.Combine(rules, "v1.4.json")));
    }

    // -----------------------------------------------------------------------

    private static async Task<string> PublishAsync(UploadService service, DateTimeOffset capturedAt)
    {
        var bytes = Encoding.UTF8.GetBytes(MasterCsv);

        var outcome = await service.PublishAsync(
            [new IncomingSourceFile("eai_master.csv", bytes.Length, () => new MemoryStream(bytes))],
            capturedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
            "tests",
            capturedAt);

        Assert.True(outcome.Accepted, string.Join(" - ", outcome.Errors.Select(e => e.Code)));
        return outcome.Version!.Id;
    }

    private List<string> Uploads()
        => [.. Directory.EnumerateDirectories(_root)
            .Where(directory => UploadManifest.TryRead(directory) is not null)
            .Select(directory => Path.GetFileName(directory)!)];
}
