using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;

namespace Modernization.Api.Tests;

/// <summary>
/// Las dos capas sobre el adaptador de base de datos, con el esquema aplicado al
/// arrancar. Es el mismo montaje que el de archivos, cambiando una linea de
/// configuracion: eso es lo que la Fase 09 tiene que demostrar.
/// </summary>
public sealed class DatabaseBackedApiFixture : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-dbapi-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public DatabaseBackedApiFixture()
    {
        CsvDirectory = Path.Combine(_root, "csv");
        Directory.CreateDirectory(_root);

        CsvSourceExporter.Export(DummyPortfolio.Generate(applicationCount: 40), CsvDirectory);

        Host = new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Database",
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Modernization"] = $"Data Source={Path.Combine(_root, "modernization.db")}",
        });
    }

    public TwoLayerHost Host { get; }

    /// <summary>Los ocho CSV del portafolio dummy, listos para subir por la pantalla.</summary>
    public string CsvDirectory { get; }

    public MultipartFormDataContent BuildUpload(string label)
    {
        var form = new MultipartFormDataContent();

        foreach (var path in Directory.EnumerateFiles(CsvDirectory, "*.csv"))
        {
            var part = new ByteArrayContent(File.ReadAllBytes(path));
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", Path.GetFileName(path));
        }

        form.Add(new StringContent(label), "label");
        return form;
    }

    public void Dispose()
    {
        Host.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                // Un temporal que el motor todavia tiene abierto: no importa.
            }
        }
    }
}

/// <summary>Una base recien migrada, sin ninguna carga: el primer arranque.</summary>
public sealed class EmptyDatabaseApiTests(DatabaseBackedApiFixture fixture) : IClassFixture<DatabaseBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TheUploadScreenWorksAndThePortfolioSaysThereIsNoData()
    {
        var client = await fixture.Host.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal("Database", view.Provider);
        Assert.True(view.UploadEnabled);
        Assert.Empty(view.Versions);

        using var portfolio = await client.GetAsync(new Uri("/api/portfolio?pageSize=5", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Conflict, portfolio.StatusCode);
    }
}

/// <summary>El flujo completo de la pantalla de cargas, terminando en la base.</summary>
public sealed class DatabaseUploadTests(DatabaseBackedApiFixture fixture) : IClassFixture<DatabaseBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishingThroughTheChannelFillsTheDatabaseAndScoresTheSame()
    {
        var client = await fixture.Host.CreateMutatingClientAsync();

        using (var form = fixture.BuildUpload("Carga en base de datos"))
        using (var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form))
        {
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);
            Assert.NotNull(result);
            Assert.True(result.Accepted, string.Join(" · ", result.Errors));
            Assert.Equal(40, result.ApplicationCount);
        }

        // El portafolio vive: la capa Channel no sabe de donde salio.
        var portfolio = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=10", UriKind.Relative), Json);

        Assert.NotNull(portfolio);
        Assert.Equal(40, portfolio.TotalApplications);

        // Y el caso de referencia sigue dando lo mismo que con archivos.
        var explain = await client.GetFromJsonAsync<ExplainView>(
            new Uri("/api/applications/14381/explain", UriKind.Relative), Json);

        Assert.NotNull(explain);
        Assert.Equal(80.70m, explain.Formula.Score);
        Assert.Equal("20 + (45.529 / 60) x 80 = 80.70", explain.Formula.Expression);

        // La carga quedo registrada como version, con su autor.
        var ingestion = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(ingestion);
        var version = Assert.Single(ingestion.Versions);
        Assert.Equal("Carga en base de datos", version.Label);
        Assert.True(version.IsCurrent);
    }
}
