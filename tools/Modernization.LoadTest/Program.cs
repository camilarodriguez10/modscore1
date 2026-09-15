using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

// Prueba de carga minima y sin dependencias. Mide la capa System directamente (el
// costo del motor y de las fuentes) y la Channel a traves de una sesion de
// desarrollo (el costo completo de una pantalla).
//
// Los numeros son de la maquina donde corre: sirven para comparar un cambio contra
// otro y para encontrar amplificaciones, no para dimensionar produccion.
//
// Uso:
//   dotnet run --project tools/Modernization.LoadTest -- --duration 15 --concurrency 16
//
// La Channel limita a 120 peticiones por minuto por usuario. Para medirla hay que
// levantarla con RateLimiting__RequestsPerMinute alto; si no, el resultado muestra
// sobre todo 429, que tambien es un dato.

var options = LoadOptions.Parse(args);

// Revocacion de certificados comprobada aunque hoy se apunte a HTTP local: la
// herramienta tambien se usa contra entornos con TLS.
using var systemHandler = new HttpClientHandler { CheckCertificateRevocationList = true };
using var system = new HttpClient(systemHandler) { BaseAddress = options.SystemUrl, Timeout = TimeSpan.FromSeconds(60) };
system.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceToken);
system.DefaultRequestHeaders.TryAddWithoutValidation("X-Portfolio-Scope", "*");

// Administrar reglas exige el permiso reenviado por la capa Channel (defensa en
// profundidad): la System lo acepta solo de un llamador con token de servicio.
using var rulesAdmin = new HttpClient(systemHandler, disposeHandler: false) { BaseAddress = options.SystemUrl, Timeout = TimeSpan.FromSeconds(120) };
rulesAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceToken);
rulesAdmin.DefaultRequestHeaders.TryAddWithoutValidation("X-Portfolio-Scope", "*");
rulesAdmin.DefaultRequestHeaders.TryAddWithoutValidation("X-Portfolio-Grants", "rules.admin");

var eaiCodes = await FetchEaiCodesAsync(system).ConfigureAwait(false);
var activeRules = await FetchActiveRulesAsync(rulesAdmin).ConfigureAwait(false);
Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"Portafolio: {eaiCodes.Count} aplicaciones · {options.Concurrency} usuarios concurrentes · {options.Duration.TotalSeconds:0} s por escenario"));

using var channelHandler = new HttpClientHandler { UseCookies = false, CheckCertificateRevocationList = true };
using var channel = new HttpClient(channelHandler) { BaseAddress = options.ChannelUrl, Timeout = TimeSpan.FromSeconds(60) };
var channelAvailable = await TryLoginAsync(channel).ConfigureAwait(false);

var scenarios = new List<Scenario>
{
    new("System · GET /system/scores (200 por pagina)",
        () => system.GetAsync(new Uri("/system/scores?pageSize=200", UriKind.Relative))),
    new("System · GET /system/applications/{eai}/score",
        () => system.GetAsync(new Uri($"/system/applications/{Pick(eaiCodes)}/score", UriKind.Relative))),
    new("System · POST what-if (MFA)",
        () => system.PostAsJsonAsync(
            new Uri($"/system/applications/{Pick(eaiCodes)}/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "mfa.enable", parameter = (int?)null } } })),
    new("System · GET /system/history/portfolio",
        () => system.GetAsync(new Uri("/system/history/portfolio", UriKind.Relative))),
    new("System · GET /system/rules (versiones)",
        () => rulesAdmin.GetAsync(new Uri("/system/rules", UriKind.Relative))),
    // El caso caro de la Fase 12: simular un cambio de pesos sobre todo el portafolio,
    // con la proyeccion de los planes antes y despues.
    new("System · POST /system/rules/simulate (Kenna 5, MFA 10)",
        () => rulesAdmin.PostAsJsonAsync(
            new Uri("/system/rules/simulate", UriKind.Relative),
            new
            {
                basedOn = activeRules,
                draft = new { metrics = new[] { new { key = "kenna", weight = 5m }, new { key = "mfa", weight = 10m } } },
            })),
};

if (channelAvailable)
{
    scenarios.Add(new("Channel · GET /api/portfolio",
        () => channel.GetAsync(new Uri("/api/portfolio?pageSize=25", UriKind.Relative))));
    scenarios.Add(new("Channel · GET /api/applications/{eai}/explain",
        () => channel.GetAsync(new Uri($"/api/applications/{Pick(eaiCodes)}/explain", UriKind.Relative))));
    scenarios.Add(new("Channel · GET /api/ingestion",
        () => channel.GetAsync(new Uri("/api/ingestion", UriKind.Relative))));
}
else
{
    Console.WriteLine("Channel no disponible con sesion de desarrollo: se omiten sus escenarios.");
}

var results = new List<ScenarioResult>();

foreach (var scenario in scenarios)
{
    // Una peticion de calentamiento: el primer calculo llena caches y no es
    // representativo del estado estable. Su latencia se reporta aparte ("primera"):
    // es lo que espera quien abre la pantalla justo despues de una carga o de publicar
    // reglas.
    var first = Stopwatch.GetTimestamp();

    using (var warm = await scenario.Send().ConfigureAwait(false))
    {
        await warm.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    var firstMs = Stopwatch.GetElapsedTime(first).TotalMilliseconds;

    results.Add(await RunAsync(scenario, options.Concurrency, options.Duration).ConfigureAwait(false) with { First = firstMs });
}

Console.WriteLine();
Console.WriteLine("| Escenario | Primera ms | Peticiones | Errores | 429 | req/s | p50 ms | p95 ms | p99 ms |");
Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");

foreach (var result in results)
{
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"| {result.Name} | {result.First:0.0} | {result.Requests} | {result.Errors} | {result.Throttled} | {result.RequestsPerSecond:0.0} | {result.P50:0.0} | {result.P95:0.0} | {result.P99:0.0} |"));
}

return results.Any(r => r.Errors > 0) ? 1 : 0;

static string Pick(IReadOnlyList<string> values) => values[RandomNumberGenerator.GetInt32(values.Count)];

static async Task<IReadOnlyList<string>> FetchEaiCodesAsync(HttpClient system)
{
    using var response = await system.GetAsync(new Uri("/system/applications?pageSize=200", UriKind.Relative)).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

    return [.. document.RootElement.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("eaiCode").GetString()!)];
}

static async Task<string> FetchActiveRulesAsync(HttpClient rulesAdmin)
{
    using var response = await rulesAdmin.GetAsync(new Uri("/system/rules", UriKind.Relative)).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

    return document.RootElement.GetProperty("active").GetProperty("id").GetString()!;
}

// La cookie de sesion es Secure y __Host-: el CookieContainer de .NET no la envia por
// HTTP, ni siquiera a localhost, y cada peticion saldria anonima. Se reenvia a mano,
// como la enviaria el navegador sobre HTTPS, y se comprueba antes de medir.
static async Task<bool> TryLoginAsync(HttpClient channel)
{
    try
    {
        using var login = await channel.GetAsync(new Uri("/api/login", UriKind.Relative)).ConfigureAwait(false);

        if (!login.IsSuccessStatusCode || !login.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return false;
        }

        channel.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));

        using var session = await channel.GetAsync(new Uri("/api/session", UriKind.Relative)).ConfigureAwait(false);
        return session.IsSuccessStatusCode;
    }
    catch (HttpRequestException)
    {
        return false;
    }
}

