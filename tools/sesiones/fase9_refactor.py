import io
import os

ROOT = r"C:\Users\kmiro\Downloads\mod score"


def edit(rel, pairs, replace_all=()):
    """Idempotente: si el cambio ya esta aplicado, lo salta en lugar de fallar."""
    path = os.path.join(ROOT, rel)
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    applied = 0

    for old, new in pairs:
        if old in s:
            s = s.replace(old, new, 1)
            applied += 1
        elif new not in s:
            raise AssertionError(f"NO MATCH in {rel}: {old[:110]!r}")

    for old, new in replace_all:
        if old in s:
            s = s.replace(old, new)
            applied += 1

    if crlf:
        s = s.replace("\n", "\r\n")

    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print(f"ok {rel} ({applied} cambios)")


# ------------------------------------------------------- paquetes de EF Core
edit(r"Directory.Packages.props", [(
    '    <PackageVersion Include="CsvHelper" Version="33.1.0" />',
    '    <PackageVersion Include="CsvHelper" Version="33.1.0" />\n'
    '    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />\n'
    '    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />')])

# ------------------------------------------- el adaptador ve los nombres de fuente
edit(r"src\system\Modernization.Sources\Modernization.Sources.csproj", [(
    '    <InternalsVisibleTo Include="Modernization.Sources.ContractTests" />',
    '    <InternalsVisibleTo Include="Modernization.Sources.ContractTests" />\n'
    '    <InternalsVisibleTo Include="Modernization.Sources.Database" />')])

# ------------------------------------ UploadService pasa a implementar el puerto
edit(
    r"src\system\Modernization.Sources\Files\UploadService.cs",
    [
        ("public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null) : IDisposable",
         "public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null)\n    : ISnapshotPublisher, IDisposable"),
        ("""    /// <summary>Maximo por archivo. Un CSV mayor no es una carga, es un incidente.</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>Maximo por carga completa.</summary>
    public const long MaxTotalBytes = 256L * 1024 * 1024;""",
         """    /// <summary>Maximo por archivo. Los topes son del dominio: todos los adaptadores comparten los mismos.</summary>
    public const long MaxFileBytes = PublishLimits.MaxFileBytes;

    /// <summary>Maximo por carga completa.</summary>
    public const long MaxTotalBytes = PublishLimits.MaxTotalBytes;

    /// <summary>Adaptador que publica.</summary>
    public string Provider => FileSnapshotProvider.ProviderName;"""),
        ("""    /// <summary>Un archivo entrante, ya en memoria o como stream.</summary>
    public sealed record IncomingFile(string ClientFileName, long Length, Func<Stream> OpenRead);

    public sealed record UploadOutcome(
        bool Accepted,
        SnapshotVersion? Version,
        IReadOnlyList<FileOutcome> Files,
        IReadOnlyList<string> Errors,
        int ApplicationCount);

    public sealed record FileOutcome(
        string ClientFileName,
        string? CanonicalFileName,
        string Source,
        long Bytes,
        int RowCount,
        int RejectedCount,
        string? Error);

""", ""),
    ],
    replace_all=[
        ("UploadService.IncomingFile", "IncomingSourceFile"),
        ("IncomingFile", "IncomingSourceFile"),
        ("UploadOutcome", "PublishOutcome"),
        ("FileOutcome", "PublishedFileOutcome"),
    ])

# ------------------------------------- IngestionService depende del puerto
edit(
    r"src\system\Modernization.SystemApi\Services\IngestionService.cs",
    [
        ("using Microsoft.AspNetCore.Http;\nusing Modernization.Security;",
         "using Microsoft.AspNetCore.Http;\nusing Modernization.Domain.Sources;\nusing Modernization.Security;"),
        ("""public sealed class IngestionService(
    UploadService? uploads,""",
         """public sealed class IngestionService(
    ISnapshotPublisher? publisher,"""),
        ("    public bool IsEnabled => uploads is not null;",
         "    public bool IsEnabled => publisher is not null;"),
        ("        if (uploads is null)", "        if (publisher is null)"),
        ("        return await uploads.PublishAsync(", "        return await publisher.PublishAsync("),
    ],
    replace_all=[
        ("UploadService.UploadOutcome", "PublishOutcome"),
        ("UploadService.IncomingFile", "IncomingSourceFile"),
    ])

