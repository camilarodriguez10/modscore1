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

    /// <summary>
    /// El archivo es confidencial: la columna y la fila son lo que permite encontrar la celda
    /// sin mandárselo a nadie.
    /// </summary>
    [Fact]
    public void AFieldBeyondTheLimitNamesItsColumnAndRow()
    {
        var huge = new string('9', CsvSourceReader.MaxFieldLength + 1);
        using var reader = new StringReader(
            $"EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,1\n1002,0,{huge},0,1\n");

        var ex = Assert.Throws<SourceSchemaException>(() => new CsvSourceReader(ProductStrategy).Read(reader));

        Assert.Equal("Invest_Count", ex.Notice?.Args["column"]);
        Assert.Equal("3", ex.Notice?.Args["row"]);
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

    private static SourceSchema Sca
        => SourceSchemaCatalog.Default.Files.Single(f => f.FileName == "veracode_sca_open_flaws.csv");

    /// <summary>
    /// Un export de SCA de 70 MB son unas 350 mil filas. Con un diccionario por fila y una
    /// copia de cada texto repetido, leerlo agotaba el heap de un pod de 1 GiB. Lo que asigna
    /// cada fila se mide en el hilo que lee: no depende de otras pruebas en paralelo.
    /// </summary>
    [Fact]
    public void ALargeExportAllocatesLittlePerRow()
    {
        const int rowCount = 20_000;
        var csv = ScaExport(rowCount);
        var reader = new CsvSourceReader(Sca);

        // Una lectura previa deja cargado todo lo que se inicializa una sola vez.
        using (var warmup = new StringReader(ScaExport(10)))
        {
            reader.Read(warmup);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        CsvSourceReader.ReadOutcome outcome;

        using (var text = new StringReader(csv))
        {
            outcome = reader.Read(text);
        }

        var perRow = (GC.GetAllocatedBytesForCurrentThread() - before) / rowCount;

        Assert.Equal(rowCount, outcome.Rows.Count);
        Assert.True(perRow < 2000, $"Cada fila asignó {perRow} bytes.");
    }

    [Fact]
    public void ARowExposesEverySchemaColumnInOrderAndNullForTheMissingOnes()
    {
        // Sin ACCSBLT ni PUB_DT: el archivo no las trae.
        using var text = new StringReader(
            "FLAW_IDX_NUM,EAI_CD,SVR_DSC,FND_STT\n1,14981,1-Critical,Open\n");

        var row = Assert.Single(new CsvSourceReader(Sca).Read(text).Rows);

        Assert.Equal(Sca.Columns.Select(c => c.Name), row.Values.Keys);
        Assert.Equal(Sca.Columns.Count, row.Values.Count);
        Assert.Equal("14981", row.Text("EAI_CD"));
        Assert.True(row.Values.ContainsKey("ACCSBLT"));
        Assert.Null(row.Values["ACCSBLT"]);
        Assert.False(row.Values.ContainsKey("NOT_A_COLUMN"));
        Assert.Null(row.Text("NOT_A_COLUMN"));
        Assert.Throws<KeyNotFoundException>(() => row.Values["NOT_A_COLUMN"]);
    }

    private static string ScaExport(int rowCount)
    {
        string[] templates =
        [
            "Chile - Agent Commissions,14982,1000,1-Critical,Open,24/08/2023 21:24,05/09/2026 21:24,,1111,SCA,No,CVE-2018-7489,9.8,Insecure deserialization,jackson-databind,FasterXML,05/11/2022 21:24,External",
            "Chile - Agent Commissions,14982,1000,4-Low,Open,14/06/2023 21:24,03/09/2026 21:24,,1182,SCA,No,CVE-2023-3635,7.5,GzipSource overflow,okhttp,Square,26/09/2022 21:24,Internal",
            "Mexico - Claims Portal,15020,1001,2-High,Open,02/01/2024 10:00,01/09/2026 10:00,,977,SCA,Yes,CVE-2022-42889,9.8,Text4Shell,commons-text,Apache,13/10/2022 10:00,External",
            "Brazil - Policy Admin,15111,1002,3-Medium,Open,11/03/2024 08:30,02/09/2026 08:30,,905,SCA,No,CVE-2021-44228,10.0,Log4Shell,log4j-core,Apache,10/12/2021 08:30,Internal",
        ];

        var builder = new StringBuilder(rowCount * 240);
        builder.Append(string.Join(',', Sca.Columns.Select(c => c.Name))).Append('\n');

        for (var i = 0; i < rowCount; i++)
        {
            builder.Append((300_000 + i).ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(templates[i % templates.Length])
                .Append('\n');
        }

        return builder.ToString();
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
