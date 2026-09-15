using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// Criterio de aceptacion de la Fase 11: una busqueda de texto en espanol sobre las
/// respuestas de la capa Channel no devuelve nada (ADR 0017).
/// </summary>
/// <remarks>
/// Recorre las pantallas con datos de verdad —no una respuesta de ejemplo—, porque
/// las frases que se cuelan son las de los caminos raros: la metrica sin dato, la
/// palanca no simulable, la fila rechazada, el error de validacion.
/// </remarks>
internal static partial class SpanishText
{
    /// <summary>
    /// Campos que son dato del usuario y no texto del sistema: la etiqueta que una
    /// persona escribe al cargar puede estar en cualquier idioma.
    /// </summary>
    private static readonly HashSet<string> UserData = new(StringComparer.Ordinal) { "label", "versionLabel", "reason" };

    [GeneratedRegex(
        @"[áéíóúñ¿¡]|\b(?:de|del|la|las|los|el|que|con|para|por|una|sin|esta|este|carga|cargas|archivo|archivos|fuente|fuentes|aplicacion|aplicaciones|columna|columnas|fila|filas|metrica|metricas|peso|puntaje|tramo|portafolio|semanas|invalido|invalida|esperaba|recibio|alcance|falta|faltan|desconocido|encontrada|escenario|palanca|palancas|objetivo|techo|riesgo|asignar|filtrado|usuario|usuarios|sesion|datos)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static async Task CollectAsync(HttpResponseMessage response, string request, List<string> found)
    {
        var media = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var body = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(body) || !media.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var document = JsonDocument.Parse(body);
        Collect(document.RootElement, request, found);
    }

    private static void Collect(JsonElement element, string path, List<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (!UserData.Contains(property.Name))
                    {
                        Collect(property.Value, path + "." + property.Name, found);
                    }
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, $"{path}[{index++}]", found);
                }

                break;

            case JsonValueKind.String:
                var text = element.GetString() ?? string.Empty;
                if (Pattern().IsMatch(text))
                {
                    found.Add($"{path} = {text}");
                }

                break;
        }
    }

    public static string Report(List<string> found)
        => found.Count + " textos en espanol:" + Environment.NewLine
           + string.Join(Environment.NewLine, found.Distinct().Take(25));
}

public sealed class SpanishFreeChannelTests(QueryExperienceFixture fixture) : IClassFixture<QueryExperienceFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] Groupings =
        ["portfolio", "application", "country", "cio", "lob", "disposition", "roadmap"];

    [Fact]
    public async Task NoChannelResponseCarriesSpanishText()
    {
        var client = await fixture.Host.CreateMutatingClientAsync();
        var found = new List<string>();

        async Task Get(string url)
        {
            using var response = await client.GetAsync(new Uri(url, UriKind.Relative));
            await SpanishText.CollectAsync(response, "GET " + url, found);
        }

        async Task Post(HttpClient http, string url, object body)
        {
            using var response = await http.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, Json);
            await SpanishText.CollectAsync(response, "POST " + url, found);
        }

        // ------------------------------------------------------ pantallas de consulta
        await Get("/api/session");
        await Get("/api/portfolio?pageSize=100");
        await Get("/api/portfolio?band=critical&pageSize=100");

        foreach (var grouping in Groupings)
        {
            await Get($"/api/roadmap?groupBy={grouping}");
        }

        await Get("/api/ingestion");
        await Get("/api/lineage");

        var portfolio = await client.GetFromJsonAsync<PortfolioView>(
            new Uri("/api/portfolio?pageSize=40", UriKind.Relative), Json);

        foreach (var row in portfolio!.Applications)
        {
            await Get($"/api/applications/{row.EaiCode}/overview");
            await Get($"/api/applications/{row.EaiCode}/explain");
            await Get($"/api/applications/{row.EaiCode}/whatif");
            await Get($"/api/applications/{row.EaiCode}/roadmap");
            await Get($"/api/applications/{row.EaiCode}/history");
        }

        // ------------------------------------------- escenarios y score objetivo
        foreach (var row in portfolio.Applications.Take(10))
        {
            var lab = await client.GetFromJsonAsync<WhatIfLabView>(
                new Uri($"/api/applications/{row.EaiCode}/whatif", UriKind.Relative), Json);

            if (lab!.Actionable.Count > 1)
            {
                await Post(client, $"/api/applications/{row.EaiCode}/whatif/scenario",
                    new { levers = lab.Actionable.Select(l => new { key = l.Key }).ToArray() });
            }

            await Post(client, $"/api/applications/{row.EaiCode}/whatif/target", new { targetScore = 100m });
            await Post(client, $"/api/applications/{row.EaiCode}/whatif/target", new { targetScore = 20m });
            await Post(client, $"/api/applications/{row.EaiCode}/whatif/target",
                new { targetScore = Math.Min(100m, lab.BaselineScore + 1m) });
        }

        // ------------------------------------------------ errores de validacion
        var first = portfolio.Applications[0].EaiCode;

        await Get("/api/portfolio?page=0");
        await Get("/api/portfolio?band=Excelente");
        await Get("/api/roadmap?groupBy=planeta");
        await Get("/api/applications/abc/overview");
        await Get("/api/applications/9999999999/overview");
        await Get("/api/lineage?version=..%2F..%2Fx");
        await Get("/api/lineage?version=20990101T000000");
        await Post(client, $"/api/applications/{first}/whatif/scenario", new { levers = Array.Empty<object>() });
        await Post(client, $"/api/applications/{first}/whatif/scenario", new { levers = new[] { new { key = "unknown.lever" } } });
        await Post(client, $"/api/applications/{first}/whatif/target", new { targetScore = 5m });

        var withoutCsrf = await fixture.Host.CreateAuthenticatedChannelClientAsync();
        await Post(withoutCsrf, $"/api/applications/{first}/whatif/target", new { targetScore = 90m });

        // El adaptador dummy no admite cargas: el veredicto tambien es un aviso.
        using (var form = new MultipartFormDataContent())
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes("EAI_Code,LOB\n1001,Individual\n"));
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", "inventory.csv");

            using var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), form);
            await SpanishText.CollectAsync(response, "POST /api/ingestion/upload", found);
        }

        Assert.True(found.Count == 0, SpanishText.Report(found));
    }
}

