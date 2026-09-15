using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;
using Modernization.Rules;
using Modernization.Security;
using Modernization.SystemApi.Contracts;
using Modernization.SystemApi.Services;

namespace Modernization.Api.Tests;

/// <summary>Captura los eventos de la categoria de auditoria, con su EventId.</summary>
public sealed class AuditLog : ILoggerProvider
{
    private readonly ConcurrentQueue<(int EventId, string Message)> _entries = new();

    public IReadOnlyList<(int EventId, string Message)> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName)
        => string.Equals(categoryName, SecurityAudit.Category, StringComparison.Ordinal)
            ? new Logger(_entries)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class Logger(ConcurrentQueue<(int EventId, string Message)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue((eventId.Id, formatter(state, exception)));
    }
}

/// <summary>Hosts y cuerpos de las pruebas de administracion de reglas.</summary>
internal static class RulesHosts
{
    public const string Reason = "Move weight from Kenna to MFA";

    /// <summary>Tres puntos de peso de Kenna a MFA: el total sigue en 100.</summary>
    public static readonly object KennaToMfa = new
    {
        metrics = new[] { new { key = "kenna", weight = 5m }, new { key = "mfa", weight = 10m } },
    };

    /// <summary>
    /// Cada prueba levanta su host: publicar cambia las reglas, y una prueba no puede
    /// heredar las de otra. Las reglas se releen en cada peticion.
    /// </summary>
    public static TwoLayerHost Create(
        IReadOnlyDictionary<string, string?>? channelSettings = null,
        Action<IServiceCollection>? channelServices = null,
        AuditLog? systemAudit = null) => new(
        new Dictionary<string, string?>
        {
            ["DataSource:Provider"] = "Dummy",
            ["DataSource:ApplicationCount"] = "40",
            ["Rules:RefreshSeconds"] = "0",
        },
        channelSettings,
        channelServices,
        systemAudit is null ? null : services => services.AddSingleton<ILoggerProvider>(systemAudit));

    public static object Publish(string basedOn, object? draft = null, string reason = Reason)
        => new { basedOn, reason, draft = draft ?? KennaToMfa };

    public static object Simulate(string basedOn, object? draft = null)
        => new { basedOn, draft = draft ?? KennaToMfa };

    public static Dictionary<string, string?> Access(string scope, params string[] roles)
    {
        var settings = new Dictionary<string, string?> { ["Authentication:Development:Scopes:0"] = scope };

        for (var i = 0; i < roles.Length; i++)
        {
            settings[string.Create(CultureInfo.InvariantCulture, $"Authentication:Development:Roles:{i}")] = roles[i];
        }

        return settings;
    }

    /// <summary>El borrador que arma la pantalla a partir de un documento, sin tocar nada.</summary>
    public static RulesDraft DraftOf(RulesDocument document) => new(
        [.. document.Metrics.Select(m => new MetricDraft(m.Key, m.Weight, m.Bands, m.Cases, m.CategoryScores))],
        [.. document.Levers.Select(l => new LeverDraft(l.Key, l.Enabled, l.DefaultParameter))],
        new RoadmapDraft(
            document.Roadmap.HealthyScore,
            document.Roadmap.SignificantUserBase,
            [.. document.Roadmap.Roadmaps.Select(r => new RoadmapPathDraft(r.Key, r.Levers, [.. r.Milestones.Select(m => m.Weeks)]))]));
}

