using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modernization.ChannelApi.Exports;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// Revision del 2026-09-14: comparar versiones de reglas con su efecto en una aplicacion
/// (sugerencia 5), marcar los cambios de reglas en la tendencia (sugerencia 4) y exportar
/// en CSV lo que se ve, con los filtros (sugerencia 7).
/// </summary>
public sealed class RulesComparisonAndExportTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static Uri Api(string path) => new(path, UriKind.Relative);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static async Task PublishKennaToMfaAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json);
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("published").GetBoolean(), body.ToString());
    }

    /// <summary>Las filas de un CSV, sin el BOM, respetando comillas.</summary>
    private static async Task<List<string[]>> CsvAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()), "The CSV starts with a UTF-8 BOM.");

        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        return [.. text
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)];
    }

    private static string[] ParseLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (quoted)
            {
                if (character == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        cells.Add(current.ToString());
        return [.. cells];
    }

    // ------------------------------------------------------ comparar versiones

    [Fact]
    public async Task ComparingTwoVersionsShowsChangesImpactAndOneApplicationMetricByMetric()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();
        await PublishKennaToMfaAsync(client);

        using var export = await client.GetAsync(Api("/api/admin/rules/compare/export.csv?from=v1.3&to=v1.4"));
        var rows = await CsvAsync(export);

        Assert.Equal(["EAI Code", "Application", "Score v1.3", "Score v1.4", "Change", "Band v1.3", "Band v1.4"], rows[0]);
        Assert.Equal(41, rows.Count);

        var sample = rows[1];

        using var response = await client.GetAsync(Api($"/api/admin/rules/compare?from=v1.3&to=v1.4&eai={sample[0]}"));
        var comparison = await JsonAsync(response);

        Assert.Equal("v1.3", comparison.GetProperty("from").GetProperty("id").GetString());
        Assert.Equal("v1.4", comparison.GetProperty("to").GetProperty("id").GetString());
        Assert.Equal(2, comparison.GetProperty("changes").GetArrayLength());
        Assert.Equal(40, comparison.GetProperty("impact").GetProperty("applications").GetInt32());

        var application = comparison.GetProperty("application");
        Assert.Equal(sample[0], application.GetProperty("eaiCode").GetString());
        Assert.Equal(sample[2], Number(application.GetProperty("scoreFrom")));
        Assert.Equal(sample[3], Number(application.GetProperty("scoreTo")));

        var kenna = application.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == "kenna");
        Assert.Equal(8m, kenna.GetProperty("weightFrom").GetDecimal());
        Assert.Equal(5m, kenna.GetProperty("weightTo").GetDecimal());
        Assert.True(kenna.GetProperty("changed").GetBoolean());
    }

    private static string Number(JsonElement value)
        => value.ValueKind == JsonValueKind.Null
            ? string.Empty
            : value.GetDecimal().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task ComparingAVersionWithItselfMovesNothing()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(Api("/api/admin/rules/compare?from=v1.3&to=v1.3"));
        var comparison = await JsonAsync(response);

        Assert.Equal(0, comparison.GetProperty("changes").GetArrayLength());
        Assert.Equal(0, comparison.GetProperty("impact").GetProperty("up").GetInt32());
        Assert.Equal(0, comparison.GetProperty("impact").GetProperty("down").GetInt32());
        Assert.Equal(JsonValueKind.Null, comparison.GetProperty("application").ValueKind);
    }

    [Theory]
    [InlineData("/api/admin/rules/compare?from=v1.3&to=v9.9", HttpStatusCode.NotFound)]
    [InlineData("/api/admin/rules/compare?from=..%2Fv1.3&to=v1.3", HttpStatusCode.BadRequest)]
    [InlineData("/api/admin/rules/compare?from=v1.3&to=v1.3&eai=abc", HttpStatusCode.BadRequest)]
    [InlineData("/api/admin/rules/compare?from=v1.3&to=v1.3&eai=999999999", HttpStatusCode.NotFound)]
    [InlineData("/api/admin/rules/compare/export.csv?from=v1.3", HttpStatusCode.BadRequest)]
    public async Task ComparingRejectsWhatDoesNotExistOrIsMalformed(string path, HttpStatusCode expected)
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(Api(path));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SimulatingADraftCanFocusOnOneApplication()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        using var export = await client.GetAsync(Api("/api/portfolio/export.csv"));
        var eai = (await CsvAsync(export))[1][0];

        using var response = await client.PostAsJsonAsync(
            Api("/api/admin/rules/simulate"),
            new { basedOn = "v1.3", draft = RulesHosts.KennaToMfa, eaiCode = eai },
            Json);
        var simulation = await JsonAsync(response);

        var application = simulation.GetProperty("application");
        Assert.Equal(eai, application.GetProperty("eaiCode").GetString());
        Assert.Equal(18, application.GetProperty("metrics").GetArrayLength());

        var mfa = application.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("key").GetString() == "mfa");
        Assert.Equal(10m, mfa.GetProperty("weightTo").GetDecimal());
    }

    [Fact]
    public async Task OnlyRulesAdministratorsCompareOrExportAComparison()
    {
        using var host = RulesHosts.Create(RulesHosts.Access("*", "portfolio.read"));
        var client = await host.CreateMutatingClientAsync();

        using var compare = await client.GetAsync(Api("/api/admin/rules/compare?from=v1.3&to=v1.3"));
        using var export = await client.GetAsync(Api("/api/admin/rules/compare/export.csv?from=v1.3&to=v1.3"));

        Assert.Equal(HttpStatusCode.Forbidden, compare.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
    }

    // ------------------------------------------------------ linea de tiempo

    [Fact]
    public async Task TheRulesTimelineReachesReadersWithoutSayingWhoPublished()
    {
        using var admin = RulesHosts.Create();
        var client = await admin.CreateMutatingClientAsync();
        await PublishKennaToMfaAsync(client);

        using var response = await client.GetAsync(Api("/api/rules/timeline"));
        var raw = await response.Content.ReadAsStringAsync();
        var timeline = await JsonAsync(response);

        Assert.Equal("v1.4", timeline.GetProperty("active").GetProperty("id").GetString());
        Assert.Equal(2, timeline.GetProperty("active").GetProperty("changeCount").GetInt32());
        Assert.Equal(["v1.4", "v1.3"], timeline.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("id").GetString()));
        Assert.DoesNotContain("publishedBy", raw, StringComparison.OrdinalIgnoreCase);

        using var reader = RulesHosts.Create(RulesHosts.Access("lob:Individual", "portfolio.read"));
        var readerClient = await reader.CreateMutatingClientAsync();

        using var readerResponse = await readerClient.GetAsync(Api("/api/rules/timeline"));
        Assert.Equal(HttpStatusCode.OK, readerResponse.StatusCode);
    }

    // ------------------------------------------------------------ exportar

    [Fact]
    public async Task ThePortfolioExportFollowsTheFiltersAndIsAudited()
    {
        var audit = new AuditLog();
        using var host = RulesHosts.Create(channelServices: services => services.AddSingleton<ILoggerProvider>(audit));
        var client = await host.CreateMutatingClientAsync();

        using var all = await client.GetAsync(Api("/api/portfolio/export.csv"));
        var everything = await CsvAsync(all);

        Assert.Equal("EAI Code", everything[0][0]);
        Assert.Equal(41, everything.Count);
        Assert.Matches(@"^modscore-portfolio-\d{8}-\d{4}\.csv$", all.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? string.Empty);

        var country = everything[1][6];

        using var filtered = await client.GetAsync(Api($"/api/portfolio/export.csv?country={Uri.EscapeDataString(country)}"));
        var rows = await CsvAsync(filtered);

        Assert.InRange(rows.Count - 1, 1, 39);
        Assert.All(rows.Skip(1), row => Assert.Equal(country, row[6]));
        Assert.Equal(everything.Skip(1).Count(r => r[6] == country), rows.Count - 1);

        Assert.Contains(audit.Entries, e => e.EventId == 4213 && e.Message.Contains("portfolio", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheExportNeverWidensTheUsersScope()
    {
        using var host = RulesHosts.Create(RulesHosts.Access("lob:Individual", "portfolio.read"));
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(Api("/api/portfolio/export.csv?lob=Group%20Benefits"));
        var rows = await CsvAsync(response);

        Assert.Single(rows);

        using var own = await client.GetAsync(Api("/api/portfolio/export.csv"));
        var ownRows = await CsvAsync(own);

        Assert.True(ownRows.Count > 1);
        Assert.All(ownRows.Skip(1), row => Assert.Equal("Individual", row[3]));
    }

    [Fact]
    public async Task TheRoadmapExportHasOneRowPerGroup()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<JsonElement>(Api("/api/roadmap?groupBy=country"), Json);

        using var response = await client.GetAsync(Api("/api/roadmap/export.csv?groupBy=country"));
        var rows = await CsvAsync(response);

        Assert.Equal("Grouping", rows[0][0]);
        Assert.Equal(view.GetProperty("groups").GetArrayLength(), rows.Count - 1);
        Assert.All(rows.Skip(1), row => Assert.Equal("country", row[0], ignoreCase: true));

        using var invalid = await client.GetAsync(Api("/api/roadmap/export.csv?groupBy=salary"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ------------------------------------------------------------ CSV seguro

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("  =1", "'  =1")]
    [InlineData("\tcmd", "' cmd")]
    [InlineData("Payments, Mexico", "\"Payments, Mexico\"")]
    [InlineData("Line\r\nbreak", "Line  break")]
    [InlineData("Plain", "Plain")]
    public void TextCellsCannotBecomeFormulas(string value, string expected)
        => Assert.Equal(expected, CsvCell.Text(value).Value);

    [Fact]
    public void NumbersKeepTheirSign()
    {
        Assert.Equal("-1.5", CsvCell.Number(-1.5m).Value);
        Assert.Equal(string.Empty, CsvCell.Number((decimal?)null).Value);
    }
}