public sealed class SpanishFreeUploadTests(FileBackedApiFixture fixture) : IClassFixture<FileBackedApiFixture>
{
    private static MultipartFormDataContent Form(IEnumerable<(string Name, byte[] Content)> files, string label)
    {
        var form = new MultipartFormDataContent();

        foreach (var (name, content) in files)
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            form.Add(part, "files", name);
        }

        form.Add(new StringContent(label), "label");
        return form;
    }

    /// <summary>
    /// Los caminos de la carga que mas texto generaban: el archivo ambiguo, el que no
    /// se reconoce, el que no es CSV, las filas rechazadas y el linaje que las muestra.
    /// </summary>
    [Fact]
    public async Task UploadVerdictsAndLineageCarryNoSpanishText()
    {
        var client = await fixture.CreateMutatingClientAsync();
        var found = new List<string>();

        // Una carga que se rechaza por varios motivos a la vez.
        using (var rejected = Form(
            [
                ("ambiguous.csv", Encoding.UTF8.GetBytes("EAI,Country\n1001,Chile\n")),
                ("unknown.csv", Encoding.UTF8.GetBytes("A,B\n1,2\n")),
                ("empty.csv", []),
            ],
            "Rejected on purpose"))
        using (var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), rejected))
        {
            await SpanishText.CollectAsync(response, "POST upload (rechazada)", found);
        }

        // Una carga aceptada con filas malas: rechazos por tipo y por clave vacia.
        var files = fixture.ReadSeedFiles()
            .Select((file, index) => file.Name == "product_strategy.csv"
                ? ($"export {index}.csv", Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(file.Content) + "1001,abc,def,x,y\n,1,1,1,1\n"))
                : ($"export {index}.csv", file.Content))
            .ToList();

        using (var accepted = Form(files, "Accepted with bad rows"))
        using (var response = await client.PostAsync(new Uri("/api/ingestion/upload", UriKind.Relative), accepted))
        {
            await SpanishText.CollectAsync(response, "POST upload (aceptada)", found);
        }

        using (var ingestion = await client.GetAsync(new Uri("/api/ingestion", UriKind.Relative)))
        {
            await SpanishText.CollectAsync(ingestion, "GET /api/ingestion", found);
        }

        using (var lineage = await client.GetAsync(new Uri("/api/lineage", UriKind.Relative)))
        {
            await SpanishText.CollectAsync(lineage, "GET /api/lineage", found);
        }

        Assert.True(found.Count == 0, SpanishText.Report(found));
    }
}

/// <summary>
/// El contrato entre el backend y el catalogo de frases de la SPA: un codigo sin
/// frase no puede llegar a produccion.
/// </summary>
public sealed class NoticeCatalogTests
{
    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PLAN.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    private static string Catalog()
        => File.ReadAllText(RepositoryFile("src", "web", "src", "i18n", "notices.ts"));

    [Fact]
    public void EveryNoticeCodeHasASentenceInTheInterface()
    {
        var catalog = Catalog();

        var codes = typeof(NoticeCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);

        var missing = codes.Where(code => !catalog.Contains($"'{code}':", StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, "Codigos sin frase en notices.ts: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryScoreBandHasANameInTheInterface()
    {
        var catalog = Catalog();

        var missing = ScoreBands.All
            .Select(band => band.Code)
            .Where(code => !catalog.Contains($"'{code}':", StringComparison.Ordinal)
                && !catalog.Contains($"  {code}:", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, "Tramos sin nombre en notices.ts: " + string.Join(", ", missing));
    }
}
