using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// El What-If Lab atravesando las dos capas. Los POST ejercitan ademas el camino
/// CSRF completo, que hasta ahora solo cubria el logout.
/// </summary>
public sealed class WhatIfEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ReferenceEai = "14381";

    /// <summary>Cliente autenticado con el token CSRF ya cargado, listo para POST.</summary>
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

    // ------------------------------------------------------------- catalogo

    [Fact]
    public async Task LabReturnsTheLeverCatalogOrderedByGain()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<WhatIfLabView>(
            new Uri($"/api/applications/{ReferenceEai}/whatif", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(79.67m, view.BaselineScore);
        Assert.Equal(60m, view.BaselineWeight);

        Assert.NotEmpty(view.Actionable);
        Assert.Equal(
            view.Actionable.Select(l => l.Delta).OrderByDescending(d => d),
            view.Actionable.Select(l => l.Delta));

        // Los quick wins son las tres de mayor retorno.
        Assert.Equal(3, view.QuickWins.Count);
        Assert.Equal(view.Actionable.Take(3).Select(l => l.Key), view.QuickWins.Select(l => l.Key));

        // Cada palanca describe una accion real, no solo un movimiento del numero.
        Assert.All(view.Actionable, l =>
        {
            Assert.NotEmpty(l.Action);
            Assert.True(l.Delta > 0);
        });
    }

    [Fact]
    public async Task LabSeparatesWhatIsAlreadyAchieved()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<WhatIfLabView>(
            new Uri($"/api/applications/{ReferenceEai}/whatif", UriKind.Relative), Json);

        Assert.NotNull(view);

        // El EAI 14381 ya tiene MFA: no puede aparecer como oportunidad.
        Assert.Contains(view.AlreadyAchieved, l => l.Key == "mfa.enable");
        Assert.DoesNotContain(view.Actionable, l => l.Key == "mfa.enable");
        Assert.All(view.AlreadyAchieved, l => Assert.Equal(0m, l.Delta));
    }

    [Fact]
    public async Task LabReportsTheCeilingAndTheHeadroom()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<WhatIfLabView>(
            new Uri($"/api/applications/{ReferenceEai}/whatif", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.CeilingScore > view.BaselineScore);
        Assert.Equal(view.CeilingScore - view.BaselineScore, view.Headroom);
    }

    // ------------------------------------------------------------ escenarios

    /// <summary>
    /// Incluir la aplicacion en el plan de DR aporta dato a una metrica que no lo
    /// tenia: el peso del calculo sube de 60 a 67 y el score con el.
    /// </summary>
    [Fact]
    public async Task ScenarioRecalculatesAndReportsTheNewWeight()
    {
        var client = await MutatingClientAsync();

        // SCA crítico sin dato en la 14381: remediarlo suma los 10 de peso de la métrica.
        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "sca.remediateCritical" } } },
            Json);

        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<ScenarioView>(Json);

        Assert.NotNull(view);
        Assert.Equal(79.67m, view.BaselineScore);
        Assert.Equal(60m, view.BaselineWeight);
        Assert.Equal(70m, view.ProjectedWeight);
        Assert.True(view.Delta > 0);
        Assert.Equal("sca.remediateCritical", Assert.Single(view.Applied).Key);
    }

    /// <summary>
    /// La 14381 no es crítica, y DR solo puntúa en críticas (dashboard v1.3, 2026-10-07):
    /// incluirla en el plan de DR no cambiaría nada, así que el laboratorio no lo ofrece y
    /// dice por qué.
    /// </summary>
    [Fact]
    public async Task TheDisasterRecoveryLeverIsNotOfferedToANonCriticalApplication()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<WhatIfLabView>(
            new Uri($"/api/applications/{ReferenceEai}/whatif", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.DoesNotContain(view.Actionable, l => l.Key == "dr.include");

        var dr = Assert.Single(view.NotActionable, l => l.Key == "dr.include");
        Assert.Equal(NoticeCodes.LeverNotCritical, dr.NotActionableReason?.Code);
    }

    /// <summary>
    /// Cuando el conjunto no equivale a la suma de las partes, la respuesta lo
    /// explica en texto en vez de dejar al usuario sospechando del calculo.
    /// </summary>
    [Fact]
    public async Task ScenarioExplainsTheInteractionBetweenLevers()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new
            {
                levers = new[]
                {
                    new { key = "sca.remediateCritical" },
                    new { key = "techDebt.clear" },
                },
            },
            Json);

        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<ScenarioView>(Json);

        Assert.NotNull(view);
        Assert.NotEqual(0m, view.InteractionEffect);
        Assert.NotNull(view.InteractionNote);
        Assert.StartsWith("scenario.", view.InteractionNote!.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioWithoutCsrfTokenIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "dr.include" } } },
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EmptyScenarioIsRejected()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new { levers = Array.Empty<object>() },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownLeverIsRejected()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "palanca.inventada" } } },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------- objetivo inverso

    [Fact]
    public async Task TargetReturnsAReachablePlan()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/target", UriKind.Relative),
            new { targetScore = 90m },
            Json);

        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<TargetView>(Json);

        Assert.NotNull(view);
        Assert.True(view.IsReachable);
        Assert.NotNull(view.Scenario);
        Assert.True(view.Scenario.ProjectedScore >= 90m);
        Assert.NotEmpty(view.Explanation.Code);
    }

    [Fact]
    public async Task TargetAlreadyMetSaysSo()
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/target", UriKind.Relative),
            new { targetScore = 70m },
            Json);

        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<TargetView>(Json);

        Assert.NotNull(view);
        Assert.True(view.IsAlreadyMet);
        Assert.Null(view.Scenario);
        Assert.Equal(NoticeCodes.TargetAlreadyMet, view.Explanation.Code);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(101)]
    public async Task TargetOutsideTheScaleIsRejectedAtTheEdge(int target)
    {
        var client = await MutatingClientAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/target", UriKind.Relative),
            new { targetScore = target },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------ invariantes

    /// <summary>
    /// Un escenario es una proyeccion: no puede alterar el score real. Si lo
    /// hiciera, simular seria un acto destructivo.
    /// </summary>
    [Fact]
    public async Task SimulatingDoesNotChangeTheRealScore()
    {
        var client = await MutatingClientAsync();

        using var before = await client.GetAsync(
            new Uri($"/api/applications/{ReferenceEai}/overview", UriKind.Relative));
        var beforeView = await before.Content.ReadFromJsonAsync<ScoreOverviewView>(Json);

        using var _ = await client.PostAsJsonAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif/scenario", UriKind.Relative),
            new { levers = new[] { new { key = "sca.remediateCritical" }, new { key = "techDebt.clear" } } },
            Json);

        using var after = await client.GetAsync(
            new Uri($"/api/applications/{ReferenceEai}/overview", UriKind.Relative));
        var afterView = await after.Content.ReadFromJsonAsync<ScoreOverviewView>(Json);

        Assert.NotNull(beforeView);
        Assert.NotNull(afterView);
        Assert.Equal(79.67m, beforeView.Score);
        Assert.Equal(beforeView.Score, afterView.Score);
        Assert.Equal(beforeView.Denominator, afterView.Denominator);
    }

    [Fact]
    public async Task WhatIfEndpointsRejectAnonymousRequests()
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(
            new Uri($"/api/applications/{ReferenceEai}/whatif", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
