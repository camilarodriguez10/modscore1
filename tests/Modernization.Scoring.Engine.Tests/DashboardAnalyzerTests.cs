using Modernization.Domain;
using Modernization.Insights;
using Modernization.Roadmap;
using Modernization.WhatIf;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// El dashboard ejecutivo sobre un portafolio de prueba donde cada panel se puede
/// verificar a mano.
/// </summary>
/// <remarks>
/// Once aplicaciones: tres de Policy Servicing en Chile —la duplicación—, una crítica
/// sin recuperación ni monitoreo, una legada por componentes, dos marcadas para
/// eliminar y una sin plan. Lo que se afirma sale de esos datos y de
/// <c>contracts/dashboard-v1.json</c>, no de una corrida anterior.
/// </remarks>
public sealed class DashboardAnalyzerTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);

    private readonly ScoringEngine _engine = new(RulesetCatalog.Default);
    private readonly RoadmapPlanner _planner;

    public DashboardAnalyzerTests()
    {
        _planner = new RoadmapPlanner(RoadmapCatalog.Default, new WhatIfEngine(_engine), _engine);
    }

    // --------------------------------------------------------- definiciones

    [Fact]
    public void TheDefaultDefinitionsLoadAndSayTheyAreProvisional()
    {
        var rules = DashboardRules.Default;

        Assert.Equal("dashboard-v1", rules.Version);
        Assert.Equal("provisional", rules.Status);
        Assert.Equal(60m, rules.Currency.LegacyBelow);
        Assert.Equal("risk", rules.Risk.Pillar);
        Assert.Empty(rules.Problems());
        Assert.All(AnalyticsCatalog.Areas, area => Assert.Equal(AnalyticsArea.Provisional, rules.AreaOf(area).Status));
        Assert.Equal("CMT owner", rules.AreaOf(AnalyticsCatalog.Currency).Owner);
    }

    [Fact]
    public void AgreeingEveryAreaMakesTheDefinitionsAgreed()
    {
        var rules = DashboardRules.Default with
        {
            Areas = AnalyticsCatalog.Areas.ToDictionary(key => key, _ => new AnalyticsArea(AnalyticsArea.Agreed)),
        };

        Assert.Equal(AnalyticsArea.Agreed, rules.Status);
    }

    /// <summary>
    /// Cada parámetro del catálogo lee y escribe el mismo campo: si un Set apuntara a otro
    /// campo, el portal cambiaría un umbral y el cálculo usaría otro.
    /// </summary>
    [Fact]
    public void EveryCatalogParameterReadsWhatItWrites()
    {
        foreach (var parameter in AnalyticsCatalog.Parameters)
        {
            var current = parameter.Get(DashboardRules.Default);
            Assert.True(parameter.Accepts(current), parameter.Key);

            foreach (var value in new[] { parameter.Min, parameter.Max })
            {
                var changed = parameter.Set(DashboardRules.Default, value);
                Assert.Equal(value, parameter.Get(changed));

                // Y solo ese: el resto de los parámetros no se mueve.
                foreach (var other in AnalyticsCatalog.Parameters.Where(p => p.Key != parameter.Key))
                {
                    Assert.Equal(other.Get(DashboardRules.Default), other.Get(changed));
                }
            }
        }

        Assert.Equal(
            AnalyticsCatalog.Parameters.Count,
            AnalyticsCatalog.Parameters.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(AnalyticsCatalog.Parameters, p => Assert.Contains(p.Area, AnalyticsCatalog.Areas));
    }

    [Fact]
    public void TheDocumentListsEveryParameterAndAreaWithItsValue()
    {
        var document = AnalyticsDocument.From(DashboardRules.Default, 1000);

        Assert.Equal(AnalyticsCatalog.Parameters.Select(p => p.Key), document.Parameters.Select(p => p.Key));
        Assert.Equal(AnalyticsCatalog.Areas, document.Areas.Select(a => a.Key));
        Assert.Equal(60m, document.Parameters.Single(p => p.Key == "currency.legacyBelow").Value);
        Assert.Equal(1000, document.SignificantUserBase);
    }

    [Fact]
    public void TheDefinitionsSurviveTheirOwnStorageFormat()
    {
        var changed = DashboardRules.Default with
        {
            Currency = DashboardRules.Default.Currency with { LegacyBelow = 55.5m },
            Areas = new Dictionary<string, AnalyticsArea> { ["risk"] = new(AnalyticsArea.Agreed, "Security") },
        };

        var read = DashboardRules.Parse(changed.ToJson());

        Assert.Equal(55.5m, read.Currency.LegacyBelow);
        Assert.Equal(new AnalyticsArea(AnalyticsArea.Agreed, "Security"), read.AreaOf("risk"));
        Assert.Equal(AnalyticsArea.Provisional, read.AreaOf("currency").Status);
    }

    /// <summary>Un grupo de obsolescencia con una métrica mal escrita no sumaría nada, en silencio.</summary>
    [Fact]
    public void EveryObsolescenceMetricExistsInTheRuleset()
    {
        var known = RulesetCatalog.Default.Metrics.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var metric in DashboardRules.Default.Obsolescence.SelectMany(g => g.Metrics))
        {
            Assert.Contains(metric, known);
        }
    }

    [Fact]
    public void ThresholdsOutOfOrderAreRejectedWhenLoading()
    {
        var json = """
            {
              "version": "broken",
              "currency": { "legacyBelow": 50 },
              "risk": { "pillar": "risk", "moderateFrom": 70, "highFrom": 60, "criticalFrom": 80 },
              "opportunity": { "mediumFrom": 5, "highFrom": 10, "veryHighFrom": 20 },
              "criticality": { "broadCapabilities": 3 },
              "candidates": { "minCriticality": 2, "minOpportunity": 2 },
              "complexity": { "duplicateThreshold": 3, "languageThreshold": 3 },
              "concentration": { "topShare": 0.2, "moderateFrom": 0.4, "highFrom": 0.6, "criticalFrom": 0.8 },
              "resilience": { "recoveryModerateFromHours": 24, "recoveryHighFromHours": 48,
                "operationalModerateBelow": 80, "operationalHighBelow": 60, "operationalCriticalBelow": 40 },
              "obsolescence": [ { "key": "lifecycle", "currency": true, "metrics": [ "productStrategy" ] } ]
            }
            """;

        var error = Assert.Throws<DashboardRulesException>(() => DashboardRules.Parse(json));
        Assert.Contains("risk.levels", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMetricInTwoObsolescenceGroupsIsRejected()
    {
        var json = DashboardJson(obsolescence: """
            [ { "key": "a", "currency": true, "metrics": [ "oss" ] },
              { "key": "b", "currency": false, "metrics": [ "oss" ] } ]
            """);

        Assert.Throws<DashboardRulesException>(() => DashboardRules.Parse(json));
    }

    // ---------------------------------------------------------- indicadores

    [Fact]
    public void TheHeaderCarriesFiveIndicatorsInTheirOrder()
    {
        var dashboard = Analyze();

        Assert.Equal(["health", "atRisk", "candidates", "currency", "resilience"], dashboard.Kpis.Select(k => k.Key));

        var health = dashboard.Kpis[0];
        Assert.Equal(dashboard.Health.AverageScore, health.Value);
        Assert.Equal(11, health.Base);
        Assert.Equal(ScoreBands.Of(dashboard.Health.AverageScore), health.Level);
    }

    /// <summary>Cada porcentaje se lee como «tantas de tantas»: Count de Base.</summary>
    [Fact]
    public void EachIndicatorShareIsItsCountOverItsBase()
    {
        var dashboard = Analyze();

        foreach (var kpi in dashboard.Kpis.Skip(1))
        {
            var expected = kpi.Base == 0 ? 0m : Math.Round(kpi.Count / (decimal)kpi.Base * 100m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(expected, kpi.Share);
        }
    }

    [Fact]
    public void CurrencyExposureCountsTheLegacyApplications()
    {
        var dashboard = Analyze();
        var currency = dashboard.Kpis.Single(k => k.Key == "currency");

        // Solo 7001 tiene un Currency Score de CMT bajo 60.
        Assert.Equal(1, currency.Count);
        Assert.Equal(11, currency.Base);
        Assert.True(dashboard.Applications.Single(a => a.EaiCode == "7001").Legacy);
        Assert.Equal(20m, dashboard.Applications.Single(a => a.EaiCode == "7001").Currency);
    }

    // ------------------------------------------------------ 1 · salud

    [Fact]
    public void TheHeatmapCountsEveryApplicationExactlyOnce()
    {
        var dashboard = Analyze();

        Assert.Equal("country", dashboard.Matrix.Axis);
        Assert.Equal(dashboard.Applications.Count, dashboard.Matrix.Cells.Sum(c => c.Applications));
        Assert.Equal(["Chile", "Mexico", "Peru"], dashboard.Matrix.Columns);

        var policy = dashboard.Matrix.Cells.Single(c => c.Row == "Policy Servicing" && c.Column == "Chile");
        Assert.Equal(3, policy.Applications);
        Assert.Equal(ScoreBands.Of(policy.AverageScore), policy.Band);
    }

    [Theory]
    [InlineData("cio", "CIO North")]
    [InlineData("region", "LATAM")]
    [InlineData("disposition", "Invest")]
    public void TheHeatmapAxisCanBeChosen(string axis, string expectedColumn)
    {
        var dashboard = Analyze(new DashboardOptions(axis));

        Assert.Equal(axis, dashboard.Matrix.Axis);
        Assert.Contains(expectedColumn, dashboard.Matrix.Columns);
        Assert.Equal(dashboard.Applications.Count, dashboard.Matrix.Cells.Sum(c => c.Applications));
    }

    [Fact]
    public void AnUnknownAxisFallsBackToCountry()
        => Assert.Equal("country", Analyze(new DashboardOptions("planet")).Matrix.Axis);

    [Fact]
    public void HealthSplitsThePortfolioByBandAndCountsWhatNeedsIntervention()
    {
        var dashboard = Analyze();

        Assert.Equal(11, dashboard.Health.Applications);
        Assert.Equal(dashboard.Health.Applications, dashboard.Health.Bands.Sum(b => b.Applications));
        Assert.Equal(
            dashboard.Applications.Count(a => a.Band is "critical" or "at-risk"),
            dashboard.Health.RequiringIntervention);
    }

    // ------------------------------------------------------ 2 · prioridad

    /// <summary>Lo que sale del portafolio o no tiene plan no se puede priorizar para modernizar.</summary>
    [Fact]
    public void ApplicationsLeavingThePortfolioOrWithoutAPlanStayOutOfTheMatrix()
    {
        var dashboard = Analyze();
        var inMatrix = dashboard.Prioritization.Points.Select(p => p.EaiCode).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("4001", inMatrix);
        Assert.DoesNotContain("4002", inMatrix);
        Assert.DoesNotContain("5001", inMatrix);
        Assert.Equal(dashboard.Applications.Count - inMatrix.Count, dashboard.Prioritization.OutsideMatrix);
    }

    [Fact]
    public void CriticalityCombinesTheCriticalFlagWithUsersAndCapabilities()
    {
        var rows = Analyze().Applications.ToDictionary(a => a.EaiCode, StringComparer.Ordinal);

        Assert.Equal(3, rows["2001"].Criticality); // crítica y con base de usuarios
        Assert.Equal(1, rows["3002"].Criticality); // base de usuarios, no crítica
        Assert.Equal(0, rows["3003"].Criticality); // ni lo uno ni lo otro
    }

    [Fact]
    public void CandidatesAreTheTopRightQuadrant()
    {
        var dashboard = Analyze();

        Assert.All(dashboard.Prioritization.Points, point =>
            Assert.Equal(point.Criticality >= 2 && point.Opportunity >= 2, point.Candidate));

        Assert.Equal(dashboard.Prioritization.Points.Count(p => p.Candidate), dashboard.Prioritization.Candidates);
        Assert.Equal(dashboard.Prioritization.Candidates, dashboard.Kpis.Single(k => k.Key == "candidates").Count);
    }

    [Fact]
    public void TheTopTenPutsCriticalityAndOpportunityFirstThenRisk()
    {
        var top = Analyze().Prioritization.Top;

        Assert.InRange(top.Count, 1, 10);

        for (var i = 1; i < top.Count; i++)
        {
            var before = top[i - 1];
            var after = top[i];
            var sumBefore = before.Criticality + before.Opportunity;
            var sumAfter = after.Criticality + after.Opportunity;

            Assert.True(sumBefore > sumAfter || (sumBefore == sumAfter && (before.RiskScore ?? -1m) >= (after.RiskScore ?? -1m)));
        }
    }

    // ---------------------------------------------------- 3 · complejidad

    [Fact]
    public void ComplexityMeasuresFourDimensionsAndSaysWhichTwoHaveNoSource()
    {
        var dimensions = Analyze().Complexity.Dimensions.ToDictionary(d => d.Key, StringComparer.Ordinal);

        Assert.Equal(3, dimensions["duplicateTechnologies"].Applications);
        Assert.Equal(1, dimensions["legacyPlatforms"].Applications);

        Assert.Equal(DashboardAnalyzer.NeedsData, dimensions["sharedDependencies"].Status);
        Assert.Null(dimensions["sharedDependencies"].Applications);
        Assert.Equal(DashboardAnalyzer.NeedsData, dimensions["otherComplexity"].Status);

        var measured = dimensions.Values.Where(d => d.Applications is not null).ToList();
        Assert.InRange(measured.Sum(d => d.Share!.Value), 99.9m, 100.1m);
    }

    [Fact]
    public void TheReductionOpportunitiesComeFromTheSameMeasurements()
    {
        var opportunities = Analyze().Complexity.Opportunities.ToDictionary(o => o.Key, o => o.Count, StringComparer.Ordinal);

        Assert.Equal(3, opportunities["rationalize"]);
        Assert.Equal(1, opportunities["consolidate"]);
        Assert.Equal(2, opportunities["retire"]);
        Assert.Equal(4, opportunities["standardize"]); // Java, C#, TypeScript, Python
    }

    // --------------------------------------------------------- 4 · riesgo

    /// <summary>El riesgo es el pilar de Risk Vectors invertido: 100 en el pilar es 0 de riesgo.</summary>
    [Fact]
    public void RiskIsTheRiskVectorsPillarInverted()
    {
        var applications = Portfolio();
        var dashboard = Analyze();

        foreach (var application in applications)
        {
            var pillar = _engine.Calculate(application).Pillars.Single(p => p.Key == "risk").Score!.Value;
            var expected = Math.Round((100m - pillar) / 80m * 100m, 2, MidpointRounding.AwayFromZero);

            Assert.Equal(expected, dashboard.Applications.Single(a => a.EaiCode == application.EaiCode).RiskScore);
        }
    }

    [Fact]
    public void HotspotsAreTheFiveRiskiestApplications()
    {
        var dashboard = Analyze();
        var hotspots = dashboard.Risk.Hotspots;

        Assert.Equal(5, hotspots.Count);
        Assert.Equal(hotspots.Select(h => h.RiskScore).OrderByDescending(r => r), hotspots.Select(h => h.RiskScore));
        Assert.Equal("2001", hotspots[0].EaiCode);
    }

    [Fact]
    public void TheConcentrationIndexIsAShareAndItsLevelMatchesTheThresholds()
    {
        var risk = Analyze().Risk;

        Assert.InRange(risk.ConcentrationIndex, 0m, 1m);

        var expected = risk.ConcentrationIndex >= 0.8m ? "critical"
            : risk.ConcentrationIndex >= 0.6m ? "high"
            : risk.ConcentrationIndex >= 0.4m ? "moderate"
            : "low";

        Assert.Equal(expected, risk.ConcentrationLevel);
    }

    // ---------------------------------------------------- 5 · obsolescencia

    [Fact]
    public void ObsolescenceFactorsShareTheLostPointsAndEosEolWaitForTlm()
    {
        var obsolescence = Analyze().Obsolescence;
        var measured = obsolescence.Factors.Where(f => f.Status == DashboardAnalyzer.Measured).ToList();

        Assert.InRange(measured.Sum(f => f.Share), 99.9m, 100.1m);
        Assert.Equal(measured.OrderByDescending(f => f.LostPoints).Select(f => f.Key), measured.Select(f => f.Key));

        Assert.Contains(obsolescence.Factors, f => f.Key == "eosExposure" && f.Status == DashboardAnalyzer.NeedsData);
        Assert.Contains(obsolescence.Factors, f => f.Key == "eolExposure" && f.Status == DashboardAnalyzer.NeedsData);

        Assert.Equal(
            Math.Round(measured.Where(f => f.Currency).Sum(f => f.Share), 2, MidpointRounding.AwayFromZero),
            obsolescence.CurrencyDrivenShare);
    }

    // ------------------------------------------------------ 6 · resiliencia

    [Fact]
    public void ACriticalApplicationWithoutRecoveryOrMonitoringIsCritical()
    {
        var row = Analyze().Applications.Single(a => a.EaiCode == "2001");

        Assert.Equal("critical", row.Recovery);
        Assert.Equal("critical", row.Operational);
        Assert.Equal("critical", row.Resilience);
    }

    [Fact]
    public void TheRecoveryLevelFollowsTheFloorOfTheRtoRange()
    {
        var rows = Analyze().Applications.ToDictionary(a => a.EaiCode, StringComparer.Ordinal);

        Assert.Equal("low", rows["3001"].Recovery);      // 4-24 hrs.
        Assert.Equal("moderate", rows["3002"].Recovery); // 24-48 hrs.
        Assert.Equal("high", rows["3003"].Recovery);     // 72+ hrs.
    }

    [Fact]
    public void ResilienceMeasuresTwoDimensionsAndWaitsForThreeSources()
    {
        var resilience = Analyze().Resilience;

        Assert.Equal(
            ["availability", "recovery", "criticalDependencies", "singlePointsOfFailure", "operationalVulnerabilities"],
            resilience.Dimensions.Select(d => d.Key));

        foreach (var pending in resilience.Dimensions.Where(d => d.Status == DashboardAnalyzer.NeedsData))
        {
            Assert.Equal(0, pending.WithData);
            Assert.Empty(pending.Levels);
        }

        Assert.Equal(3, resilience.Dimensions.Count(d => d.Status == DashboardAnalyzer.NeedsData));
        Assert.InRange(resilience.Overall.Sum(l => l.Share), 99.9m, 100.1m);
        Assert.Equal(resilience.Overall.Where(l => l.Level is "high" or "critical").Sum(l => l.Applications), resilience.Exposed);
    }

    // ------------------------------------------------------------ detalle

    /// <summary>El detalle dice lo mismo que el panel desde el que se abrió.</summary>
    [Fact]
    public void TheDetailRowsAgreeWithEveryPanel()
    {
        var dashboard = Analyze();
        var rows = dashboard.Applications.ToDictionary(a => a.EaiCode, StringComparer.Ordinal);

        foreach (var hotspot in dashboard.Risk.Hotspots)
        {
            Assert.Equal(hotspot.RiskLevel, rows[hotspot.EaiCode].RiskLevel);
        }

        foreach (var point in dashboard.Prioritization.Points)
        {
            Assert.Equal(point.Candidate, rows[point.EaiCode].Candidate);
            Assert.Equal(point.Opportunity, rows[point.EaiCode].Opportunity);
        }

        Assert.Contains("duplicateTechnologies", rows["1001"].Complexity);
        Assert.Contains("legacyPlatforms", rows["7001"].Complexity);
        Assert.Equal("Tolerate", rows["1001"].Disposition);
        Assert.Equal("Maintain (Patch)", rows["1001"].Roadmap);
    }

    [Fact]
    public void AnEmptyPortfolioAnswersWithZerosInsteadOfFailing()
    {
        var dashboard = DashboardAnalyzer.Analyze([], [], [], DashboardRules.Default, new DashboardOptions());

        Assert.Empty(dashboard.Applications);
        Assert.All(dashboard.Kpis, k => Assert.Equal(0, k.Count));
        Assert.Equal(0m, dashboard.Risk.ConcentrationIndex);
        Assert.Empty(dashboard.Matrix.Cells);
    }

    // ------------------------------------------------ parámetros configurables

    /// <summary>El Currency Score es el de CMT; un 0 es falta de dato, como en el modelo.</summary>
    [Fact]
    public void ACurrencyScoreOfZeroIsNoData()
    {
        var applications = Portfolio();
        applications[0] = applications[0] with { RawTechScore = 0m };

        var dashboard = Analyze(applications: applications);
        var currency = dashboard.Kpis.Single(k => k.Key == "currency");

        Assert.Null(dashboard.Applications.Single(a => a.EaiCode == applications[0].EaiCode).Currency);
        Assert.Equal(10, currency.Base);
    }

    [Fact]
    public void TheLegacyThresholdComesFromTheDefinitions()
    {
        // Con 90, las que tienen 80 también son legadas.
        var rules = DashboardRules.Default with { Currency = DashboardRules.Default.Currency with { LegacyBelow = 90m } };

        var currency = Analyze(rules: rules).Kpis.Single(k => k.Key == "currency");

        Assert.Equal(11, currency.Count);
    }

    [Fact]
    public void TheListsAreAsLongAsTheDefinitionsSay()
    {
        var rules = DashboardRules.Default with
        {
            Risk = DashboardRules.Default.Risk with { HotspotCount = 2 },
            Candidates = DashboardRules.Default.Candidates with { TopCount = 3 },
        };

        var dashboard = Analyze(rules: rules);

        Assert.Equal(2, dashboard.Risk.Hotspots.Count);
        Assert.Equal(3, dashboard.Prioritization.Top.Count);
    }

    [Fact]
    public void HighTechnicalDebtFollowsItsThreshold()
    {
        var applications = Portfolio();
        applications[0] = applications[0] with { TechDebtScore = 1m };

        // 1 cae en la banda 0,1-2,4, que puntúa 60: no es deuda alta con el corte de 20.
        Assert.DoesNotContain("technicalDebt", Analyze(applications: applications).Applications[0].Complexity);

        var rules = DashboardRules.Default with
        {
            Complexity = DashboardRules.Default.Complexity with { TechDebtMappedAtMost = 60m },
        };

        Assert.Contains("technicalDebt", Analyze(rules: rules, applications: applications).Applications[0].Complexity);
    }

    // ------------------------------------------------------------------------

    private PortfolioDashboard Analyze(
        DashboardOptions? options = null,
        DashboardRules? rules = null,
        List<ApplicationSnapshot>? applications = null)
    {
        applications ??= Portfolio();
        var scores = applications.Select(a => _engine.Calculate(a)).ToList();
        var plans = applications.Select(a => _planner.Build(a, Start)).ToList();

        return DashboardAnalyzer.Analyze(
            applications,
            scores,
            plans,
            rules ?? DashboardRules.Default,
            options ?? new DashboardOptions(SignificantUserBase: 1000));
    }

    private static List<ApplicationSnapshot> Portfolio() =>
    [
        // Tres veces la misma capacidad en Chile: la duplicación.
        App("1001", "Policy Admin Legacy", "Chile", "Policy Servicing", critical: false, users: 400, kenna: 80, scaCritical: 1,
            disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.MaintainPatch),
        App("1002", "Policy Admin Regional", "Chile", "Policy Servicing", critical: false, users: 900, kenna: 200, scaCritical: 0,
            disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.Refactor),
        App("1003", "Policy Servicing Portal", "Chile", "Policy Servicing", critical: false, users: 120, kenna: 300, scaCritical: 0,
            disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.Rehost),

        // Crítica, con muchos usuarios, flaws, y sin recuperación ni monitoreo.
        App("2001", "Claims Core", "Chile", "Claims", critical: true, users: 5000, kenna: 900, scaCritical: 3,
            disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.Reimagine,
            disasterRecovery: false, monitoring: false, operational: 45m, rto: null),

        // Sanas, con rangos de RTO distintos.
        App("3001", "Billing", "Mexico", "Billing", critical: false, users: 200, kenna: 320, scaCritical: 0,
            disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.MaintainEvolve, language: "C#"),
        App("3002", "Customer Portal", "Mexico", "Servicing", critical: false, users: 3000, kenna: 300, scaCritical: 0,
            disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.MaintainEvolve, language: "TypeScript", rto: "24-48 hrs."),
        App("3003", "Reporting", "Peru", "Reporting", critical: false, users: 50, kenna: 250, scaCritical: 0,
            disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.MaintainPatch, language: "Python", rto: "72+ hrs."),

        // Marcadas para eliminar y todavía activas.
        App("4001", "Legacy Quotes", "Peru", "Quoting", critical: false, users: 10, kenna: 150, scaCritical: 0,
            disposition: ApplicationDisposition.Eliminate, roadmap: RoadmapType.Retire),
        App("4002", "Old Agent Desktop", "Mexico", "Servicing", critical: false, users: 5, kenna: 180, scaCritical: 0,
            disposition: ApplicationDisposition.Eliminate, roadmap: RoadmapType.Consolidate),

        // Sin disposition ni roadmap: no se puede planificar.
        App("5001", "Shadow Tool", "Chile", "Quoting", critical: false, users: 15, kenna: 90, scaCritical: 0,
            disposition: null, roadmap: null),

        // Legada: CMT le da un Currency Score de 20.
        App("7001", "Mainframe Bridge", "Mexico", "Payments", critical: false, users: 300, kenna: 400, scaCritical: 0,
            disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.Rehost, modern: 1, obsolete: 4, rawTech: 20m),
    ];

    private static ApplicationSnapshot App(
        string eaiCode,
        string name,
        string country,
        string capability,
        bool critical,
        int users,
        int kenna,
        int scaCritical,
        ApplicationDisposition? disposition,
        RoadmapType? roadmap,
        bool disasterRecovery = true,
        bool monitoring = true,
        decimal operational = 85m,
        string? language = "Java",
        string? rto = "4-24 hrs.",
        int modern = 5,
        int obsolete = 0,
        decimal rawTech = 80m) => new()
        {
            EaiCode = eaiCode,
            ApplicationName = name,
            CapturedAt = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
            ApplicationStatus = "Active",
            Region = "LATAM",
            Cio = critical ? "CIO North" : "CIO South",
            Cre = "CRE One",
            Lob = "Individual",
            Country = country,
            PrimaryBusinessCapability = capability,
            L1BusinessCapabilityCount = 2,
            CriticalApplicationIndicator = critical,
            InternetFacing = false,
            CodingLanguage = language,
            UserCount = users,
            KennaScore = kenna,
            DisasterRecoveryInScope = disasterRecovery,
            ScaFlaws = new ScaFlawCounts { Critical = scaCritical, High = scaCritical * 2, Medium = 0, Low = 0 },
            UtilizesMfa = true,
            OpenSourceScore = 70m,
            RawTechScore = rawTech,
            ArchitecturalValue = new ArchitecturalValue { ModernCount = modern, HeritageCount = 0, ObsoleteCount = obsolete },
            ProductStrategy = new ProductStrategy { InvestCount = 1, TolerateCount = 1 },
            TechDebtScore = null,
            Monitoring = new MonitoringCapabilities { GeneralMonitoring = monitoring },
            OperationalScore = operational,
            Disposition = disposition,
            Roadmap = roadmap,
            RecoveryTimeObjectiveRange = rto,
            RecoveryPointObjectiveRange = "1-4 hrs.",
        };

    private static string DashboardJson(string obsolescence) => """
        {
          "version": "test",
          "currency": { "legacyBelow": 50 },
          "risk": { "pillar": "risk", "moderateFrom": 40, "highFrom": 60, "criticalFrom": 80 },
          "opportunity": { "mediumFrom": 5, "highFrom": 10, "veryHighFrom": 20 },
          "criticality": { "broadCapabilities": 3 },
          "candidates": { "minCriticality": 2, "minOpportunity": 2 },
          "complexity": { "duplicateThreshold": 3, "languageThreshold": 3 },
          "concentration": { "topShare": 0.2, "moderateFrom": 0.4, "highFrom": 0.6, "criticalFrom": 0.8 },
          "resilience": { "recoveryModerateFromHours": 24, "recoveryHighFromHours": 48,
            "operationalModerateBelow": 80, "operationalHighBelow": 60, "operationalCriticalBelow": 40 },
          "obsolescence":
        """ + obsolescence + "}";
}
