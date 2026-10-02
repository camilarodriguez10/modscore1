using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// El flujo real de carga sobre el adaptador de archivos: subir, validar,
/// publicar y recalcular.
/// </summary>
public sealed class UploadFlowTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string UploadPath = "/api/ingestion/upload";

    private MultipartFormDataContent SeedForm(string label)
    {
        var form = new MultipartFormDataContent();

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        form.Add(new StringContent(label), "label");
        return form;
    }

    [Fact]
    public async Task IngestionScreenReportsUploadEnabledOnTheFileAdapter()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.UploadEnabled);
        Assert.Equal("File", view.Provider);
        Assert.Null(view.DisabledReason);
        Assert.NotEmpty(view.Versions);
        Assert.Single(view.Versions, v => v.IsCurrent);
    }

    /// <summary>
    /// Un nombre que no corresponde a ninguna fuente se rechaza, y el error dice
    /// cuales se esperaban en vez de dejar al usuario adivinando.
    /// </summary>
    [Fact]
    public async Task UnknownFileNameIsRejectedWithTheExpectedList()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("A,B\n1,2\n"));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "inventario-random.csv");

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.UploadMissingMaster);
    }

    /// <summary>
    /// Dos cargas seguidas producen versiones distintas. Con ids a granularidad
    /// de segundo, la segunda habria sobrescrito a la primera sin aviso.
    /// </summary>
    [Fact]
    public async Task TwoUploadsInQuickSuccessionProduceDistinctVersions()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var firstForm = SeedForm("Rafaga A");
        using var firstResponse = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), firstForm);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<UploadResultView>(Json);

        using var secondForm = SeedForm("Rafaga B");
        using var secondResponse = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), secondForm);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(first?.Version);
        Assert.NotNull(second?.Version);
        Assert.NotEqual(first.Version.Id, second.Version.Id);
    }

    /// <summary>
    /// El camino feliz completo: los ocho archivos entran, se publican como una
    /// version nueva y el portafolio queda recalculado.
    /// </summary>
    [Fact]
    public async Task UploadPublishesANewVersionAndRecalculates()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var before = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(before);

        using var form = SeedForm("Carga de prueba");

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));
        Assert.Equal(8, result.Files.Count);
        Assert.All(result.Files, file => Assert.Null(file.Error));
        Assert.Equal(40, result.ApplicationCount);
        Assert.NotNull(result.Version);
        Assert.Equal(NoticeCodes.UploadPublished, result.Summary.Code);

        // La carga nueva aparece como version vigente.
        var after = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(after);
        Assert.True(after.Versions.Count > before.Versions.Count);
        Assert.Equal("Carga de prueba", after.Versions.Single(v => v.IsCurrent).Label);
    }

    /// <summary>
    /// El score del EAI de referencia sobrevive el viaje completo: CSV subido por
    /// HTTP, validado, publicado, releido y recalculado.
    /// </summary>
    [Fact]
    public async Task ReferenceScoreSurvivesTheWholeUploadRoundTrip()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = SeedForm("Verificacion 14381");
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var overview = await client.GetFromJsonAsync<ScoreOverviewView>(
            new Uri("/api/applications/14381/overview", UriKind.Relative), Json);

        Assert.NotNull(overview);
        Assert.Equal(79.67m, overview.Score);
        Assert.Equal(60m, overview.Denominator);
    }

    /// <summary>
    /// Sin el maestro EAI no hay portafolio. La carga se rechaza entera y el
    /// portafolio vigente no se toca.
    /// </summary>
    [Fact]
    public async Task UploadWithoutTheMasterFileIsRejectedAndChangesNothing()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var before = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5", UriKind.Relative), Json);
        Assert.NotNull(before);

        using var form = new MultipartFormDataContent();
        foreach (var (name, content) in fixture.ReadSeedFiles().Where(f => f.Name != "eai_master.csv"))
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);
        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.UploadMissingMaster);

        var after = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=5", UriKind.Relative), Json);

        Assert.NotNull(after);
        Assert.Equal(before.TotalApplications, after.TotalApplications);
        Assert.Equal(before.AverageScore, after.AverageScore);
    }

    /// <summary>
    /// Un archivo al que le falta una columna obligatoria invalida la carga
    /// completa, con el nombre de la columna en el mensaje.
    /// </summary>
    [Fact]
    public async Task FileMissingARequiredColumnIsReportedWithTheColumnName()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var bytes = name == "product_strategy.csv"
                ? Encoding.UTF8.GetBytes("Invest_Count,Tolerate_Count\n3,1\n")
                : content;

            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Args.Values.Any(v => v.Contains("EAI_CODE", StringComparison.Ordinal)));

        var failed = result.Files.Single(f => f.FileName == "product_strategy.csv");
        Assert.NotNull(failed.Error);
    }

    /// <summary>
    /// Un valor ilegible en un campo opcional deja ese campo sin dato y la fila entra
    /// (ADR 0028); la pantalla dice qué columna, qué esperaba y el valor.
    /// </summary>
    [Fact]
    public async Task UnreadableOptionalValuesAreLoadedAsEmptyAndReported()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var bytes = content;

            if (name == "product_strategy.csv")
            {
                var text = Encoding.UTF8.GetString(content);
                bytes = Encoding.UTF8.GetBytes(text + "9999,0,no-es-un-numero,0,1\n");
            }

            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));

        var strategy = result.Files.Single(f => f.FileName == "product_strategy.csv");
        Assert.Equal(0, strategy.RejectedCount);
        Assert.Empty(strategy.RejectionReasons);

        // El valor llega a la pantalla: la columna, qué esperaba y lo recibido.
        var ignored = Assert.Single(strategy.IgnoredValues);
        Assert.Equal(NoticeCodes.RowInvalidValue, ignored.Reason.Code);
        Assert.Equal("Invest_Count", ignored.Reason.Args["column"]);
        Assert.Equal("no-es-un-numero", ignored.Reason.Args["value"]);
        Assert.Equal(1, ignored.Rows);
    }

    /// <summary>
    /// El caso que lo motivó: un EAI Master con una columna de números ilegible en todas
    /// las filas. Antes se rechazaban las 3.869 filas y la carga se descartaba; ahora cada
    /// aplicación entra sin ese dato, y la pantalla dice cuál es.
    /// </summary>
    [Fact]
    public async Task AMasterWithAnUnreadableColumnInEveryRowStillLoadsEveryApplication()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        var masterRows = 0;

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var bytes = content;

            if (name == "eai_master.csv")
            {
                // Cada fila con un rango en User Count, como traen algunos inventarios.
                var lines = Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var header = lines[0].TrimEnd('\r').Split(',');
                var userCount = Array.IndexOf(header, "User Count");
                var broken = lines.Skip(1).Select(line =>
                {
                    var cells = line.TrimEnd('\r').Split(',');
                    cells[userCount] = "100-500";
                    return string.Join(',', cells);
                }).ToList();

                masterRows = broken.Count;
                bytes = Encoding.UTF8.GetBytes(string.Join('\n', [lines[0].TrimEnd('\r'), .. broken]) + "\n");
            }

            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));
        Assert.Equal(masterRows, result.ApplicationCount);

        var master = result.Files.Single(f => f.CanonicalFileName == "eai_master.csv");
        Assert.Equal(masterRows, master.RowCount);
        Assert.Equal(0, master.RejectedCount);

        var ignored = Assert.Single(master.IgnoredValues);
        Assert.Equal("User Count", ignored.Reason.Args["column"]);
        Assert.Equal("integer", ignored.Reason.Args["expected"]);
        Assert.Equal("100-500", ignored.Reason.Args["value"]);
        Assert.Equal(masterRows, ignored.Rows);
        Assert.Equal([2, 3, 4, 5, 6], ignored.SampleRows);
    }

    /// <summary>
    /// Los exports reales llegan con prefijo de fecha. El nombre se empareja por
    /// su raiz, asi que "0809 tech_debt.csv" entra sin renombrar nada.
    /// </summary>
    [Fact]
    public async Task DatePrefixedFileNamesAreMatchedByTheirStem()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", string.Create(CultureInfo.InvariantCulture, $"0809 {name}"));
        }

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));
        Assert.All(result.Files, file => Assert.StartsWith("0809 ", file.FileName, StringComparison.Ordinal));
        Assert.All(result.Files, file => Assert.NotNull(file.CanonicalFileName));
    }

    /// <summary>Cada carga se acumula como version consultable, no reemplaza a la anterior.</summary>
    [Fact]
    public async Task EachUploadAddsToTheHistory()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var first = SeedForm("Historia A");
        using var firstResponse = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), first);
        firstResponse.EnsureSuccessStatusCode();

        var history = await client.GetFromJsonAsync<IReadOnlyList<HistoryPointView>>(
            new Uri("/api/applications/14381/history", UriKind.Relative), Json);

        Assert.NotNull(history);
        Assert.True(history.Count >= 2);
        Assert.All(history.Where(p => p.Score is not null), p => Assert.Equal(79.67m, p.Score));
    }
}
