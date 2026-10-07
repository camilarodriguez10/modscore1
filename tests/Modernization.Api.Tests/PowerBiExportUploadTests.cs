using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;

namespace Modernization.Api.Tests;

/// <summary>
/// Un EAI Master exportado desde Power BI, con el encabezado que entregó la organización
/// el 2026-10-02: "Suma de" en los campos resumidos, "MFA" junto al nombre completo, la
/// jerarquía de fecha (Year, Quarter, Month, Day), sin APP_ID y con coma decimal.
/// </summary>
/// <remarks>
/// Antes de la lectura tolerante (ADR 0028) una carga así terminaba con el EAI Master en
/// cero filas. Se arma desde la carga inicial de la prueba para comparar contra el caso de
/// referencia: la 14381 tiene que seguir dando 80,70 (79,67 con las bandas del BRD, ADR 0032).
/// </remarks>
public sealed class PowerBiExportUploadTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> PowerBiNames = new(StringComparer.Ordinal)
    {
        ["Kenna_Score"] = "Suma de Kenna_Score",
        ["L1_Business_Capability_Count"] = "Suma de L1_Business_Capability_Count",
        ["Operational_Score"] = "Suma de Operational_Score",
        ["Raw_Tech_Score"] = "Suma de Raw_Tech_Score",
    };

    private static readonly string[] DecimalColumns = ["Raw_Tech_Score", "Operational_Score"];

    /// <summary>El maestro de la carga inicial, reescrito como lo exporta Power BI en español.</summary>
    private static byte[] AsPowerBiExport(byte[] master)
    {
        var lines = Encoding.UTF8.GetString(master).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split(','))
            .ToList();

        var header = lines[0];
        var keep = Enumerable.Range(0, header.Length).Where(i => header[i] != "APP_ID").ToList();
        var mfa = Array.IndexOf(header, "Does this Application Utilize MFA?");

        var output = new StringBuilder();
        var names = new List<string>();

        foreach (var i in keep)
        {
            if (i == mfa)
            {
                names.Add("MFA");
            }

            names.Add(PowerBiNames.GetValueOrDefault(header[i], header[i]));
        }

        names.AddRange(["Year", "Quarter", "Month", "Day"]);
        output.Append(string.Join(',', names)).Append('\n');

        foreach (var row in lines.Skip(1))
        {
            var cells = new List<string>();

            foreach (var i in keep)
            {
                if (i == mfa)
                {
                    cells.Add("Other");
                }

                var value = row[i];

                if (DecimalColumns.Contains(header[i]) && value.Length > 0)
                {
                    value = "\"" + decimal.Parse(value, CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.GetCultureInfo("es-CL")) + "\"";
                }
                else if (header[i] == "Kenna_Score" && value.Length > 0)
                {
                    value = "\"" + value + ",00\"";
                }
                else if (value.Length == 0 && header[i] == "Kenna_Score")
                {
                    value = "(En blanco)";
                }

                cells.Add(value);
            }

            cells.AddRange(["2026", "Trimestre 3", "septiembre", "28"]);
            output.Append(string.Join(',', cells)).Append('\n');
        }

        return Encoding.UTF8.GetBytes(output.ToString());
    }

    [Fact]
    public async Task APowerBiExportOfTheEaiMasterLoadsEveryApplicationAndKeepsTheReferenceScore()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var seed = fixture.ReadSeedFiles();
        var applications = Encoding.UTF8.GetString(seed.Single(f => f.Name == "eai_master.csv").Content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;

        using var form = new MultipartFormDataContent();

        foreach (var (name, content) in seed)
        {
            var isMaster = name == "eai_master.csv";
            var part = new ByteArrayContent(isMaster ? AsPowerBiExport(content) : content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", isMaster ? "EAI TABLE export.csv" : name);
        }

        form.Add(new StringContent("Export de Power BI"), "label");

        using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.True(result.Accepted, string.Join(" | ", result.Errors));
        Assert.Equal(applications, result.ApplicationCount);

        var master = result.Files.Single(f => f.CanonicalFileName == "eai_master.csv");
        Assert.Equal(applications, master.RowCount);
        Assert.Equal(0, master.RejectedCount);
        Assert.Empty(master.IgnoredValues);

        // El caso de referencia del BRD, leído del export con "Suma de" y coma decimal.
        var overview = await client.GetFromJsonAsync<ScoreOverviewView>(
            new Uri("/api/applications/14381/overview", UriKind.Relative), Json);

        Assert.NotNull(overview);
        Assert.Equal(80.70m, overview.Score);
        Assert.Equal(60m, overview.Denominator);
    }
}
