using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// La carga de archivos es el endpoint mas peligroso de la aplicacion. Estos
/// tests convierten cada control del diseno en un hecho comprobable.
/// </summary>
public sealed class UploadSecurityTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string UploadPath = "/api/ingestion/upload";

    /// <summary>Un CSV minimo pero valido segun el contrato de Product Strategy.</summary>
    private const string ValidCsv =
        "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count\n1001,0,3,0,1\n";

    private async Task<HttpClient> MutatingClientAsync()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var session = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        var raw = session.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains("csrf", StringComparison.OrdinalIgnoreCase));
        var pair = raw.Split(';')[0];

        client.DefaultRequestHeaders.Add("X-CSRF-Token", pair[(pair.IndexOf('=', StringComparison.Ordinal) + 1)..]);
        return client;
    }

    private static MultipartFormDataContent Form(
        string fileName,
        string content,
        string contentType = "text/csv")
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(part, "files", fileName);
        return form;
    }

    // ------------------------------------------------------- autenticacion

    [Fact]
    public async Task UploadRejectsAnonymousRequests()
    {
        var client = fixture.CreateChannelClient();

        using var form = Form("product_strategy.csv", ValidCsv);
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// La carga es una mutacion: sin token CSRF no pasa, igual que cualquier otro
    /// POST. Un formulario en un sitio de terceros no puede reemplazar el
    /// portafolio de la organizacion.
    /// </summary>
    [Fact]
    public async Task UploadWithoutCsrfTokenIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var form = Form("product_strategy.csv", ValidCsv);
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------- validacion de entrada

    [Fact]
    public async Task UploadWithoutFilesIsRejected()
    {
        var client = await MutatingClientAsync();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Carga vacia"), "label");

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonMultipartRequestIsRejected()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri(UploadPath, UriKind.Relative),
            new { files = "no soy multipart" },
            Json);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    /// <summary>
    /// Un cliente puede enviar cualquier cosa como nombre de archivo. Lo que llega
    /// nunca se usa como ruta: se toma solo el nombre y se empareja contra la
    /// lista blanca de fuentes conocidas.
    /// </summary>
    [Theory]
    [InlineData("../../../appsettings.json")]
    [InlineData("..\\..\\web.config")]
    [InlineData("/etc/passwd")]
    public async Task PathTraversalInFileNameIsNeverUsedAsAPath(string fileName)
    {
        var client = await MutatingClientAsync();

        using var form = Form(fileName, ValidCsv);
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("script.js")]
    [InlineData("archivo.xlsx")]
    [InlineData("sin-extension")]
    public async Task OnlyCsvExtensionsAreAccepted(string fileName)
    {
        var client = await MutatingClientAsync();

        using var form = Form(fileName, ValidCsv, "application/octet-stream");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.FileExtension);
    }

    [Fact]
    public async Task UnexpectedContentTypeIsRejected()
    {
        var client = await MutatingClientAsync();

        using var form = Form("product_strategy.csv", ValidCsv, "text/html");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Args.Values.Any(v => v.Contains("text/html", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TooManyFilesAreRejected()
    {
        var client = await MutatingClientAsync();

        using var form = new MultipartFormDataContent();
        for (var i = 0; i < 20; i++)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes(ValidCsv));
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", string.Create(CultureInfo.InvariantCulture, $"archivo-{i}.csv"));
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
    }

    // ------------------------------------------------------- adaptador activo

    /// <summary>
    /// Con el adaptador Dummy la carga esta deshabilitada, y la pantalla lo dice
    /// en lugar de ofrecer un boton que no puede funcionar.
    /// </summary>
    [Fact]
    public async Task IngestionScreenReportsUploadDisabledOnTheDummyAdapter()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.False(view.UploadEnabled);
        Assert.Equal("Dummy", view.Provider);
        Assert.NotNull(view.DisabledReason);
    }

    [Fact]
    public async Task UploadOnADummyAdapterChangesNothing()
    {
        var client = await MutatingClientAsync();

        using var form = Form("product_strategy.csv", ValidCsv);
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.UploadNotSupported);

        // Y el portafolio sigue siendo el mismo.
        var portfolio = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5", UriKind.Relative), Json);

        Assert.NotNull(portfolio);
        Assert.True(portfolio.TotalApplications > 0);
    }

    // -------------------------------------------------------------- historia

    [Fact]
    public async Task IngestionScreenListsVersionsAndPortfolioHistory()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotEmpty(view.Versions);
        Assert.Single(view.Versions, v => v.IsCurrent);
        Assert.Equal(8, view.ExpectedFiles.Count);

        // La serie va de la carga mas antigua a la mas reciente.
        Assert.True(view.PortfolioHistory.Count > 1);
        Assert.Equal(
            view.PortfolioHistory.Select(p => p.CapturedAt).OrderBy(d => d),
            view.PortfolioHistory.Select(p => p.CapturedAt));
    }

    /// <summary>
    /// La evolucion de una aplicacion trae el peso disponible en cada punto: un
    /// salto de score con mas peso detras puede ser mas evidencia, no mejora.
    /// </summary>
    [Fact]
    public async Task ApplicationHistoryCarriesScoreAndWeight()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var history = await client.GetFromJsonAsync<IReadOnlyList<HistoryPointView>>(
            new Uri("/api/applications/14381/history", UriKind.Relative), Json);

        Assert.NotNull(history);
        Assert.NotEmpty(history);

        var withScore = history.Where(p => p.Score is not null).ToList();
        Assert.NotEmpty(withScore);
        Assert.All(withScore, p =>
        {
            Assert.InRange(p.Score!.Value, 20m, 100m);
            Assert.NotNull(p.AvailableWeight);
        });

        // El primer punto no tiene con que compararse.
        Assert.Null(history[0].DeltaFromPrevious);
    }

    [Fact]
    public async Task HistoryRejectsAMalformedEaiCode()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri("/api/applications/no-es-un-eai/history", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