# ---------------------------------------------- contrato de respuesta de carga
edit(r"src\system\Modernization.SystemApi\Contracts\SystemContracts.cs", [
    ("    public static UploadResponse From(Sources.Files.UploadService.UploadOutcome o) => new(",
     "    public static UploadResponse From(Domain.Sources.PublishOutcome o) => new("),
])

# ------------------------------------------------------- cableado de la capa System
edit(r"src\system\Modernization.SystemApi\Program.cs", [
    ("using Modernization.Sources.Files;",
     "using Modernization.Sources.Database;\nusing Modernization.Sources.Files;"),
    ("""        builder.Services.AddSingleton<IApplicationSnapshotProvider>(
            _ => new FileSnapshotProvider(dataDirectory));
        builder.Services.AddSingleton(_ => new UploadService(dataDirectory));
        break;

    case "Database":
        throw new InvalidOperationException("El adaptador de base de datos llega en la Fase 09.");""",
     """        builder.Services.AddSingleton<IApplicationSnapshotProvider>(
            _ => new FileSnapshotProvider(dataDirectory));
        builder.Services.AddSingleton(_ => new UploadService(dataDirectory));
        builder.Services.AddSingleton<ISnapshotPublisher>(sp => sp.GetRequiredService<UploadService>());
        break;

    case "Database":
        // La cadena de conexion es un secreto: llega como archivo del CSI driver,
        // igual que el token de servicio.
        var connectionString = builder.Configuration.GetConnectionString("Modernization")
            ?? throw new InvalidOperationException(
                "El adaptador de base de datos necesita 'ConnectionStrings:Modernization'.");

        builder.Services.AddDatabaseSnapshotAdapter(new DatabaseAdapterOptions(
            builder.Configuration["Database:Provider"] ?? DatabaseAdapter.Sqlite,
            connectionString,
            builder.Configuration.GetValue("Database:RetainVersions", 0)));
        break;"""),
    ("""builder.Services.AddSingleton(sp => new IngestionService(
    sp.GetService<UploadService>(),""",
     """builder.Services.AddSingleton(sp => new IngestionService(
    sp.GetService<ISnapshotPublisher>(),"""),
    ("""app.Services.GetService<UploadService>()?.CleanupStaleStaging(
    app.Services.GetRequiredService<TimeProvider>().GetUtcNow(),
    TimeSpan.FromHours(1));""",
     """app.Services.GetService<UploadService>()?.CleanupStaleStaging(
    app.Services.GetRequiredService<TimeProvider>().GetUtcNow(),
    TimeSpan.FromHours(1));

// Con base de datos, el esquema se pone al dia antes de atender la primera
// peticion. Con varias replicas esto lo hace un job de despliegue, no cada proceso.
if (provider == "Database" && builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DatabaseAdapter.MigrateAsync(app.Services).ConfigureAwait(false);
}"""),
])

# ------------------------------------------------------------------ tests
edit(r"tests\Modernization.Sources.ContractTests\Modernization.Sources.ContractTests.csproj", [
    ("""    <ProjectReference Include="..\\..\\src\\system\\Modernization.Sources\\Modernization.Sources.csproj" />""",
     """    <ProjectReference Include="..\\..\\src\\system\\Modernization.Sources\\Modernization.Sources.csproj" />
    <ProjectReference Include="..\\..\\src\\system\\Modernization.Sources.Database\\Modernization.Sources.Database.csproj" />"""),
])

edit(r"tests\Modernization.Sources.ContractTests\UploadHardeningTests.cs", [
    ("using System.Globalization;\nusing System.Text;\nusing Modernization.Sources.Files;",
     "using System.Globalization;\nusing System.Text;\nusing Modernization.Domain.Sources;\nusing Modernization.Sources.Files;"),
], replace_all=[("UploadService.IncomingFile", "IncomingSourceFile")])

print("done")