static async Task<ScenarioResult> RunAsync(Scenario scenario, int concurrency, TimeSpan duration)
{
    var latencies = new ConcurrentBag<double>();
    var errors = 0;
    var throttled = 0;
    var deadline = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
    var clock = Stopwatch.StartNew();

    var workers = Enumerable.Range(0, concurrency).Select(async _ =>
    {
        while (Stopwatch.GetTimestamp() < deadline)
        {
            var started = Stopwatch.GetTimestamp();

            try
            {
                using var response = await scenario.Send().ConfigureAwait(false);

                // El cuerpo se lee completo: serializar tambien es parte del costo.
                await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Interlocked.Increment(ref throttled);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref errors);
                }
            }
            catch (HttpRequestException)
            {
                Interlocked.Increment(ref errors);
            }
            catch (TaskCanceledException)
            {
                Interlocked.Increment(ref errors);
            }

            latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    });

    await Task.WhenAll(workers).ConfigureAwait(false);

    return ScenarioResult.From(scenario.Name, [.. latencies], errors, throttled, clock.Elapsed);
}

internal sealed record Scenario(string Name, Func<Task<HttpResponseMessage>> Send);

internal sealed record ScenarioResult(
    string Name,
    int Requests,
    int Errors,
    int Throttled,
    double RequestsPerSecond,
    double P50,
    double P95,
    double P99)
{
    /// <summary>Latencia de la primera peticion, con caches frias.</summary>
    public double First { get; init; }

    public static ScenarioResult From(string name, double[] latencies, int errors, int throttled, TimeSpan elapsed)
    {
        Array.Sort(latencies);

        return new ScenarioResult(
            name,
            latencies.Length,
            errors,
            throttled,
            latencies.Length / elapsed.TotalSeconds,
            Percentile(latencies, 0.50),
            Percentile(latencies, 0.95),
            Percentile(latencies, 0.99));
    }

    private static double Percentile(double[] sorted, double quantile)
        => sorted.Length == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(quantile * sorted.Length) - 1)];
}

internal sealed record LoadOptions(Uri SystemUrl, Uri ChannelUrl, string ServiceToken, TimeSpan Duration, int Concurrency)
{
    public static LoadOptions Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        return new LoadOptions(
            new Uri(Value("--system") ?? "http://localhost:5101"),
            new Uri(Value("--channel") ?? "http://localhost:5100"),
            // El token no va en la linea de comandos: quedaria en el historial.
            Environment.GetEnvironmentVariable("MODSCORE_SERVICE_TOKEN")
                ?? "dev-only-service-token-not-for-any-real-environment",
            TimeSpan.FromSeconds(int.Parse(Value("--duration") ?? "15", CultureInfo.InvariantCulture)),
            int.Parse(Value("--concurrency") ?? "16", CultureInfo.InvariantCulture));
    }
}
