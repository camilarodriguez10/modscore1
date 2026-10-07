using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.ChannelApi.ViewModels;
using Modernization.Domain;

namespace Modernization.Api.Tests;

/// <summary>
/// El plan futuro atravesando las dos capas. Verifica lo que el requisito 6 pide:
/// que le va a pasar a una aplicacion segun su disposition y su roadmap.
/// </summary>
public sealed class RoadmapEndToEndTests(LayeredApiFixture fixture) : IClassFixture<LayeredApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ReferenceEai = "14381";

    [Fact]
    public async Task PlanReturnsTheMapsModelForTheApplication()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<RoadmapPlanView>(
            new Uri($"/api/applications/{ReferenceEai}/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);

        // El EAI de referencia es Tolerate / Maintain (Patch) en el portafolio dummy.
        Assert.Equal("Tolerate", view.Disposition);
        Assert.Equal("Maintain (Patch)", view.Roadmap);
        Assert.NotEmpty(view.DispositionDefinition!);
        Assert.NotEmpty(view.RoadmapDefinition!);
        Assert.NotEmpty(view.LeverRationale!);
        Assert.Equal(80.70m, view.BaselineScore);
    }

    /// <summary>
    /// El score proyectado sale del motor de what-if, no de un numero escrito a
    /// mano: viene acompanado de las palancas concretas que lo producen.
    /// </summary>
    [Fact]
    public async Task PlanProjectsTheFutureScoreWithItsLevers()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<RoadmapPlanView>(
            new Uri($"/api/applications/{ReferenceEai}/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotNull(view.ProjectedScore);
        Assert.True(view.ProjectedDelta > 0);
        Assert.Equal(view.ProjectedScore - view.BaselineScore, view.ProjectedDelta);

        Assert.NotEmpty(view.DeliveredBy);
        Assert.All(view.DeliveredBy, lever => Assert.NotEmpty(lever.Action));
    }

    [Fact]
    public async Task PlanSchedulesMilestonesWithOwnersAndDates()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<RoadmapPlanView>(
            new Uri($"/api/applications/{ReferenceEai}/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotEmpty(view.Milestones);
        Assert.Equal(view.Milestones[^1].End, view.CompletionDate);
        Assert.Equal(view.Milestones.Sum(m => m.Weeks), view.TotalWeeks);

        Assert.All(view.Milestones, milestone =>
        {
            Assert.NotEmpty(milestone.Name);
            Assert.NotEmpty(milestone.Owner);
            Assert.True(milestone.Weeks > 0);
            Assert.True(milestone.SharePercent > 0);
        });

        // Los porcentajes reparten el total.
        Assert.InRange(view.Milestones.Sum(m => m.SharePercent), 99m, 101m);
    }

    /// <summary>La frase de resumen se escribe con los numeros reales del plan.</summary>
    [Fact]
    public async Task PlanExplainsTheOutlookInOneSentence()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<RoadmapPlanView>(
            new Uri($"/api/applications/{ReferenceEai}/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.Equal(NoticeCodes.OutlookProjected, view.Outlook.Code);
        Assert.Equal("Maintain (Patch)", view.Outlook.Args["roadmap"]);
        Assert.Equal("80.7", view.Outlook.Args["baseline"]);
    }

    // ------------------------------------------------------ vista de portafolio

    [Fact]
    public async Task PortfolioGroupsEveryDispositionWithItsRoadmaps()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri("/api/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.TotalApplications > 0);
        Assert.Equal(3, view.Dispositions.Count);

        // Las siete combinaciones de MAPS, repartidas entre las tres dispositions.
        Assert.Equal(7, view.Dispositions.Sum(d => d.Roadmaps.Count));

        Assert.All(view.Dispositions, group =>
        {
            Assert.NotEmpty(group.Definition);
            Assert.All(group.Roadmaps, r => Assert.Equal(group.Disposition, ExpectedDisposition(r.Roadmap)));
        });
    }

    [Fact]
    public async Task PortfolioReportsWhatLeavesAndWhatImproves()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri("/api/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.LeavingPortfolio > 0);

        var leaving = view.Dispositions
            .SelectMany(d => d.Roadmaps)
            .Where(r => r.RemovesFromPortfolio)
            .ToList();

        Assert.Equal(2, leaving.Count);
        Assert.All(leaving, r =>
        {
            Assert.Null(r.AverageProjectedScore);
            Assert.Null(r.AverageDelta);
        });

        // Las que se quedan si proyectan mejora.
        var staying = view.Dispositions
            .SelectMany(d => d.Roadmaps)
            .Where(r => !r.RemovesFromPortfolio && r.Count > 0)
            .ToList();

        Assert.NotEmpty(staying);
        Assert.All(staying, r => Assert.NotNull(r.AverageProjectedScore));
    }

    /// <summary>
    /// Las alertas de coherencia agregadas, con EAI de ejemplo para poder ir a
    /// mirarlos. El portafolio dummy genera contradicciones reales.
    /// </summary>
    [Fact]
    public async Task PortfolioSurfacesCoherenceAlerts()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var view = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri("/api/roadmap", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.NotEmpty(view.Alerts);

        Assert.All(view.Alerts, alert =>
        {
            Assert.NotEmpty(alert.Title);
            Assert.True(alert.Count > 0);
            Assert.NotEmpty(alert.SampleEaiCodes);
        });

        // Ordenadas por gravedad: lo critico primero.
        var order = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Critical"] = 3,
            ["Warning"] = 2,
            ["Info"] = 1,
        };

        Assert.Equal(
            view.Alerts.Select(a => order[a.Severity]).OrderByDescending(v => v),
            view.Alerts.Select(a => order[a.Severity]));
    }

    /// <summary>Un EAI de ejemplo tiene que existir de verdad y tener esa alerta.</summary>
    [Fact]
    public async Task AlertSamplesPointAtRealApplications()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        var portfolio = await client.GetFromJsonAsync<PortfolioRoadmapView>(
            new Uri("/api/roadmap", UriKind.Relative), Json);

        Assert.NotNull(portfolio);
        var alert = portfolio.Alerts[0];
        var sample = alert.SampleEaiCodes[0];

        var plan = await client.GetFromJsonAsync<RoadmapPlanView>(
            new Uri($"/api/applications/{sample}/roadmap", UriKind.Relative), Json);

        Assert.NotNull(plan);
        Assert.Contains(plan.Alerts, a => a.RuleKey == alert.RuleKey);
    }

    // ------------------------------------------------------------- seguridad

    [Theory]
    [InlineData("/api/roadmap")]
    [InlineData("/api/applications/14381/roadmap")]
    public async Task RoadmapEndpointsRejectAnonymousRequests(string path)
    {
        var client = fixture.CreateChannelClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedEaiCodeIsRejected()
    {
        var client = await fixture.CreateAuthenticatedChannelClientAsync();

        using var response = await client.GetAsync(
            new Uri("/api/applications/no-es-un-eai/roadmap", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string ExpectedDisposition(string roadmap) => roadmap switch
    {
        "MaintainPatch" or "Rehost" => "Tolerate",
        "MaintainEvolve" or "Refactor" or "Reimagine" => "Invest",
        "Retire" or "Consolidate" => "Eliminate",
        _ => throw new ArgumentOutOfRangeException(nameof(roadmap), roadmap, "Roadmap fuera de MAPS."),
    };
}
