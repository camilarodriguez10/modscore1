using System.Globalization;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;

namespace Modernization.Api.Tests;

/// <summary>
/// Las dos capas corriendo sobre el adaptador de archivos, con una carga inicial
/// ya publicada. Es lo que permite probar el flujo real de carga en vez de solo
/// su rechazo.
/// </summary>
public sealed class FileBackedApiFixture : IDisposable
{
    internal const string ServiceToken = TwoLayerHost.ServiceToken;

    private readonly TwoLayerHost _host;

    /// <summary>Directorio de versiones que usa el adaptador de archivos.</summary>
    public string DataDirectory { get; }

    public FileBackedApiFixture()
    {
        DataDirectory = Path.Combine(
            Path.GetTempPath(),
            "modscore-api-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        SeedInitialVersion();

        _host = new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "File",
            ["DataSource:Directory"] = DataDirectory,
        });
    }

    /// <summary>Una carga inicial: sin ella el proveedor no tiene de donde leer.</summary>
    private void SeedInitialVersion() => SeedVersion(DataDirectory);

    /// <summary>Publica en el directorio una carga con 40 aplicaciones.</summary>
    internal static void SeedVersion(string dataDirectory)
    {
        var portfolio = DummyPortfolio.Generate(applicationCount: 40);
        var id = UploadManifest.NewId(portfolio.CapturedAt);
        var directory = Path.Combine(dataDirectory, id);
        var files = CsvSourceExporter.Export(portfolio, directory);

        new UploadManifest
        {
            Id = id,
            Label = "Carga inicial",
            CapturedAt = portfolio.CapturedAt,
            ApplicationCount = portfolio.Eai.Count,
            Files = [.. files.Select(f => f.FileName)],
            UploadedBy = "tests",
        }.Write(directory);
    }

    /// <summary>Los CSV de la carga inicial, para reenviarlos como si fueran nuevos.</summary>
    public IReadOnlyList<(string Name, byte[] Content)> ReadSeedFiles()
    {
        // Se busca por contenido, no por posicion: el directorio de staging
        // tambien vive bajo la raiz y no contiene una carga completa.
        var directory = Directory.EnumerateDirectories(DataDirectory)
            .First(d => File.Exists(Path.Combine(d, "eai_master.csv")));

        return [.. Directory.EnumerateFiles(directory, "*.csv")
            .Select(path => (Path.GetFileName(path), File.ReadAllBytes(path)))];
    }

    public HttpClient CreateChannelClient() => _host.CreateChannelClient();

    /// <summary>Cliente autenticado y con token CSRF cargado, listo para mutar.</summary>
    public Task<HttpClient> CreateMutatingClientAsync() => _host.CreateMutatingClientAsync();

    public HttpClient CreateSystemClient(bool authenticated = true) => _host.CreateSystemClient(authenticated);

    public void Dispose()
    {
        _host.Dispose();

        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}
