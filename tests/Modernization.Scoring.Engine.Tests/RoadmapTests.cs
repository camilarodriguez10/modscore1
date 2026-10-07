using Modernization.Domain;
using Modernization.Roadmap;
using Modernization.WhatIf;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Criterio de aceptacion de la Fase 6: las siete combinaciones disposition
/// /roadmap de MAPS modeladas, con su plan y su proyeccion.
/// </summary>
public sealed class RoadmapTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);

    private readonly RoadmapCatalog _catalog = RoadmapCatalog.Default;
    private readonly RoadmapPlanner _planner;
    private readonly PortfolioRoadmapAnalyzer _analyzer;

    public RoadmapTests()
    {
        var engine = new ScoringEngine(RulesetCatalog.Default);
        _planner = new RoadmapPlanner(_catalog, new WhatIfEngine(engine), engine);
        _analyzer = new PortfolioRoadmapAnalyzer(_catalog, _planner);
    }

    // ------------------------------------------------------------- catalogo

    /// <summary>Las siete combinaciones del BRD, con su disposition correcta.</summary>
    [Theory]
    [InlineData(RoadmapType.MaintainPatch, ApplicationDisposition.Tolerate, 60)]
    [InlineData(RoadmapType.Rehost, ApplicationDisposition.Tolerate, 60)]
    [InlineData(RoadmapType.MaintainEvolve, ApplicationDisposition.Invest, 100)]
    [InlineData(RoadmapType.Refactor, ApplicationDisposition.Invest, 20)]
    [InlineData(RoadmapType.Reimagine, ApplicationDisposition.Invest, 20)]
    [InlineData(RoadmapType.Retire, ApplicationDisposition.Eliminate, 20)]
    [InlineData(RoadmapType.Consolidate, ApplicationDisposition.Eliminate, 20)]
    public void MapsPairsMatchTheBrd(RoadmapType roadmap, ApplicationDisposition disposition, int mappedScore)
    {
        var definition = _catalog.Require(roadmap);

        Assert.Equal(disposition, definition.Disposition);
        Assert.Equal(mappedScore, definition.MappedScore);
        Assert.True(_catalog.IsValidPair(disposition, roadmap));
    }

    [Fact]
    public void ParametersAreDataNotConstants()
    {
        Assert.Equal(85m, _catalog.Parameters.HealthyScore);
        Assert.Equal(500, _catalog.Parameters.SignificantUserBase);
    }

    [Fact]
    public void CatalogDeclaresEverySevenRoadmapsWithMilestones()
    {
        Assert.Equal(7, _catalog.Roadmaps.Count);
        Assert.Equal(3, _catalog.Dispositions.Count);

        Assert.All(_catalog.Roadmaps, r =>
        {
            Assert.NotEmpty(r.Definition);
            Assert.NotEmpty(r.Milestones);
            Assert.NotEmpty(r.LeverRationale);
            Assert.All(r.Milestones, m => Assert.True(m.Weeks > 0));
        });
    }

    /// <summary>Toda palanca que un roadmap declara tiene que existir de verdad.</summary>
    [Fact]
    public void EveryDeclaredLeverExistsInTheWhatIfCatalog()
    {
        var known = LeverCatalog.Default.Levers.Select(l => l.Key).ToHashSet(StringComparer.Ordinal);

        var unknown = _catalog.Roadmaps
            .SelectMany(r => r.Levers.Select(key => $"{r.Key} -> {key}"))
            .Where(entry => !known.Contains(entry.Split(" -> ")[1]))
            .ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void CatalogRejectsAnIncompleteModel()
    {
        const string Json = """
        {
          "version": "roto",
          "parameters": { "healthyScore": 85, "significantUserBase": 500 },
          "dispositions": [ { "key": "Tolerate", "name": "Tolerate", "definition": "x" } ],
          "roadmaps": [],
          "coherenceRules": [ { "key": "r", "severity": "Info", "title": "t", "detail": "d" } ]
        }
        """;

        var ex = Assert.Throws<RoadmapException>(() => RoadmapCatalog.FromJson(Json));

        Assert.Contains("7", ex.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------- plan futuro

    /// <summary>
    /// El score proyectado no es un numero escrito a mano: sale del motor de
    /// what-if aplicando las palancas que ese roadmap entrega.
    /// </summary>
    [Fact]
    public void ProjectedScoreComesFromTheWhatIfEngine()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = RoadmapType.Refactor,
        };

        var plan = _planner.Build(snapshot, Start);

        Assert.Equal(80.70m, plan.Baseline.Score);
        Assert.NotNull(plan.Projected);
        Assert.True(plan.ProjectedDelta > 0);

        // Las palancas aplicadas son exactamente las que declara el roadmap.
        Assert.Equal(
            _catalog.Require(RoadmapType.Refactor).Levers.Order(StringComparer.Ordinal),
            plan.Projected.Applied.Select(a => a.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Retire y Consolidate no proyectan mejora: la aplicacion sale del portafolio,
    /// su score no sube — deja de existir. Proyectar una ganancia ahi justificaria
    /// invertir en algo que se va a apagar.
    /// </summary>
    [Theory]
    [InlineData(RoadmapType.Retire)]
    [InlineData(RoadmapType.Consolidate)]
    public void ApplicationsLeavingThePortfolioProjectNoGain(RoadmapType roadmap)
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Eliminate,
            Roadmap = roadmap,
        };

        var plan = _planner.Build(snapshot, Start);

        Assert.True(plan.LeavesPortfolio);
        Assert.Null(plan.Projected);
        Assert.Equal(0m, plan.ProjectedDelta);

        // Pero si tiene plan de ejecucion: dar de baja tambien es un proyecto.
        Assert.NotEmpty(plan.Milestones);
    }

    [Fact]
    public void MilestonesAreChainedFromTheStartDate()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.Rehost,
        };

        var plan = _planner.Build(snapshot, Start);

        Assert.Equal(Start, plan.Milestones[0].Start);
        Assert.Equal(plan.Milestones[^1].End, plan.CompletionDate);
        Assert.Equal(_catalog.Require(RoadmapType.Rehost).TotalWeeks, plan.TotalWeeks);

        // Cada hito arranca donde termina el anterior.
        for (var i = 1; i < plan.Milestones.Count; i++)
        {
            Assert.Equal(plan.Milestones[i - 1].End, plan.Milestones[i].Start);
            Assert.Equal(i + 1, plan.Milestones[i].Order);
        }
    }

    /// <summary>La planificacion es determinista: no lee el reloj.</summary>
    [Fact]
    public void PlanningIsDeterministic()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = RoadmapType.MaintainEvolve,
        };

        var first = _planner.Build(snapshot, Start);
        var second = _planner.Build(snapshot, Start);

        Assert.Equal(first.CompletionDate, second.CompletionDate);
        Assert.Equal(first.ProjectedDelta, second.ProjectedDelta);
    }

    [Fact]
    public void ApplicationWithoutRoadmapHasNoPlan()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = null,
        };

        var plan = _planner.Build(snapshot, Start);

        Assert.Null(plan.RoadmapDefinition);
        Assert.Empty(plan.Milestones);
        Assert.Null(plan.CompletionDate);
        Assert.Contains(plan.Alerts, a => a.Rule.Key == "roadmap.missing");
    }

    // ------------------------------------------------- alertas de coherencia

    /// <summary>Tolerar no es lo mismo que aceptar riesgo critico abierto.</summary>
    [Fact]
    public void TolerateWithOpenCriticalFlawsIsFlagged()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.MaintainPatch,
            ScaFlaws = new ScaFlawCounts { Critical = 4, High = 0 },
        };

        var alert = Assert.Single(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "tolerate.withOpenCriticalFlaws");

        Assert.Equal(CoherenceSeverity.Critical, alert.Rule.Severity);
        Assert.Contains("4", alert.Evidence.Args.Values);
    }

    /// <summary>La disposition dice sacarla; la estrategia de producto, invertir.</summary>
    [Fact]
    public void EliminateWithInvestProductsIsFlagged()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Eliminate,
            Roadmap = RoadmapType.Retire,
        };

        var alert = Assert.Single(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "eliminate.withInvestProducts");

        // El fixture tiene 2 productos en Invest.
        Assert.Contains("2", alert.Evidence.Args.Values);
    }

    [Fact]
    public void RehostDoesNotFixMissingMfa()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.Rehost,
            UtilizesMfa = false,
        };

        Assert.Contains(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "rehost.withoutMfa");
    }

    [Fact]
    public void CriticalApplicationOutsideDisasterRecoveryIsFlagged()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.MaintainPatch,
            CriticalApplicationIndicator = true,
            DisasterRecoveryInScope = false,
        };

        var alert = Assert.Single(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "critical.withoutDisasterRecovery");

        Assert.Equal(CoherenceSeverity.Critical, alert.Rule.Severity);
    }

    /// <summary>Un par que MAPS no define es un problema del dato de origen.</summary>
    [Fact]
    public void MismatchedDispositionAndRoadmapIsFlagged()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Eliminate,
            Roadmap = RoadmapType.Rehost,
        };

        var alert = Assert.Single(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "roadmap.mismatched");

        Assert.Equal(CoherenceSeverity.Critical, alert.Rule.Severity);
    }

    [Fact]
    public void ReimagineOnAHealthyApplicationIsQuestioned()
    {
        // Kenna bajo, sin flaws abiertos y OSS alto: score 96,81, muy por encima
        // del umbral de 85 que el catalogo declara como aplicacion sana.
        var snapshot = GoldenFixtures.Eai10969() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = RoadmapType.Reimagine,
            KennaScore = 100,
            ScaFlaws = new ScaFlawCounts { Critical = 0, High = 0 },
            OpenSourceScore = 90m,
        };

        var alert = Assert.Single(
            _planner.Build(snapshot, Start).Alerts,
            a => a.Rule.Key == "reimagine.onHealthyApplication");

        // Es una pregunta, no un error: la decision puede ser funcional.
        Assert.Equal(CoherenceSeverity.Info, alert.Rule.Severity);
    }

    [Fact]
    public void AlertsComeOrderedBySeverity()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Eliminate,
            Roadmap = RoadmapType.Rehost,
            CriticalApplicationIndicator = true,
            DisasterRecoveryInScope = false,
            UtilizesMfa = false,
        };

        var alerts = _planner.Build(snapshot, Start).Alerts;

        Assert.True(alerts.Count >= 3);
        Assert.Equal(
            alerts.Select(a => a.Rule.Severity).OrderByDescending(s => s),
            alerts.Select(a => a.Rule.Severity));
    }

    /// <summary>Una aplicacion coherente no genera ruido.</summary>
    [Fact]
    public void ACoherentApplicationRaisesNoAlerts()
    {
        var snapshot = GoldenFixtures.Eai14381() with
        {
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.MaintainPatch,
            CriticalApplicationIndicator = false,
            ScaFlaws = new ScaFlawCounts { Critical = 0, High = 0 },
        };

        Assert.Empty(_planner.Build(snapshot, Start).Alerts);
    }

    // ----------------------------------------------------- vista de portafolio

    [Fact]
    public void PortfolioSummaryCoversEveryDispositionAndRoadmap()
    {
        var portfolio = BuildPortfolio();

        var summary = _analyzer.Summarize(portfolio, Start);

        Assert.Equal(portfolio.Count, summary.TotalApplications);
        Assert.Equal(3, summary.Dispositions.Count);
        Assert.Equal(7, summary.Roadmaps.Count);
        Assert.All(summary.Roadmaps, r => Assert.True(r.Count > 0));
    }

    [Fact]
    public void PortfolioSummaryCountsWhatLeavesThePortfolio()
    {
        var summary = _analyzer.Summarize(BuildPortfolio(), Start);

        // Un Retire y un Consolidate en el portafolio de prueba.
        Assert.Equal(2, summary.LeavingPortfolio);

        foreach (var roadmap in summary.Roadmaps.Where(r => r.RemovesFromPortfolio))
        {
            Assert.Null(roadmap.AverageProjectedScore);
        }
    }

    [Fact]
    public void PortfolioSummaryAggregatesAlertsWithSamples()
    {
        var portfolio = BuildPortfolio();
        portfolio.Add(GoldenFixtures.Eai14381() with
        {
            EaiCode = "90001",
            Disposition = ApplicationDisposition.Tolerate,
            Roadmap = RoadmapType.MaintainPatch,
            ScaFlaws = new ScaFlawCounts { Critical = 9 },
        });

        var summary = _analyzer.Summarize(portfolio, Start);
        var alert = summary.Alerts.Single(a => a.RuleKey == "tolerate.withOpenCriticalFlaws");

        Assert.True(alert.Count > 0);
        Assert.Contains("90001", alert.SampleEaiCodes);
        Assert.Equal(
            summary.Alerts.Select(a => a.Severity).OrderByDescending(s => s),
            summary.Alerts.Select(a => a.Severity));
    }

    // ------------------------------------------------ agrupacion (Fase 10)

    /// <summary>
    /// La clave del grupo es el nombre de pantalla, el mismo que emite el inventario:
    /// con el nombre del enum, abrir un grupo "Maintain (Patch)" no filtraba nada.
    /// </summary>
    [Fact]
    public void GroupingByRoadmapUsesTheDisplayNameAndPartitionsThePortfolio()
    {
        var portfolio = BuildPortfolio();

        var summary = _analyzer.Summarize(portfolio, Start, RoadmapGrouping.Roadmap);

        Assert.Equal(RoadmapGrouping.Roadmap, summary.Grouping);
        Assert.Equal(portfolio.Count, summary.Groups.Sum(g => g.Count));
        Assert.Contains(summary.Groups, g => g.Key == "Maintain (Patch)");
        Assert.DoesNotContain(summary.Groups, g => g.Key == "MaintainPatch");
        Assert.All(summary.Groups, g => Assert.All(g.Mix, m => Assert.Equal(g.Key, m.Roadmap)));
    }

    /// <summary>
    /// Agrupar no recalcula: el promedio proyectado de un pais es el promedio de los
    /// planes individuales de sus aplicaciones, y las que salen no entran en el.
    /// </summary>
    [Fact]
    public void GroupingByCountryAveragesTheSameProjectionsAsEachPlan()
    {
        var portfolio = BuildPortfolio()
            .Select((application, index) => application with { Country = index % 2 == 0 ? "Chile" : "Mexico" })
            .ToList();

        var summary = _analyzer.Summarize(portfolio, Start, RoadmapGrouping.Country);
        var chile = summary.Groups.Single(g => g.Key == "Chile");
        var plans = portfolio.Where(a => a.Country == "Chile").Select(a => _planner.Build(a, Start)).ToList();
        var projected = plans.Where(p => p.Projected is not null).ToList();

        Assert.Equal(plans.Count, chile.Count);
        Assert.Equal(plans.Count(p => p.LeavesPortfolio), chile.LeavingPortfolio);
        Assert.Equal(
            Math.Round(projected.Average(p => p.Projected!.Projected.EffectiveScore), 2, MidpointRounding.AwayFromZero),
            chile.AverageProjectedScore);
        Assert.Equal(portfolio.Count, summary.Groups.Sum(g => g.Count));
    }

    [Fact]
    public void ApplicationsWithoutTheAttributeGetTheirOwnGroup()
    {
        var portfolio = BuildPortfolio().Select(a => a with { Cio = "CIO Uno" }).ToList();
        portfolio[0] = portfolio[0] with { Cio = null };

        var summary = _analyzer.Summarize(portfolio, Start, RoadmapGrouping.Cio);

        Assert.Equal(1, summary.Groups.Single(g => g.Key.Length == 0).Count);
        Assert.Equal(portfolio.Count - 1, summary.Groups.Single(g => g.Key == "CIO Uno").Count);
    }

    [Fact]
    public void ThePortfolioGroupingIsOneRowAndTheApplicationGroupingOnePerApplication()
    {
        var portfolio = BuildPortfolio();

        var whole = _analyzer.Summarize(portfolio, Start);
        var perApplication = _analyzer.Summarize(portfolio, Start, RoadmapGrouping.Application);

        Assert.Equal(RoadmapGrouping.Portfolio, whole.Grouping);
        Assert.Equal(portfolio.Count, Assert.Single(whole.Groups).Count);
        Assert.Equal(portfolio.Count, perApplication.Groups.Count);
        Assert.All(perApplication.Groups, g => Assert.Equal(1, g.Count));
    }

    /// <summary>Un portafolio con las siete combinaciones, una por roadmap.</summary>
    private static List<ApplicationSnapshot> BuildPortfolio()
    {
        var catalog = RoadmapCatalog.Default;
        var portfolio = new List<ApplicationSnapshot>();
        var index = 0;

        foreach (var definition in catalog.Roadmaps)
        {
            portfolio.Add(GoldenFixtures.Eai14381() with
            {
                EaiCode = (80000 + index++).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Disposition = definition.Disposition,
                Roadmap = definition.Key,
            });
        }

        return portfolio;
    }
}
