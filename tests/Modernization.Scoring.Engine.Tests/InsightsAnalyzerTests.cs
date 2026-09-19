using Modernization.Domain;
using Modernization.Insights;
using Modernization.Roadmap;
using Modernization.WhatIf;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Las seis preguntas del portafolio, respondidas con un portafolio de prueba donde
/// cada respuesta se puede verificar a mano.
/// </summary>
/// <remarks>
/// El portafolio tiene once aplicaciones: tres de Policy Servicing en Chile —la
/// duplicación—, una crítica con flaws y sin recuperación, y varias sanas. Los números
/// que se afirman aquí salen de esos datos, no de una corrida anterior.
/// </remarks>
public sealed class InsightsAnalyzerTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);

    private readonly ScoringEngine _engine = new(RulesetCatalog.Default);
    private readonly RoadmapPlanner _planner;

    public InsightsAnalyzerTests()
    {
        _planner = new RoadmapPlanner(RoadmapCatalog.Default, new WhatIfEngine(_engine), _engine);
    }

    // --------------------------------------------- ¿qué tan sano está?

    [Fact]
    public void HealthCountsTheApplicationsAndSplitsThemByBand()
    {
        var insights = Analyze();

        Assert.Equal(11, insights.Health.Applications);
        Assert.Equal(insights.Health.Applications, insights.Health.Bands.Sum(b => b.Applications));
        Assert.InRange(insights.Health.AverageScore, 20m, 100m);
        Assert.InRange(insights.Health.DataCoverage, 0m, 100m);
        Assert.Equal(100m, insights.Health.Bands.Sum(b => b.Share), 0);
    }

    /// <summary>Una aplicación crítica bajo el umbral es lo primero que hay que mirar.</summary>
    [Fact]
    public void HealthFlagsCriticalApplicationsBelowTheHealthyScore()
    {
        var insights = Analyze();

        Assert.True(insights.Health.CriticalBelowHealthy >= 1);
    }

    // ----------------------------------------- ¿qué modernizar primero?

    [Fact]
    public void PrioritiesAreOrderedByValuePerWeekAndCarryTheirEvidence()
    {
        var insights = Analyze();

        Assert.NotEmpty(insights.Priorities);

        var order = insights.Priorities.Select(p => p.ValuePerWeek).ToList();
        Assert.Equal(order.OrderByDescending(v => v), order);

        foreach (var candidate in insights.Priorities)
        {
            Assert.True(candidate.Uplift > 0m, "solo entra lo que sube el score");
            Assert.True(candidate.Weeks > 0, "el esfuerzo sale de los hitos del roadmap");
            Assert.Equal(candidate.Uplift * candidate.BusinessWeight, candidate.Impact, 1);
            Assert.Contains(candidate.Evidence, e => e.Code == NoticeCodes.InsightsUpliftFromRoadmap);
        }
    }

    /// <summary>Una aplicación crítica y con muchos usuarios pesa más que una igual sin eso.</summary>
    [Fact]
    public void BusinessWeightGrowsWithCriticalityAndUsers()
    {
        var insights = Analyze();

        var critical = insights.Priorities.FirstOrDefault(p => p.EaiCode == "2001");

        Assert.NotNull(critical);
        Assert.True(critical.BusinessWeight >= 2.5m, "crítica (+1) y con base de usuarios (+0,5)");
    }

    /// <summary>Sin roadmap no hay plan, y sin plan no hay esfuerzo que comparar.</summary>
    [Fact]
    public void ApplicationsWithoutARoadmapAreNotRanked()
    {
        var insights = Analyze();

        Assert.DoesNotContain(insights.Priorities, p => p.EaiCode == "5001");
    }

    // --------------------------------------- ¿dónde reducir complejidad?

    [Fact]
    public void ComplexityFindsTheSameCapabilityRepeatedInOneCountry()
    {
        var insights = Analyze();

        var cluster = Assert.Single(
            insights.Complexity,
            c => c.Kind == ComplexityKind.DuplicateCapabilityInCountry && c.Dimension.Contains("Chile", StringComparison.Ordinal));

        Assert.Equal(3, cluster.Applications);
        Assert.Equal(NoticeCodes.InsightsDuplicateCapability, cluster.Evidence.Code);
        Assert.Equal("Policy Servicing", cluster.Evidence.Args["capability"]);
        Assert.Contains("1001", cluster.EaiCodes);
    }

    /// <summary>El umbral se puede subir, y entonces la coincidencia deja de contar.</summary>
    [Fact]
    public void RaisingTheThresholdRemovesSmallerClusters()
    {
        var insights = Analyze(new InsightsOptions(DuplicateThreshold: 4));

        Assert.DoesNotContain(
            insights.Complexity,
            c => c.Kind == ComplexityKind.DuplicateCapabilityInCountry);
    }

    [Fact]
    public void ComplexityListsWhatWasMarkedForRemovalAndIsStillActive()
    {
        var insights = Analyze();

        var backlog = Assert.Single(insights.Complexity, c => c.Kind == ComplexityKind.DecommissionBacklog);

        Assert.Equal(2, backlog.Applications);
        Assert.Equal(NoticeCodes.InsightsDecommissionBacklog, backlog.Evidence.Code);
    }

    // ------------------------------------- ¿dónde se concentra el riesgo?

    [Fact]
    public void RiskConcentratesWhereTheFlawsAndTheExposureAre()
    {
        var insights = Analyze();

        Assert.NotEmpty(insights.Risk);

        var top = insights.Risk[0];
        Assert.True(top.RiskIndex > 0m);
        Assert.Equal(insights.Risk.Select(r => r.RiskIndex).OrderByDescending(r => r), insights.Risk.Select(r => r.RiskIndex));

        var lob = Assert.Single(insights.Risk, r => r.Grouping == "lob" && r.Dimension == "Individual");
        Assert.True(lob.CriticalFlaws >= 4);
        Assert.True(lob.InternetFacingCritical >= 1);
    }

    /// <summary>
    /// Kenna va de 0 a 1000 y más alto es peor. Hasta el 2026-09-18 se contaba al revés
    /// —Kenna menor que 50— y las aplicaciones con el mejor riesgo de host sumaban.
    /// </summary>
    [Fact]
    public void WeakVulnerabilityManagementIsAHighKennaScore()
    {
        var insights = Analyze();

        var claims = Assert.Single(insights.Risk, r => r.Grouping == "capability" && r.Dimension == "Claims");
        Assert.Equal(1, claims.WeakVulnerabilityManagement);

        // Ninguna otra llega a 670.
        Assert.All(
            insights.Risk.Where(r => r.Grouping == "capability" && r.Dimension != "Claims"),
            r => Assert.Equal(0, r.WeakVulnerabilityManagement));
    }

    [Fact]
    public void TheRiskIndexAndTheBusinessWeightComeFromTheDefinitions()
    {
        var rules = new InsightsRules
        {
            RiskIndex = new RiskIndexRules { CriticalFlaw = 0m, HighFlaw = 0m, WeakKenna = 0m, ObsoleteComponent = 0m, ExposedCritical = 1m },
            BusinessWeight = new BusinessWeightRules { Critical = 3m, SignificantUsers = 0m, BroadCapabilities = 0m },
        };

        var insights = Analyze(new InsightsOptions(Rules: rules));

        // Solo pesa la crítica expuesta: cada grupo listado vale lo que cuenta de ellas.
        Assert.NotEmpty(insights.Risk);
        Assert.All(insights.Risk, r => Assert.Equal(r.InternetFacingCritical, r.RiskIndex));

        Assert.Equal(4m, insights.Priorities.Single(p => p.EaiCode == "2001").BusinessWeight);
    }

    // --------------------------------- ¿qué está causando la obsolescencia?

    [Fact]
    public void ObsolescenceRanksTheMetricsThatCostTheMostPoints()
    {
        var insights = Analyze();

        Assert.NotEmpty(insights.Obsolescence);

        var lost = insights.Obsolescence.Select(o => o.LostPoints).ToList();
        Assert.Equal(lost.OrderByDescending(l => l), lost);

        // El portafolio de prueba pierde peso en SCA crítico: varias aplicaciones con flaws.
        Assert.Contains(insights.Obsolescence, o => o.MetricKey == "sca.critical" && o.LostPoints > 0m);

        // Las métricas de peso 0 no explican pérdida de score y no aparecen.
        Assert.DoesNotContain(insights.Obsolescence, o => o.MetricKey == "devsecops");
    }

    /// <summary>La falta de dato no es obsolescencia: va en su propia columna.</summary>
    [Fact]
    public void MissingDataIsCountedApartFromLostPoints()
    {
        var insights = Analyze();

        var techDebt = Assert.Single(insights.Obsolescence, o => o.MetricKey == "techDebt");

        Assert.True(techDebt.WeightWithoutData > 0m);
    }

    // ------------------------------------------ ¿dónde falta resiliencia?

    [Fact]
    public void ResilienceListsWhatIsMissingApplicationByApplication()
    {
        var insights = Analyze();

        Assert.True(insights.Resilience.CriticalWithoutDisasterRecovery >= 1);
        Assert.True(insights.Resilience.CriticalWithoutMonitoring >= 1);

        var worst = insights.Resilience.Applications[0];

        Assert.True(worst.Critical, "las críticas van primero");
        Assert.NotEmpty(worst.Gaps);
        Assert.Contains(worst.Gaps, g => g.Code == NoticeCodes.InsightsGapDisasterRecovery);
    }

    // ------------------------------------------------------------ bordes

    [Fact]
    public void AnEmptyPortfolioAnswersWithoutNumbersInsteadOfFailing()
    {
        var insights = InsightsAnalyzer.Analyze([], [], [], new InsightsOptions());

        Assert.Equal(0, insights.Health.Applications);
        Assert.Empty(insights.Priorities);
        Assert.Empty(insights.Complexity);
        Assert.Empty(insights.Risk);
        Assert.Empty(insights.Obsolescence);
        Assert.Empty(insights.Resilience.Applications);
    }

    [Fact]
    public void TheTopCountIsClampedSoOneQueryCannotAskForEverything()
    {
        var insights = Analyze(new InsightsOptions(TopCount: 5000));

        Assert.Equal(InsightsOptions.MaxTopCount, insights.Options.TopCount);
    }

    // -----------------------------------------------------------------------

    private PortfolioInsights Analyze(InsightsOptions? options = null)
    {
        var applications = Portfolio();
        var scores = applications.Select(a => _engine.Calculate(a)).ToList();
        var plans = applications.Select(a => _planner.Build(a, Start)).ToList();

        return InsightsAnalyzer.Analyze(applications, scores, plans, options ?? new InsightsOptions());
    }

    /// <summary>
    /// Once aplicaciones pensadas para que cada pregunta tenga una respuesta comprobable.
    /// </summary>
    private static List<ApplicationSnapshot> Portfolio()
    {
        var portfolio = new List<ApplicationSnapshot>
        {
            // Tres veces la misma capacidad en Chile: la duplicación.
            App("1001", "Policy Admin Legacy", "Individual", "Chile", "Policy Servicing",
                critical: false, users: 400, kenna: 80, scaCritical: 1, disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.MaintainPatch),
            App("1002", "Policy Admin Regional", "Individual", "Chile", "Policy Servicing",
                critical: false, users: 900, kenna: 200, scaCritical: 0, disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.Refactor),
            App("1003", "Policy Servicing Portal", "Individual", "Chile", "Policy Servicing",
                critical: false, users: 120, kenna: 300, scaCritical: 0, disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.Rehost),

            // Crítica, expuesta, con flaws, Kenna en la banda Critical/High y sin plan de
            // recuperación ni monitoreo.
            App("2001", "Claims Core", "Individual", "Chile", "Claims",
                critical: true, users: 5000, kenna: 900, scaCritical: 3, disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.Reimagine,
                internetFacing: true, disasterRecovery: false, monitoring: false, operational: 45m, rto: null, rpo: null),

            // Sanas, para que el promedio no sea todo rojo.
            App("3001", "Billing", "Retirement", "Mexico", "Billing", critical: false, users: 200, kenna: 320, scaCritical: 0,
                disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.MaintainEvolve, language: "C#"),
            App("3002", "Customer Portal", "Retirement", "Mexico", "Servicing", critical: false, users: 3000, kenna: 300, scaCritical: 0,
                disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.MaintainEvolve, language: "TypeScript"),
            App("3003", "Reporting", "Retirement", "Peru", "Reporting", critical: false, users: 50, kenna: 250, scaCritical: 0,
                disposition: ApplicationDisposition.Tolerate, roadmap: RoadmapType.MaintainPatch, language: "Python"),

            // Marcadas para eliminar y todavía activas: el backlog de decomiso.
            App("4001", "Legacy Quotes", "Individual", "Peru", "Quoting", critical: false, users: 10, kenna: 150, scaCritical: 0,
                disposition: ApplicationDisposition.Eliminate, roadmap: RoadmapType.Retire),
            App("4002", "Old Agent Desktop", "Individual", "Mexico", "Servicing", critical: false, users: 5, kenna: 180, scaCritical: 0,
                disposition: ApplicationDisposition.Eliminate, roadmap: RoadmapType.Consolidate),

            // Sin disposition ni roadmap: no se puede planificar.
            App("5001", "Shadow Tool", "Individual", "Chile", "Quoting", critical: false, users: 15, kenna: 90, scaCritical: 0,
                disposition: null, roadmap: null),

            // Crítica en otro país, sin monitoreo pero con recuperación.
            App("6001", "Payments", "Retirement", "Mexico", "Payments", critical: true, users: 2500, kenna: 260, scaCritical: 0,
                disposition: ApplicationDisposition.Invest, roadmap: RoadmapType.Rehost, monitoring: false),
        };

        return portfolio;
    }

    private static ApplicationSnapshot App(
        string eaiCode,
        string name,
        string lob,
        string country,
        string capability,
        bool critical,
        int users,
        int kenna,
        int scaCritical,
        ApplicationDisposition? disposition,
        RoadmapType? roadmap,
        bool internetFacing = false,
        bool disasterRecovery = true,
        bool monitoring = true,
        decimal operational = 85m,
        string? language = "Java",
        string? rto = "4-24 hrs.",
        string? rpo = "48-72 hrs.") => new()
        {
            EaiCode = eaiCode,
            ApplicationName = name,
            CapturedAt = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
            ApplicationStatus = "Active",
            Lob = lob,
            Country = country,
            PrimaryBusinessCapability = capability,
            L1BusinessCapabilityCount = 2,
            CriticalApplicationIndicator = critical,
            InternetFacing = internetFacing,
            CodingLanguage = language,
            UserCount = users,
            KennaScore = kenna,
            DisasterRecoveryInScope = disasterRecovery,
            ScaFlaws = new ScaFlawCounts { Critical = scaCritical, High = scaCritical * 2, Medium = 0, Low = 0 },
            UtilizesMfa = true,
            OpenSourceScore = 70m,
            RawTechScore = 80m,
            ArchitecturalValue = new ArchitecturalValue { ModernCount = 5, HeritageCount = 2, ObsoleteCount = critical ? 3 : 0 },
            ProductStrategy = new ProductStrategy { InvestCount = 1, TolerateCount = 1 },
            TechDebtScore = null,
            Monitoring = new MonitoringCapabilities { GeneralMonitoring = monitoring },
            OperationalScore = operational,
            Disposition = disposition,
            Roadmap = roadmap,
            RecoveryTimeObjectiveRange = rto,
            RecoveryPointObjectiveRange = rpo,
        };
}
