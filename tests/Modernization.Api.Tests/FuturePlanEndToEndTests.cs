using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// El pedido del 2026-10-21, por las dos capas: el future plan de cada aplicación llega
/// calculado y paso por paso, y coincide exactamente con lo que da el What-If Lab al mismo
/// objetivo y con el escenario armado con sus palancas.
/// </summary>
public sealed class FuturePlanEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] Owners = ["security", "infrastructure", "development", "architecture", "operations"];

    private async Task<HttpClient> MutatingClientAsync() => await fixture.CreateMutatingClientAsync();

    /// <summary>Unas cuantas aplicaciones del portafolio de ejemplo, con y sin roadmap de MAPS.</summary>
    private static async Task<IReadOnlyList<string>> SomeApplicationsAsync(HttpClient client)
    {
        var portfolio = await client.GetFromJsonAsync<PortfolioView>(new Uri("/api/portfolio?pageSize=40", UriKind.Relative), Json);
        return [.. portfolio!.Applications.Select(a => a.EaiCode).Take(12)];
    }

    [Fact]
    public async Task EveryStepSaysWhatToDoWhoDoesItAndHowManyPointsItAdds()
    {
        var client = await MutatingClientAsync();
        var withSteps = 0;

        foreach (var eai in await SomeApplicationsAsync(client))
        {
            var view = await client.GetFromJsonAsync<RoadmapPlanView>(new Uri($"/api/applications/{eai}/roadmap", UriKind.Relative), Json);

            if (view!.ActionPlan is not { } plan)
            {
                // Sin plan solo lo que se va a eliminar.
                Assert.Equal("Eliminate", view.Disposition);
                continue;
            }

            Assert.Equal(90m, plan.Target);
            Assert.Equal(view.BaselineScore, plan.BaselineScore);

            var score = plan.BaselineScore;

            foreach (var step in plan.Steps)
            {
                withSteps++;
                Assert.NotEmpty(step.Actions);
                Assert.NotEmpty(step.MetricName);
                Assert.Contains(step.Owner, Owners);
                Assert.True(step.Points > 0, $"{eai} {step.LeverKey} suma {step.Points}.");
                Assert.Equal(Math.Round(step.ScoreAfter - score, 2, MidpointRounding.AwayFromZero), step.Points);
                score = step.ScoreAfter;
            }

            if (plan.Steps.Count > 0)
            {
                Assert.Equal(plan.ProjectedScore, plan.Steps[^1].ScoreAfter);
                Assert.Equal(plan.IsReachable, plan.ProjectedScore >= plan.Target);
            }
        }

        Assert.True(withSteps > 0, "Ninguna aplicación de la muestra tenía pasos.");
    }

    [Fact]
    public async Task TheFuturePlanIsExactlyWhatTheWhatIfLabFindsForNinety()
    {
        var client = await MutatingClientAsync();

        foreach (var eai in await SomeApplicationsAsync(client))
        {
            var view = await client.GetFromJsonAsync<RoadmapPlanView>(new Uri($"/api/applications/{eai}/roadmap", UriKind.Relative), Json);

            if (view!.ActionPlan is not { } plan)
            {
                continue;
            }

            using var response = await client.PostAsJsonAsync(
                new Uri($"/api/applications/{eai}/whatif/target", UriKind.Relative), new { targetScore = plan.Target }, Json);
            response.EnsureSuccessStatusCode();
            var target = await response.Content.ReadFromJsonAsync<TargetView>(Json);

            Assert.Equal(plan.IsReachable, target!.IsReachable);
            Assert.Equal(plan.IsAlreadyMet, target.IsAlreadyMet);

            if (plan.IsAlreadyMet)
            {
                Assert.Empty(plan.Steps);
                continue;
            }

            Assert.Equal(
                plan.Steps.Select(s => (s.LeverKey, s.Parameter)),
                target.Scenario!.Applied.Select(a => (a.Key, a.Parameter)));
            Assert.Equal(plan.ProjectedScore, target.Scenario.ProjectedScore);

            // «Open in What-If»: el escenario con las mismas palancas y cantidades da lo mismo.
            using var scenarioResponse = await client.PostAsJsonAsync(
                new Uri($"/api/applications/{eai}/whatif/scenario", UriKind.Relative),
                new { levers = plan.Steps.Select(s => new { key = s.LeverKey, parameter = s.Parameter }) },
                Json);
            scenarioResponse.EnsureSuccessStatusCode();
            var scenario = await scenarioResponse.Content.ReadFromJsonAsync<ScenarioView>(Json);

            Assert.Equal(plan.ProjectedScore, scenario!.ProjectedScore);
        }
    }

    /// <summary>El laboratorio parte del objetivo de los planes automáticos.</summary>
    [Fact]
    public async Task TheLabStartsFromTheAutomaticTarget()
    {
        var client = await MutatingClientAsync();

        var lab = await client.GetFromJsonAsync<WhatIfLabView>(new Uri("/api/applications/14381/whatif", UriKind.Relative), Json);

        Assert.Equal(90m, lab!.AutomaticTarget);
    }

    [Fact]
    public async Task ActionsCarryTheEvidenceOfTheApplication()
    {
        var client = await MutatingClientAsync();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var eai in await SomeApplicationsAsync(client))
        {
            var view = await client.GetFromJsonAsync<RoadmapPlanView>(new Uri($"/api/applications/{eai}/roadmap", UriKind.Relative), Json);

            foreach (var action in view!.ActionPlan?.Steps.SelectMany(s => s.Actions) ?? [])
            {
                codes.Add(action.Code);
                Assert.StartsWith("plan.", action.Code, StringComparison.Ordinal);
            }
        }

        Assert.NotEmpty(codes);
        Assert.DoesNotContain(NoticeCodes.PlanGeneric, codes);
    }
}
