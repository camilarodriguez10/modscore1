using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;
using Modernization.Security;
using Modernization.Sources.Connectors;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;

namespace Modernization.Api.Tests;

/// <summary>
/// ADR 0037 por las dos capas: leer las fuentes de sus bases publica una carga con el mismo
/// rol, el mismo alcance y la misma validación que los archivos, y nada de la configuración
/// —cadenas, servidores, consultas— llega al navegador.
/// </summary>
public sealed class DatabaseConnectorApiTests : IDisposable
{
    private const string SecretMarker = "never-leaves-the-server";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-connectors-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private readonly List<TwoLayerHost> _hosts = [];

    [Fact]
    public async Task ByDefault_TheDataScreenHasNoDatabaseSources_AndAPullSaysSo()
    {
        var host = Host(enabled: false);
        var client = await host.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(view);
        Assert.Null(view.Connectors);

        using var response = await client.PostAsJsonAsync(new Uri("/api/ingestion/pull", UriKind.Relative), new { label = (string?)null });
        response.EnsureSuccessStatusCode();
        var pull = await response.Content.ReadFromJsonAsync<PullResultView>(Json);

        Assert.NotNull(pull);
        Assert.False(pull.Upload.Accepted);
        Assert.Equal(NoticeCodes.ConnectorDisabled, pull.Upload.Summary.Code);
    }

