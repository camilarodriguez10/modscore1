using System.Globalization;
using System.Text;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;
using Modernization.Domain;

namespace Modernization.Sources.ContractTests;

public sealed class CsvHardeningTests
{
    private static SourceSchema ProductStrategy
        => SourceSchemaCatalog.Default.Files.Single(f => f.FileName == "product_strategy.csv");

    /// <summary>
    /// Un campo gigante sin saltos de linea se lee entero en memoria. El tope lo
    /// corta, y el mensaje no repite el contenido del archivo.
    /// </summary>
    [Fact]
    public void AFieldBeyondTheLimitRejectsTheFileWithoutEchoingIt()
    {
        var huge = new string('A', CsvSourceReader.MaxFieldLength + 1);
        using var reader = new StringReader(
            $"EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,{huge}\n");

        var ex = Assert.Throws<SourceSchemaException>(() => new CsvSourceReader(ProductStrategy).Read(reader));

        Assert.Equal(NoticeCodes.CsvFieldTooLong, ex.Notice?.Code);
        Assert.DoesNotContain("AAAAAAAAAA", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldAtTheLimitIsStillRead()
    {
        var atLimit = new string('7', CsvSourceReader.MaxFieldLength);
        using var reader = new StringReader(
            $"EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,1\n1002,0,1,0,{atLimit}\n");

        var outcome = new CsvSourceReader(ProductStrategy).Read(reader);

        Assert.Equal(2, outcome.Rows.Count + outcome.Rejections.Count);
    }
}

public sealed class UploadServiceHardeningTests : IDisposable
{
    private const string ValidCsv =
        "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,1\n";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-upload-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    /// <summary>
    /// Dos cargas de 256 MB a la vez duplican disco y memoria, y ninguna gana nada
    /// con la otra. La segunda se rechaza con motivo, sin esperar.
    /// </summary>
    [Fact]
    public async Task OnlyOnePublicationRunsAtATime()
    {
        using var service = new UploadService(_root);
        var content = Encoding.UTF8.GetBytes(ValidCsv);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var slow = service.PublishAsync(
            [new IncomingSourceFile("product_strategy.csv", content.Length, () => new GatedStream(content, release.Task, started))],
            "lenta",
            "tests",
            DateTimeOffset.UtcNow);

        await started.Task;

        var concurrent = await service.PublishAsync(
            [new IncomingSourceFile("product_strategy.csv", content.Length, () => new MemoryStream(content))],
            "concurrente",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.False(concurrent.Accepted);
        Assert.Contains(concurrent.Errors, e => e.Code == NoticeCodes.UploadBusy);

        release.SetResult();
        await slow;

        // Terminada la primera, la siguiente vuelve a entrar.
        var after = await service.PublishAsync(
            [new IncomingSourceFile("product_strategy.csv", content.Length, () => new MemoryStream(content))],
            "despues",
            "tests",
            DateTimeOffset.UtcNow);

        Assert.DoesNotContain(after.Errors, e => e.Code == NoticeCodes.UploadBusy);
    }

    [Fact]
    public void InterruptedStagingIsCleanedButRecentOnesAreKept()
    {
        using var service = new UploadService(_root);
        var stale = Directory.CreateDirectory(Path.Combine(_root, ".staging", "interrumpida"));
        var recent = Directory.CreateDirectory(Path.Combine(_root, ".staging", "en-curso"));
        Directory.SetLastWriteTimeUtc(stale.FullName, DateTime.UtcNow.AddHours(-3));

        var removed = service.CleanupStaleStaging(DateTimeOffset.UtcNow, TimeSpan.FromHours(1));

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(stale.FullName));
        Assert.True(Directory.Exists(recent.FullName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Un archivo entrante que no termina de llegar hasta que la prueba lo suelta.</summary>
    private sealed class GatedStream(byte[] content, Task gate, TaskCompletionSource started) : MemoryStream(content)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await gate;
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
