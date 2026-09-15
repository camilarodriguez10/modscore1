using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

/// <summary>
/// El primer arranque del adaptador de archivos: el directorio existe y no hay
/// ninguna carga dentro.
/// </summary>
/// <remarks>
/// Es el estado en el que queda cualquier instalacion nueva, y tiene que tener
/// salida: si las pantallas devolvieran 500 el usuario no tendria forma de saber
/// que lo unico que falta es subir los archivos, ni de llegar a la pantalla que
/// se lo permite.
/// </remarks>
public sealed class EmptyPortfolioFixture : IDisposable
{
    private readonly TwoLayerHost _host;

    public string DataDirectory { get; }

    public EmptyPortfolioFixture()
    {
        DataDirectory = Path.Combine(
            Path.GetTempPath(),
            "modscore-empty-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(DataDirectory);

        _host = new TwoLayerHost(new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "File",
            ["DataSource:Directory"] = DataDirectory,
        });
    }

    public Task<HttpClient> CreateAuthenticatedClientAsync() => _host.CreateAuthenticatedChannelClientAsync();

    public void Dispose()
    {
        _host.Dispose();

        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}

public sealed class EmptyPortfolioTests(EmptyPortfolioFixture fixture) : IClassFixture<EmptyPortfolioFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Falta de datos no es falla del servicio. Un 500 le diria al usuario que
    /// algo se rompio; un 409 le dice que falta la primera carga, que es algo que
    /// puede resolver.
    /// </summary>
    [Theory]
    [InlineData("/api/portfolio?pageSize=5")]
    [InlineData("/api/lineage")]
    [InlineData("/api/roadmap")]
    [InlineData("/api/applications/14381/overview")]
    [InlineData("/api/applications/14381/explain")]
    [InlineData("/api/applications/14381/whatif")]
    [InlineData("/api/applications/14381/roadmap")]
    public async Task ScreensReportMissingDataInsteadOfFailing(string path)
    {
        var client = await fixture.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemShape>(Json);
        Assert.NotNull(problem);
        Assert.Equal("No published uploads", problem.Title);
        Assert.Contains("upload", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La unica pantalla que tiene que seguir en pie: es la salida del estado
    /// vacio. Sin esto el sistema queda bloqueado en su primer arranque.
    /// </summary>
    [Fact]
    public async Task TheUploadScreenStillWorksWithNoData()
    {
        var client = await fixture.CreateAuthenticatedClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.UploadEnabled);
        Assert.Empty(view.Versions);
        Assert.Empty(view.PortfolioHistory);

        // Y sigue diciendo que archivos espera, que es lo que el usuario necesita.
        Assert.Equal(8, view.ExpectedFiles.Count);
    }

    private sealed record ProblemShape(string Title, string Detail);
}