    [Fact]
    public async Task APull_PublishesANewVersionThroughTheUploadPath()
    {
        var host = Host(enabled: true);
        var client = await host.CreateMutatingClientAsync();

        var before = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(before?.Connectors);
        Assert.True(before.Connectors.PullEnabled);
        Assert.Equal(SourceSchemaCatalog.Default.Files.Count, before.Connectors.Sources.Count);
        Assert.Equal([new ConnectorConnectionView("staging", "SqlServer")], before.Connectors.Connections);

        using var response = await client.PostAsJsonAsync(new Uri("/api/ingestion/pull", UriKind.Relative), new { label = "Pull from staging" });
        response.EnsureSuccessStatusCode();
        var pull = await response.Content.ReadFromJsonAsync<PullResultView>(Json);

        Assert.NotNull(pull);
        Assert.True(pull.Upload.Accepted, string.Join(" · ", pull.Upload.Errors));
        Assert.Equal(NoticeCodes.UploadPublished, pull.Upload.Summary.Code);
        Assert.All(pull.Sources, s => Assert.Null(s.Error));
        Assert.Equal(30, pull.Upload.ApplicationCount);

        var after = await client.GetFromJsonAsync<IngestionView>(new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(after);
        Assert.Equal(before.Versions.Count + 1, after.Versions.Count);
        Assert.Equal("Pull from staging", after.Versions.Single(v => v.IsCurrent).Label);
    }

    [Fact]
    public async Task TestingConnections_ReportsEachOneWithoutPublishing()
    {
        var host = Host(enabled: true);
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.PostAsync(new Uri("/api/ingestion/connectors/test", UriKind.Relative), content: null);
        response.EnsureSuccessStatusCode();
        var checks = await response.Content.ReadFromJsonAsync<List<ConnectionTestView>>(Json);

        var check = Assert.Single(checks!);
        Assert.Equal("staging", check.Name);
        Assert.True(check.Ok);
    }

    [Fact]
    public async Task NothingOfTheConfiguration_ReachesTheBrowser()
    {
        var host = Host(enabled: true);
        var client = await host.CreateMutatingClientAsync();

        var screen = await client.GetStringAsync(new Uri("/api/ingestion", UriKind.Relative));
        using var system = host.CreateSystemClient();
        var connectors = await system.GetStringAsync(new Uri("/system/ingestion/connectors", UriKind.Relative));

        foreach (var body in new[] { screen, connectors })
        {
            Assert.DoesNotContain(SecretMarker, body, StringComparison.Ordinal);
            Assert.DoesNotContain("db.test", body, StringComparison.Ordinal);
            Assert.DoesNotContain("SRC_", body, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("GET", "/system/ingestion/connectors")]
    [InlineData("POST", "/system/ingestion/connectors/test")]
    [InlineData("POST", "/system/ingestion/pull")]
    public async Task TheSystemLayer_RequiresScopeOverTheWholePortfolio(string method, string path)
    {
        var host = Host(enabled: true);
        using var client = host.CreateSystemClient(scope: "lob:Individual");

        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task APullWithoutTheCsrfToken_IsRefused()
    {
        var host = Host(enabled: true);
        var client = await host.CreateAuthenticatedChannelClientAsync();

        using var response = await client.PostAsJsonAsync(new Uri("/api/ingestion/pull", UriKind.Relative), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void AConfigurationThatCannotWork_StopsTheStartup()
    {
        var host = Track(new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "File",
            ["DataSource:Directory"] = SeedVersions(),
            ["Connectors:Enabled"] = "true",
            ["Connectors:Sources:payroll:Connection"] = "nowhere",
            ["Connectors:Sources:payroll:Table"] = "PAYROLL",
        }));

        var error = Assert.ThrowsAny<Exception>(() => host.CreateSystemClient());

        Assert.Contains("Connectors", error.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private TwoLayerHost Host(bool enabled)
    {
        var settings = new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "File",
            ["DataSource:Directory"] = SeedVersions(),
        };

        if (!enabled)
        {
            return Track(new TwoLayerHost(settings));
        }

        var database = SeedDatabase();

        settings["Connectors:Enabled"] = "true";
        settings["Connectors:Connections:staging:Provider"] = "SqlServer";
        settings["Connectors:Connections:staging:ConnectionString"] = $"Server=db.test;Database=cmt;User ID=reader;Password={SecretMarker};Encrypt=Strict";

        foreach (var file in SourceSchemaCatalog.Default.Files)
        {
            var key = ConnectorConfiguration.KeyOf(file.FileName);
            settings[$"Connectors:Sources:{key}:Connection"] = "staging";
            settings[$"Connectors:Sources:{key}:Table"] = TableOf(file.FileName);
        }

        return Track(new TwoLayerHost(
            settings,
            configureSystemServices: services =>
            {
                services.RemoveAll<IConnectorConnectionFactory>();
                services.AddSingleton<IConnectorConnectionFactory>(new SqliteConnections(database));
            }));
    }

    private TwoLayerHost Track(TwoLayerHost host)
    {
        _hosts.Add(host);
        return host;
    }

    private string SeedVersions()
    {
        var directory = Path.Combine(_root, "versions-" + _hosts.Count.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        FileBackedApiFixture.SeedVersion(directory);
        return directory;
    }

    private static string TableOf(string fileName) => "SRC_" + ConnectorConfiguration.KeyOf(fileName).ToUpperInvariant();

    /// <summary>Una tabla por fuente con un portafolio de ejemplo de 30 aplicaciones, todo como texto.</summary>
    private string SeedDatabase()
    {
        var export = Path.Combine(_root, "export-" + _hosts.Count.ToString(CultureInfo.InvariantCulture));
        var database = Path.Combine(_root, "staging-" + _hosts.Count.ToString(CultureInfo.InvariantCulture) + ".db");
        var files = CsvSourceExporter.Export(DummyPortfolio.Generate(applicationCount: 30), export);

        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();

        foreach (var file in files)
        {
            using var reader = new StreamReader(Path.Combine(export, file.FileName));
            using var csv = new CsvHelper.CsvParser(reader, CultureInfo.InvariantCulture);
            csv.Read();
            var header = csv.Record ?? [];
            var table = TableOf(file.FileName);

            using (var create = connection.CreateCommand())
            {
#pragma warning disable CA2100 // Encabezados del export de ejemplo, entre comillas; ningún dato externo.
                create.CommandText = $"CREATE TABLE {table} ({string.Join(", ", header.Select(h => $"\"{h.Replace("\"", "\"\"", StringComparison.Ordinal)}\" TEXT"))})";
#pragma warning restore CA2100
                create.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();

            while (csv.Read())
            {
                var row = csv.Record ?? [];
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
#pragma warning disable CA2100 // Solo parámetros: los valores nunca se concatenan.
                insert.CommandText = $"INSERT INTO {table} VALUES ({string.Join(", ", header.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture)))})";
#pragma warning restore CA2100

                for (var i = 0; i < header.Length; i++)
                {
                    insert.Parameters.AddWithValue("$p" + i.ToString(CultureInfo.InvariantCulture), i < row.Length && row[i].Length > 0 ? row[i] : DBNull.Value);
                }

                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        return database;
    }

    /// <summary>La base local en lugar del servidor: misma lectura, mismo publicador.</summary>
    private sealed class SqliteConnections(string database) : IConnectorConnectionFactory
    {
        public DbConnection Create(string name, ConnectionOptions options) => new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");

        public string PingQuery(ConnectionOptions options) => "SELECT 1";

        public string? ReadOnlyStatement(ConnectionOptions options) => "PRAGMA query_only = ON";
    }
}