/// <summary>
/// Fase 12, punto 7: ajustar las formulas y los escenarios desde el portal, solo
/// administradores. Criterio de aceptacion: cambiar un peso desde el portal mueve el
/// score del portafolio, queda auditado con su impacto y no requiere recompilar ni
/// desplegar.
/// </summary>
public sealed class RulesAdministrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static Uri Api(string path) => new(path, UriKind.Relative);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<RulesPublishResponse> PublishAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/publish"), body, Json);
        return await ReadAsync<RulesPublishResponse>(response);
    }

    private static async Task<ScoreOverviewView> OverviewAsync(HttpClient client, string eaiCode)
        => (await client.GetFromJsonAsync<ScoreOverviewView>(Api($"/api/applications/{eaiCode}/overview"), Json))!;

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return problem.GetProperty("type").GetString()!.Replace(ApiProblem.TypePrefix, string.Empty, StringComparison.Ordinal);
    }

    private static string Explain(IReadOnlyList<Notice> errors) => string.Join(" | ", errors);

    // ------------------------------------------------------------ acceso

    [Fact]
    public async Task OnlyAFullScopeRulesAdministratorIsOfferedTheRules()
    {
        using var admin = RulesHosts.Create();
        using var uploader = RulesHosts.Create(RulesHosts.Access("*", "portfolio.read", "ingestion.write"));
        using var scoped = RulesHosts.Create(RulesHosts.Access("lob:Individual", "portfolio.read", "ruleset.admin"));

        foreach (var (host, expected) in new[] { (admin, true), (uploader, false), (scoped, false) })
        {
            var client = await host.CreateAuthenticatedChannelClientAsync();
            var session = await client.GetFromJsonAsync<SessionView>(Api("/api/session"), Json);

            Assert.Equal(expected, session!.Permissions.CanAdministerRules);

            using var rules = await client.GetAsync(Api("/api/admin/rules"));
            Assert.Equal(expected ? HttpStatusCode.OK : HttpStatusCode.Forbidden, rules.StatusCode);
        }

        using var anonymous = await admin.CreateChannelClient().GetAsync(Api("/api/admin/rules"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    /// <summary>
    /// Defensa en profundidad: la capa System no confia en que la Channel haya
    /// comprobado. Sin el permiso reenviado no hay reglas, y sin la reautenticacion
    /// reenviada no hay publicacion, aun con alcance completo.
    /// </summary>
    [Fact]
    public async Task TheSystemLayerChecksTheForwardedGrantsItself()
    {
        using var host = RulesHosts.Create();

        var system = host.CreateSystemClient();

        using (var denied = await system.GetAsync(Api("/system/rules")))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("insufficient-permission", await ProblemCodeAsync(denied));
        }

        system.DefaultRequestHeaders.TryAddWithoutValidation(ForwardedGrants.HeaderName, ForwardedGrants.RulesAdmin);

        using (var allowed = await system.GetAsync(Api("/system/rules")))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using (var publish = await system.PostAsJsonAsync(Api("/system/rules/publish"), RulesHosts.Publish("v1.3"), Json))
        {
            Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
            Assert.Equal("step-up-required", await ProblemCodeAsync(publish));
        }

        var partial = host.CreateSystemClient(scope: "lob:Individual");
        partial.DefaultRequestHeaders.TryAddWithoutValidation(ForwardedGrants.HeaderName, "rules.admin reauthenticated");

        using (var scoped = await partial.GetAsync(Api("/system/rules")))
        {
            Assert.Equal(HttpStatusCode.Forbidden, scoped.StatusCode);
        }

        var anonymous = host.CreateSystemClient(authenticated: false);
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation(ForwardedGrants.HeaderName, "rules.admin reauthenticated");

        using var unauthenticated = await anonymous.GetAsync(Api("/system/rules"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    // --------------------------------------------------------- pantalla

    [Fact]
    public async Task AnAdministratorStartsFromTheBuiltInRules()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<RulesAdminView>(Api("/api/admin/rules"), Json);

        Assert.NotNull(view);
        Assert.Equal("v1.3", view.Active.Id);
        Assert.True(view.Active.IsBuiltIn);
        Assert.True(view.Active.IsActive);
        Assert.Equal("Memory", view.Store);
        Assert.True(view.Reauthenticated);
        Assert.Equal(Modernization.Scoring.Engine.RulesetCatalog.Default.Metrics.Count, view.Document.Metrics.Count);
        Assert.Equal(100m, view.Document.Metrics.Sum(m => m.Weight));
        Assert.Equal(Modernization.WhatIf.LeverCatalog.Keys.Count, view.Document.Levers.Count);
        Assert.Equal(7, view.Document.Roadmap.Roadmaps.Count);
        Assert.Equal(["v1.3"], view.Versions.Select(v => v.Id));
    }

    [Fact]
    public async Task AnInvalidDraftComesBackWithEveryProblemAsACode()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var draft = new
        {
            metrics = new[] { new { key = "mfa", weight = 10m, bands = new[] { new { min = 0m, max = 100m, score = 100m, label = "all" } } } },
        };

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3", draft), Json);
        var result = await ReadAsync<RulesSimulationResponse>(response);

        Assert.False(result.Valid);
        Assert.Null(result.Impact);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.RulesKindMismatch);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.RulesWeightTotal);
    }

    [Fact]
    public async Task SimulatingShowsTheImpactAndPublishesNothing()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3"), Json);
        var result = await ReadAsync<RulesSimulationResponse>(response);

        Assert.True(result.Valid, Explain(result.Errors));
        Assert.False(result.Stale);
        Assert.Equal(2, result.Changes.Count);
        Assert.NotNull(result.Impact);
        Assert.Equal(40, result.Impact.Applications);
        Assert.True(result.Impact.Up + result.Impact.Down > 0);

        var overview = await OverviewAsync(client, "14381");
        Assert.Equal("v1.3", overview.RulesetVersion);
        Assert.Equal(79.67m, overview.Score);
    }

    // ----------------------------------------------------- publicacion

    /// <summary>El criterio de aceptacion de la Fase 12, de punta a punta.</summary>
    [Fact]
    public async Task PublishingAWeightMovesThePortfolioIsAuditedAndNeedsNoRedeploy()
    {
        var audit = new AuditLog();
        using var host = RulesHosts.Create(systemAudit: audit);
        var client = await host.CreateMutatingClientAsync();

        var before = await client.GetFromJsonAsync<PortfolioView>(Api("/api/portfolio?pageSize=100"), Json);

        var published = await PublishAsync(client, RulesHosts.Publish("v1.3"));

        Assert.True(published.Published, Explain(published.Errors));
        Assert.Equal("v1.4", published.Version!.Id);
        Assert.Equal("v1.3", published.Version.BasedOn);
        Assert.Equal("dev-user", published.Version.PublishedBy);
        Assert.Equal(2, published.Changes.Count);

        // Mueve el score del portafolio, en el mismo proceso y sin reiniciar nada.
        var after = await client.GetFromJsonAsync<PortfolioView>(Api("/api/portfolio?pageSize=100"), Json);
        Assert.NotEqual(before!.AverageScore, after!.AverageScore);

        // Cada score sale de la version nueva, con el mismo motor que simulo el impacto.
        var snapshot = await host.SystemServices.GetRequiredService<PortfolioService>().FindAsync("14381", PortfolioScope.Full);
        var expected = RulesEditor
            .Apply(RulesModel.BuiltIn, new RulesDraft(Metrics: [new MetricDraft("kenna", 5), new MetricDraft("mfa", 10)]))
            .Candidate!.Engine.Calculate(snapshot!).Score;

        var overview = await OverviewAsync(client, "14381");
        Assert.Equal("v1.4", overview.RulesetVersion);
        Assert.Equal(expected, overview.Score);

        // Queda auditado con su impacto.
        var entry = Assert.Single(audit.Entries, e => e.EventId == 4211);
        Assert.Contains("version=v1.4", entry.Message, StringComparison.Ordinal);
        Assert.Contains("base=v1.3", entry.Message, StringComparison.Ordinal);
        Assert.Contains("cambios=2", entry.Message, StringComparison.Ordinal);

        // Y guardado con quien, por que, que cambio y su impacto.
        var state = await client.GetFromJsonAsync<RulesAdminView>(Api("/api/admin/rules"), Json);
        Assert.Equal("v1.4", state!.Active.Id);
        Assert.Equal(["v1.4", "v1.3"], state.Versions.Select(v => v.Id));
        Assert.Equal(RulesHosts.Reason, state.Active.Reason);
        Assert.Equal(2, state.Active.Changes.Count);
        Assert.NotNull(state.Active.Impact);
    }

    /// <summary>Volver atras es publicar otra vez lo que habia: nada se edita ni se borra.</summary>
    [Fact]
    public async Task RestoringTheBuiltInRulesPublishesThemAgainAsANewVersion()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        Assert.True((await PublishAsync(client, RulesHosts.Publish("v1.3"))).Published);

        var previous = await client.GetFromJsonAsync<RulesVersionDocumentResponse>(Api("/api/admin/rules/versions/v1.3"), Json);
        Assert.False(previous!.Version.IsActive);

        var restore = await PublishAsync(
            client,
            RulesHosts.Publish("v1.4", RulesHosts.DraftOf(previous.Document), "Roll back to the BRD weights"));

        Assert.True(restore.Published, Explain(restore.Errors));
        Assert.Equal("v1.5", restore.Version!.Id);
        Assert.Contains(restore.Changes, c => c.Code == NoticeCodes.ChangeWeight && c.Args["metric"] == "Kenna Score" && c.Args["to"] == "8");

        var overview = await OverviewAsync(client, "14381");
        Assert.Equal("v1.5", overview.RulesetVersion);
        Assert.Equal(79.67m, overview.Score);
    }

    /// <summary>
    /// ASVS 3.7.1: una sesion abierta hace rato no alcanza para publicar. Reautenticarse
    /// si, y las dos cosas quedan auditadas.
    /// </summary>
    [Fact]
    public async Task PublishingNeedsARecentSignInAndStepUpGrantsIt()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var audit = new AuditLog();

        using var host = RulesHosts.Create(channelServices: services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<ILoggerProvider>(audit);
        });

        var client = await host.CreateMutatingClientAsync();

        clock.Advance(TimeSpan.FromMinutes(11));

        using (var stale = await client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json))
        {
            Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
            Assert.Equal("step-up-required", await ProblemCodeAsync(stale));
        }

        var view = await client.GetFromJsonAsync<RulesAdminView>(Api("/api/admin/rules"), Json);
        Assert.False(view!.Reauthenticated);

        using (var stepUp = await client.GetAsync(Api("/api/login/step-up?returnTo=/admin/rules")))
        {
            Assert.Equal(HttpStatusCode.Redirect, stepUp.StatusCode);
            Assert.Equal("/admin/rules", stepUp.Headers.Location?.OriginalString);
        }

        var published = await PublishAsync(client, RulesHosts.Publish("v1.3"));

        Assert.True(published.Published, Explain(published.Errors));
        Assert.Contains(audit.Entries, e => e.EventId == 4104);
        Assert.Contains(audit.Entries, e => e.EventId == 4004);
    }

    [Theory]
    [InlineData("https://evil.example/admin/rules")]
    [InlineData("//evil.example")]
    [InlineData("/admin/rules/../../logout")]
    public async Task StepUpNeverSendsTheUserOffTheSite(string returnTo)
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(Api("/api/login/step-up?returnTo=" + Uri.EscapeDataString(returnTo)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PublishingWithoutTheCsrfTokenIsRejected()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateAuthenticatedChannelClientAsync();

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("csrf", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task ADraftOnAnOlderVersionOrWithoutChangesOrReasonIsNotPublished()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var noReason = await PublishAsync(client, RulesHosts.Publish("v1.3", reason: "ok"));
        Assert.Equal(NoticeCodes.RulesReasonInvalid, Assert.Single(noReason.Errors).Code);

        var noChanges = await PublishAsync(client, RulesHosts.Publish("v1.3", draft: new { }));
        Assert.Equal(NoticeCodes.RulesNoChanges, Assert.Single(noChanges.Errors).Code);

        Assert.True((await PublishAsync(client, RulesHosts.Publish("v1.3"))).Published);

        var stale = await PublishAsync(client, RulesHosts.Publish("v1.3"));
        var error = Assert.Single(stale.Errors);

        Assert.False(stale.Published);
        Assert.Equal(NoticeCodes.RulesStale, error.Code);
        Assert.Equal("v1.4", error.Args["active"]);

        var state = await client.GetFromJsonAsync<RulesAdminView>(Api("/api/admin/rules"), Json);
        Assert.Equal(2, state!.Versions.Count);
    }

    [Fact]
    public async Task TheEdgeRejectsUnknownFieldsOversizedBodiesAndMalformedVersions()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        async Task<(HttpStatusCode Status, string Code)> PostAsync(HttpContent content)
        {
            using var response = await client.PostAsync(Api("/api/admin/rules/simulate"), content);
            return (response.StatusCode, await ProblemCodeAsync(response));
        }

        using (var unknown = JsonContent.Create(new { basedOn = "v1.3", draft = new { }, publishNow = true }, options: Json))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "invalid-body"), await PostAsync(unknown));
        }

        using (var missing = JsonContent.Create(new { basedOn = "v1.3" }, options: Json))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "invalid-body"), await PostAsync(missing));
        }

        using (var oversized = new StringContent(
            "{\"basedOn\":\"v1.3\",\"draft\":{},\"padding\":\"" + new string('a', 200_000) + "\"}",
            Encoding.UTF8,
            "application/json"))
        {
            Assert.Equal((HttpStatusCode.RequestEntityTooLarge, "body-too-large"), await PostAsync(oversized));
        }

        using (var text = new StringContent("{}", Encoding.UTF8, "text/plain"))
        {
            Assert.Equal((HttpStatusCode.UnsupportedMediaType, "unsupported-media-type"), await PostAsync(text));
        }

        using (var traversal = JsonContent.Create(new { basedOn = "../../etc", draft = new { } }, options: Json))
        {
            Assert.Equal((HttpStatusCode.BadRequest, "invalid-rules-version"), await PostAsync(traversal));
        }

        using var version = await client.GetAsync(Api("/api/admin/rules/versions/..%2F..%2Fx"));
        Assert.Equal(HttpStatusCode.BadRequest, version.StatusCode);
    }

    [Fact]
    public async Task PublishingHasItsOwnLowLimit()
    {
        using var host = RulesHosts.Create(new Dictionary<string, string?> { ["RateLimiting:RulesPublishesPerWindow"] = "2" });
        var client = await host.CreateMutatingClientAsync();

        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);

        // Agotar las publicaciones no bloquea la simulacion ni la lectura.
        using var simulate = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.4"), Json);
        Assert.Equal(HttpStatusCode.OK, simulate.StatusCode);
    }

    /// <summary>
    /// Otra replica sobre el mismo almacen toma la version nueva en su siguiente
    /// comprobacion, sin reiniciar: es lo que "no requiere desplegar" significa con mas
    /// de un proceso.
    /// </summary>
    [Fact]
    public async Task AnotherReplicaTakesThePublicationWithoutRestarting()
    {
        var data = Path.Combine(
            Path.GetTempPath(),
            "modscore-replicas-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        FileBackedApiFixture.SeedVersion(data);

        try
        {
            var settings = new Dictionary<string, string?>
            {
                ["DataSource:Provider"] = "File",
                ["DataSource:Directory"] = data,
                ["Rules:RefreshSeconds"] = "0",
            };

            using var first = new TwoLayerHost(settings);
            using var second = new TwoLayerHost(settings);

            var writer = await first.CreateMutatingClientAsync();
            var reader = await second.CreateAuthenticatedChannelClientAsync();

            var portfolio = await reader.GetFromJsonAsync<PortfolioView>(Api("/api/portfolio?pageSize=1"), Json);
            var eai = portfolio!.Applications[0].EaiCode;

            Assert.Equal("v1.3", (await OverviewAsync(reader, eai)).RulesetVersion);

            Assert.True((await PublishAsync(writer, RulesHosts.Publish("v1.3"))).Published);

            Assert.Equal("v1.4", (await OverviewAsync(reader, eai)).RulesetVersion);
            Assert.True(File.Exists(Path.Combine(data, ".rules", "v1.4", "version.json")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(data))
            {
                Directory.Delete(data, recursive: true);
            }
        }
    }

    /// <summary>La pantalla nueva cumple el criterio de la Fase 11: ningun texto en espanol.</summary>
    [Fact]
    public async Task RulesAdministrationCarriesNoSpanishText()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();
        var found = new List<string>();

        async Task CollectAsync(Task<HttpResponseMessage> request, string name)
        {
            using var response = await request;
            await SpanishText.CollectAsync(response, name, found);
        }

        var invalid = new { metrics = new[] { new { key = "mfa", weight = 10m } } };

        await CollectAsync(client.GetAsync(Api("/api/admin/rules")), "GET rules");
        await CollectAsync(client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3", invalid), Json), "simulate invalid");
        await CollectAsync(client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3"), Json), "simulate");
        await CollectAsync(client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json), "publish");
        await CollectAsync(client.PostAsJsonAsync(Api("/api/admin/rules/publish"), RulesHosts.Publish("v1.3"), Json), "publish stale");
        await CollectAsync(client.GetAsync(Api("/api/admin/rules")), "GET rules after");
        await CollectAsync(client.GetAsync(Api("/api/admin/rules/versions/v1.3")), "GET version");
        await CollectAsync(client.GetAsync(Api("/api/admin/rules/versions/v9.9")), "GET missing version");

        Assert.True(found.Count == 0, SpanishText.Report(found));
    }
}
