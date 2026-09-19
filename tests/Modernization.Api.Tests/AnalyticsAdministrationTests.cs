using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// ADR 0025 de punta a punta: las definiciones de las seis preguntas se editan desde el
/// portal con el mismo borrador de las reglas, se publican con ellas y el dashboard y la
/// pantalla Insights las usan sin desplegar. Y explican sus calculos con esos valores.
/// </summary>
public sealed class AnalyticsAdministrationTests
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

    private static decimal ValueOf(Insights.AnalyticsDocument document, string key)
        => document.Parameters.Single(p => p.Key == key).Value;

    [Fact]
    public async Task TheAdministratorSeesEveryDefinitionWithItsRange()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<RulesAdminView>(Api("/api/admin/rules"), Json);
        var analytics = view!.Document.Analytics;

        Assert.Equal(Insights.AnalyticsCatalog.Parameters.Count, analytics.Parameters.Count);
        Assert.Equal(Insights.AnalyticsCatalog.Areas, analytics.Areas.Select(a => a.Key));
        Assert.All(analytics.Parameters, p => Assert.InRange(p.Value, p.Min, p.Max));
        Assert.Equal(60m, ValueOf(analytics, "currency.legacyBelow"));
    }

    /// <summary>El criterio de aceptacion de D3: cambiar un umbral desde el portal mueve el dashboard sin desplegar.</summary>
    [Fact]
    public async Task PublishingADefinitionMovesTheDashboardAndInsightsButNoScore()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var before = await client.GetFromJsonAsync<DashboardResponse>(Api("/api/dashboard"), Json);

        var draft = new
        {
            analytics = new
            {
                values = new Dictionary<string, decimal>
                {
                    ["currency.legacyBelow"] = 100m,
                    ["risk.hotspotCount"] = 2m,
                    ["insights.duplicateDefault"] = 5m,
                },
                areas = new[] { new { key = "currency", status = "agreed", owner = "CMT data owner" } },
            },
        };

        using var response = await client.PostAsJsonAsync(
            Api("/api/admin/rules/publish"),
            RulesHosts.Publish("v1.3", draft, "Legacy below 100 for the review"),
            Json);
        var published = await ReadAsync<RulesPublishResponse>(response);

        Assert.True(published.Published, string.Join(" | ", published.Errors));
        Assert.Contains(published.Changes, c => c.Code == NoticeCodes.ChangeAnalytics);
        Assert.Contains(published.Changes, c => c.Code == NoticeCodes.ChangeAnalyticsStatus);
        Assert.Equal(0, published.Impact!.Up + published.Impact.Down);

        var after = await client.GetFromJsonAsync<DashboardResponse>(Api("/api/dashboard"), Json);

        Assert.Equal("v1.4", after!.DefinitionsVersion);
        Assert.Equal(100m, ValueOf(after.Definitions, "currency.legacyBelow"));
        Assert.Equal("agreed", after.Definitions.Areas.Single(a => a.Key == "currency").Status);
        Assert.Equal("CMT data owner", after.Definitions.Areas.Single(a => a.Key == "currency").Owner);
        Assert.Equal(2, after.Dashboard.Risk.Hotspots.Count);

        // Con el umbral en 100 es legada toda aplicacion con Currency Score menor que 100:
        // muchas mas que con 60, y ninguna de las que tienen exactamente 100.
        var currencyBefore = before!.Dashboard.Kpis.Single(k => k.Key == "currency");
        var currency = after.Dashboard.Kpis.Single(k => k.Key == "currency");
        Assert.True(currency.Count > currencyBefore.Count, $"{currencyBefore.Count} -> {currency.Count}");
        Assert.Equal(
            after.Dashboard.Applications.Count(a => a.Currency is < 100m),
            currency.Count);

        // Ningun score se movio: una definicion no es una regla de score.
        Assert.Equal(before.Dashboard.Health.AverageScore, after.Dashboard.Health.AverageScore);

        var insights = await client.GetFromJsonAsync<InsightsResponse>(Api("/api/insights"), Json);
        Assert.Equal(5, insights!.Options.DuplicateThreshold);
        Assert.Equal(2m, ValueOf(insights.Definitions, "risk.hotspotCount"));
    }

    [Fact]
    public async Task AnInvalidDefinitionIsRejectedWithItsCode()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var draft = new
        {
            analytics = new
            {
                values = new Dictionary<string, decimal> { ["risk.highFrom"] = 90m, ["currency.legacyBelow"] = 0m },
            },
        };

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3", draft), Json);
        var result = await ReadAsync<RulesSimulationResponse>(response);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == NoticeCodes.RulesAnalyticsValue && e.Args["parameter"] == "currency.legacyBelow");
    }

    /// <summary>Un campo que el contrato no conoce se rechaza, tambien dentro de las definiciones.</summary>
    [Fact]
    public async Task AnUnknownFieldInTheDefinitionsIsRejected()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var draft = new { analytics = new { values = new Dictionary<string, decimal>(), formula = "1+1" } };

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3", draft), Json);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheSimulationShowsHowTheDashboardIndicatorsMove()
    {
        using var host = RulesHosts.Create();
        var client = await host.CreateMutatingClientAsync();

        var draft = new { analytics = new { values = new Dictionary<string, decimal> { ["currency.legacyBelow"] = 100m } } };

        using var response = await client.PostAsJsonAsync(Api("/api/admin/rules/simulate"), RulesHosts.Simulate("v1.3", draft), Json);
        var result = await ReadAsync<RulesSimulationResponse>(response);

        Assert.True(result.Valid, string.Join(" | ", result.Errors));
        var currency = Assert.Single(result.Impact!.Indicators, i => i.Key == "currency");
        Assert.True(currency.CountAfter >= currency.CountBefore);
        Assert.Equal(0, result.Impact.Up + result.Impact.Down);
    }
}
