using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;
using Modernization.SystemApi.Services;

namespace Modernization.Api.Tests;

/// <summary>
/// El mapping de columnas previo a la carga, de punta a punta por las dos capas: revisar
/// con solo los encabezados, cargar con el mapping y que la próxima revisión lo recuerde.
/// </summary>
public sealed class ColumnMappingFlowTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string PreviewPath = "/api/ingestion/mapping/preview";

    /// <summary>El export de la organización: el dato está en "Total Users" y no en "User Count".</summary>
    private (string Name, byte[] Content, string Header) RenamedMaster()
    {
        var (name, content) = fixture.ReadSeedFiles().Single(f => f.Name == "eai_master.csv");
        var text = Encoding.UTF8.GetString(content);
        var header = text[..text.IndexOf('\n', StringComparison.Ordinal)].TrimEnd('\r').Replace("User Count", "Total Users", StringComparison.Ordinal);
        var body = text[(text.IndexOf('\n', StringComparison.Ordinal) + 1)..];

        return (name, Encoding.UTF8.GetBytes(header + "\n" + body), header);
    }

    [Fact]
    public async Task TheReviewProposesEachFieldWithOnlyTheHeader()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var (_, _, header) = RenamedMaster();

        using var response = await client.PostAsJsonAsync(
            new Uri(PreviewPath, UriKind.Relative),
            new { files = new[] { new { fileName = "export 0928.csv", header } } },
            Json);

        response.EnsureSuccessStatusCode();
        var review = await response.Content.ReadFromJsonAsync<MappingReviewView>(Json);

        var file = Assert.Single(review!.Files);
        Assert.Equal("eai_master.csv", file.CanonicalFileName);
        Assert.Contains("Total Users", file.Headers);

        var userCount = file.Fields.Single(f => f.Column == "User Count");
        Assert.Equal("missing", userCount.Match);
        Assert.Null(userCount.Header);
        Assert.Contains("Total Users", file.UnusedHeaders);

        var key = file.Fields.Single(f => f.Column == "EAI_Code");
        Assert.True(key.IsKey);
        Assert.Equal("name", key.Match);

        // Cada campo dice para qué sirve: Kenna_Score alimenta una métrica del ruleset.
        Assert.NotEmpty(file.Fields.Single(f => f.Column == "Kenna_Score").Feeds);
    }

    [Fact]
    public async Task TheReviewRefusesDataRows()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri(PreviewPath, UriKind.Relative),
            new { files = new[] { new { fileName = "export.csv", header = "EAI_Code,User Count\n1001,350" } } },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingWithTheMappingLoadsTheChosenColumnAndTheNextReviewRemembersIt()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var (masterName, masterContent, header) = RenamedMaster();

        using var form = new MultipartFormDataContent();

        foreach (var (name, content) in fixture.ReadSeedFiles())
        {
            var isMaster = name == masterName;
            var part = new ByteArrayContent(isMaster ? masterContent : content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", isMaster ? "export 0928.csv" : name);
        }

        foreach (var (name, _) in fixture.ReadSeedFiles())
        {
            var isMaster = name == masterName;
            form.Add(new StringContent(isMaster ? "eai_master.csv" : "-"), "assign");
            form.Add(new StringContent(isMaster ? """{"User Count":"Total Users"}""" : "-"), "map");
        }

        form.Add(new StringContent("Con mapping"), "label");

        using var upload = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
        upload.EnsureSuccessStatusCode();
        var result = await upload.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));

        // El archivo guardado lleva el nombre canónico en la columna elegida.
        var version = Path.Combine(fixture.DataDirectory, result.Version!.Id, "eai_master.csv");
        var storedHeader = (await File.ReadAllLinesAsync(version))[0];
        Assert.Contains("User Count", storedHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("Total Users", storedHeader, StringComparison.Ordinal);

        using var again = await client.PostAsJsonAsync(
            new Uri(PreviewPath, UriKind.Relative),
            new { files = new[] { new { fileName = "export 1028.csv", header } } },
            Json);

        var review = await again.Content.ReadFromJsonAsync<MappingReviewView>(Json);
        var userCount = Assert.Single(review!.Files).Fields.Single(f => f.Column == "User Count");

        Assert.Equal("remembered", userCount.Match);
        Assert.Equal("Total Users", userCount.Header);

        // Y la pestaña Mapping muestra de qué columna salió el campo en la carga vigente.
        var catalog = await client.GetFromJsonAsync<MappingCatalogView>(new Uri("/api/ingestion/mapping", UriKind.Relative), Json);
        Assert.True(catalog!.Recorded);

        var field = catalog.Sources.Single(s => s.FileName == "eai_master.csv").Fields.Single(f => f.Column == "User Count");
        Assert.Equal("Total Users", field.CurrentHeader);
        Assert.True(field.CurrentManual);
    }

    [Fact]
    public async Task TheMappingTabDescribesEverySourceWithTheNamesEachFieldAccepts()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var catalog = await client.GetFromJsonAsync<MappingCatalogView>(new Uri("/api/ingestion/mapping", UriKind.Relative), Json);

        Assert.NotNull(catalog);
        Assert.Equal(8, catalog.Sources.Count);

        var master = catalog.Sources.Single(s => s.FileName == "eai_master.csv");
        Assert.Equal("EAI_Code", master.KeyColumn);

        var userCount = master.Fields.Single(f => f.Column == "User Count");
        Assert.Equal(["User Count", "User_Count", "Users"], userCount.AcceptedNames);
        Assert.NotEmpty(master.Fields.Single(f => f.Column == "Kenna_Score").Feeds);
    }

    [Fact]
    public async Task AMappingSentWithoutOneSourcePerFileIsRejectedAtTheChannel()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var (_, masterContent, _) = RenamedMaster();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(masterContent);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "export.csv");
        form.Add(new StringContent("""{"User Count":"Total Users"}"""), "map");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AMalformedMappingIsRejectedWithItsReason()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var (_, masterContent, _) = RenamedMaster();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(masterContent);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "export.csv");
        form.Add(new StringContent("eai_master.csv"), "assign");
        form.Add(new StringContent("{not json"), "map");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.MappingInvalid);
    }
}

/// <summary>Qué métrica usa cada campo, a partir del campo de origen del ruleset.</summary>
public sealed class MetricFeedsTests
{
    [Theory]
    [InlineData("Kenna_Score", "Kenna_Score", true)]
    [InlineData("OSS Score", "OSS_Score", true)]
    [InlineData("SVR_DSC = 1-Critical", "SVR_DSC", true)]
    [InlineData("Invest/Tolerate/Eliminate/Migrate counts", "Invest_Count", true)]
    [InlineData("Invest/Tolerate/Eliminate/Migrate counts", "Modern_Count", false)]
    [InlineData("Does this Application Utilize MFA?", "Does this Application Utilize MFA?", true)]
    [InlineData("Raw_Tech_Score", "Operational_Score", false)]
    [InlineData("", "Kenna_Score", false)]
    public void RecognizesTheThreeWaysTheRulesetNamesAField(string field, string column, bool expected)
        => Assert.Equal(expected, MetricFeeds.Uses(field, column));
}
