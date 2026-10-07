using System.Globalization;
using Modernization.Domain;
using Modernization.Roadmap;

namespace Modernization.Insights;

/// <summary>
/// Arma el dashboard ejecutivo con lo que ya hay: el score, el plan del roadmap, los
/// datos de las fuentes y las definiciones de <see cref="DashboardRules"/>.
/// </summary>
/// <remarks>
/// Es una función pura, como <see cref="InsightsAnalyzer"/>: mismo portafolio, mismas
/// definiciones, mismo resultado. Cada nivel sale de un umbral que está en el
/// contrato, no en el código, y lo que no tiene fuente se dice como tal en vez de
/// aproximarse.
/// </remarks>
public static class DashboardAnalyzer
{
    public const string Low = "low";
    public const string Moderate = "moderate";
    public const string High = "high";
    public const string Critical = "critical";

    public const string Measured = "measured";
    public const string Provisional = "provisional";
    public const string NeedsData = "needs-data";

    private static readonly string[] Levels = [Low, Moderate, High, Critical];

    public static PortfolioDashboard Analyze(
        IReadOnlyList<ApplicationSnapshot> applications,
        IReadOnlyList<ScoreBreakdown> scores,
        IReadOnlyList<ApplicationRoadmapPlan> plans,
        DashboardRules rules,
        DashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(options);

        var axis = DashboardOptions.Axes.Contains(options.Axis, StringComparer.Ordinal) ? options.Axis : "country";
        var scoreOf = scores.ToDictionary(s => s.EaiCode, StringComparer.Ordinal);
        var planOf = plans.ToDictionary(p => p.EaiCode, StringComparer.Ordinal);

        var facts = applications
            .Where(a => scoreOf.ContainsKey(a.EaiCode))
            .Select(a => Fact.Of(a, scoreOf[a.EaiCode], planOf.GetValueOrDefault(a.EaiCode), rules, options))
            .OrderBy(f => f.Application.EaiCode, StringComparer.Ordinal)
            .ToList();

        var complexity = Complexity(facts, rules);
        var matrix = Matrix(facts, axis);
        var prioritization = Prioritization(facts, rules);
        var health = Health(facts);
        var resilience = Resilience(facts);

        return new PortfolioDashboard
        {
            Kpis = Kpis(facts, health, prioritization, resilience),
            Matrix = matrix,
            Health = health,
            Prioritization = prioritization,
            Complexity = complexity,
            Risk = Risk(facts, matrix, rules),
            Obsolescence = Obsolescence(facts, rules),
            Resilience = resilience,
            Applications = [.. facts.Select(f => f.ToRow())],
        };
    }

    // ------------------------------------------------------------ indicadores

    private static List<DashboardKpi> Kpis(
        List<Fact> facts,
        DashboardHealth health,
        DashboardPrioritization prioritization,
        DashboardResilience resilience)
    {
        var total = facts.Count;
        var withCurrency = facts.Count(f => f.Currency is not null);
        var legacy = facts.Count(f => f.Legacy);

        return
        [
            new("health", total == 0 ? null : health.AverageScore, total, total, total == 0 ? 0m : 100m,
                total == 0 ? null : ScoreBands.Of(health.AverageScore)),
            new("atRisk", health.InterventionShare, health.RequiringIntervention, total, health.InterventionShare, null),
            new("candidates", Share(prioritization.Candidates, total), prioritization.Candidates, total,
                Share(prioritization.Candidates, total), null),
            new("currency", withCurrency == 0 ? null : Share(legacy, withCurrency), legacy, withCurrency,
                Share(legacy, withCurrency), null),
            new("resilience", resilience.WithData == 0 ? null : resilience.ExposedShare, resilience.Exposed,
                resilience.WithData, resilience.ExposedShare, null),
        ];
    }

    // ----------------------------------------------------- 1. ¿qué tan sano?

    private static DashboardHealth Health(List<Fact> facts)
    {
        if (facts.Count == 0)
        {
            return new DashboardHealth(0, 0m, 0m, 0m, [], 0, 0m);
        }

        var ordered = facts.Select(f => f.Score).OrderBy(s => s).ToList();
        var median = ordered.Count % 2 == 1
            ? ordered[ordered.Count / 2]
            : (ordered[(ordered.Count / 2) - 1] + ordered[ordered.Count / 2]) / 2m;

        var coverage = facts.Average(f =>
        {
            var totalWeight = f.Breakdown.Pillars.Sum(p => p.TotalWeight);
            return totalWeight == 0 ? 0m : f.Breakdown.Pillars.Sum(p => p.AvailableWeight) / totalWeight * 100m;
        });

        var bands = ScoreBands.All
            .Select(band =>
            {
                var count = facts.Count(f => f.Band == band.Code);
                return new BandShare(band.Code, band.Range, count, Share(count, facts.Count));
            })
            .ToList();

        // Requieren intervención: los dos tramos de abajo.
        var intervention = facts.Count(f => f.Band is "critical" or "at-risk");

        return new DashboardHealth(
            facts.Count,
            Round(facts.Average(f => f.Score)),
            Round(median),
            Round(coverage),
            bands,
            intervention,
            Share(intervention, facts.Count));
    }

    /// <summary>
    /// La grilla de CIO contra el eje elegido, con el total de cada fila, de cada columna y
    /// de toda la grilla. Una aplicación sin CIO o sin valor en el eje cae en la fila o
    /// columna vacía, que la interfaz nombra «sin asignar».
    /// </summary>
    private static DashboardHeatmap Matrix(List<Fact> facts, string axis)
    {
        var cells = facts
            .GroupBy(f => (Row: f.Application.Cio ?? string.Empty, Column: f.AxisValue(axis) ?? string.Empty))
            .Select(g =>
            {
                var risks = g.Where(f => f.RiskScore is not null).Select(f => f.RiskScore!.Value).ToList();
                var averageScore = Round(g.Average(f => f.Score));
                decimal? averageRisk = risks.Count == 0 ? null : Round(risks.Average());

                return new DashboardHeatmapCell(
                    g.Key.Row,
                    g.Key.Column,
                    g.Count(),
                    averageScore,
                    ScoreBands.Of(averageScore),
                    averageRisk,
                    averageRisk is { } risk ? g.First().Rules.RiskLevel(risk) : null,
                    Round(risks.Sum()));
            })
            .OrderBy(c => c.Row.Length == 0)
            .ThenBy(c => c.Row, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Column.Length == 0)
            .ThenBy(c => c.Column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = cells.Select(c => c.Row).Distinct(StringComparer.Ordinal).ToList();
        var columns = cells
            .Select(c => c.Column)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c.Length == 0)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DashboardHeatmap(
            axis,
            rows,
            columns,
            cells,
            [.. rows.Select(row => Summarize(row, facts.Where(f => (f.Application.Cio ?? string.Empty) == row)))],
            [.. columns.Select(column => Summarize(column, facts.Where(f => (f.AxisValue(axis) ?? string.Empty) == column)))],
            facts.Count == 0 ? null : Summarize(string.Empty, facts));
    }

    /// <summary>Promedio de score y de riesgo sobre las aplicaciones, igual que una celda.</summary>
    private static DashboardHeatmapTotal Summarize(string key, IEnumerable<Fact> group)
    {
        var members = group.ToList();
        var risks = members.Where(f => f.RiskScore is not null).Select(f => f.RiskScore!.Value).ToList();
        var averageScore = Round(members.Average(f => f.Score));
        decimal? averageRisk = risks.Count == 0 ? null : Round(risks.Average());

        return new DashboardHeatmapTotal(
            key,
            members.Count,
            averageScore,
            ScoreBands.Of(averageScore),
            averageRisk,
            averageRisk is { } risk ? members[0].Rules.RiskLevel(risk) : null);
    }

    // ------------------------------------------------ 2. ¿qué modernizar?

    private static DashboardPrioritization Prioritization(List<Fact> facts, DashboardRules rules)
    {
        var points = facts
            .Where(f => f.Opportunity is not null)
            .Select(f => f.ToPoint())
            .ToList();

        // El orden por defecto: primero lo más crítico y con más oportunidad; dentro de
        // eso, lo más riesgoso. Es una regla discutible a propósito (ADR 0024).
        var top = points
            .OrderByDescending(p => p.Criticality + p.Opportunity)
            .ThenByDescending(p => p.RiskScore ?? -1m)
            .ThenByDescending(p => p.Uplift)
            .ThenBy(p => p.EaiCode, StringComparer.Ordinal)
            .Take(rules.Candidates.TopCount)
            .ToList();

        return new DashboardPrioritization(
            points,
            top,
            points.Count(p => p.Candidate),
            facts.Count - points.Count,
            rules.Candidates.MinCriticality,
            rules.Candidates.MinOpportunity,
            // Sin plan: sin roadmap y sin plan automatico. Lo marcado Eliminate no cuenta aqui: sale
            // del portafolio aunque no tenga roadmap, y por eso no recibe plan automatico.
            facts.Count(f => f.Opportunity is null && f.Application.Roadmap is null
                && f.Application.Disposition != ApplicationDisposition.Eliminate),
            points.Count(p => p.AutomaticTarget is not null),
            facts.Select(f => f.AutomaticTarget).FirstOrDefault(t => t is not null));
    }

    // ---------------------------------------------- 3. ¿dónde hay complejidad?

    private static DashboardComplexity Complexity(List<Fact> facts, DashboardRules rules)
    {
        // Duplicados: la misma capacidad en el mismo país, varias veces.
        var clusters = facts
            .Where(f => !string.IsNullOrWhiteSpace(f.Application.PrimaryBusinessCapability)
                && !string.IsNullOrWhiteSpace(f.Application.Country))
            .GroupBy(f => (f.Application.PrimaryBusinessCapability!, f.Application.Country!))
            .Where(g => g.Count() >= rules.Complexity.DuplicateThreshold)
            .ToList();

        foreach (var fact in clusters.SelectMany(g => g))
        {
            fact.Complexity.Add("duplicateTechnologies");
        }

        // Proliferación: una capacidad escrita en muchos lenguajes.
        foreach (var group in facts
            .Where(f => !string.IsNullOrWhiteSpace(f.Application.PrimaryBusinessCapability)
                && !string.IsNullOrWhiteSpace(f.Application.CodingLanguage))
            .GroupBy(f => f.Application.PrimaryBusinessCapability!)
            .Where(g => g.Select(f => f.Application.CodingLanguage!).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                >= rules.Complexity.LanguageThreshold))
        {
            foreach (var fact in group)
            {
                fact.Complexity.Add("technologyProliferation");
            }
        }

        foreach (var fact in facts)
        {
            if (fact.Legacy)
            {
                fact.Complexity.Add("legacyPlatforms");
            }

            if (fact.HighTechDebt)
            {
                fact.Complexity.Add("technicalDebt");
            }
        }

        string[] order =
        [
            "legacyPlatforms", "duplicateTechnologies", "technicalDebt",
            "sharedDependencies", "technologyProliferation", "otherComplexity",
        ];

        var measured = new HashSet<string>(StringComparer.Ordinal)
        {
            "legacyPlatforms", "duplicateTechnologies", "technicalDebt", "technologyProliferation",
        };

        var counts = order.ToDictionary(
            key => key,
            key => measured.Contains(key) ? facts.Count(f => f.Complexity.Contains(key)) : (int?)null,
            StringComparer.Ordinal);

        var counted = counts.Values.Sum(c => c ?? 0);

        var dimensions = order
            .Select(key => counts[key] is { } count
                ? new DashboardComplexityDimension(key, Provisional, count, Share(count, counted), Share(count, facts.Count))
                : new DashboardComplexityDimension(key, NeedsData, null, null, null))
            .ToList();

        var retire = facts.Count(f =>
            f.Application.Disposition == ApplicationDisposition.Eliminate
            && (f.Application.ApplicationStatus is null
                || f.Application.ApplicationStatus.Contains("Active", StringComparison.OrdinalIgnoreCase)));

        List<DashboardComplexityOpportunity> opportunities =
        [
            new("rationalize", counts["duplicateTechnologies"] ?? 0),
            new("standardize", facts
                .Select(f => f.Application.CodingLanguage)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()),
            new("consolidate", clusters.Count),
            new("retire", retire),
        ];

        return new DashboardComplexity(dimensions, opportunities);
    }

    // ------------------------------------------------ 4. ¿dónde está el riesgo?

    private static DashboardRisk Risk(List<Fact> facts, DashboardHeatmap matrix, DashboardRules rules)
    {
        var risks = facts.Where(f => f.RiskScore is not null).ToList();

        var hotspots = risks
            .OrderByDescending(f => f.RiskScore)
            .ThenBy(f => f.Application.EaiCode, StringComparer.Ordinal)
            .Take(rules.Risk.HotspotCount)
            .Select(f => new DashboardHotspot(
                f.Application.EaiCode,
                f.Application.ApplicationName,
                f.Application.PrimaryBusinessCapability,
                f.RiskScore!.Value,
                f.RiskLevel!))
            .ToList();

        // Índice de concentración: qué parte del riesgo total cargan las celdas con más
        // riesgo. Con riesgo parejo da cerca de la fracción de celdas que se mira.
        var masses = matrix.Cells.Select(c => c.RiskMass).OrderByDescending(m => m).ToList();
        var totalMass = masses.Sum();
        var topCount = masses.Count == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(masses.Count * rules.Concentration.TopShare));
        var index = totalMass == 0m ? 0m : Math.Round(masses.Take(topCount).Sum() / totalMass, 2, MidpointRounding.AwayFromZero);

        var level = index >= rules.Concentration.CriticalFrom ? Critical
            : index >= rules.Concentration.HighFrom ? High
            : index >= rules.Concentration.ModerateFrom ? Moderate
            : Low;

        return new DashboardRisk(
            risks.Count == 0 ? null : Round(risks.Average(f => f.RiskScore!.Value)),
            hotspots,
            index,
            level);
    }

    // ------------------------------------------- 5. ¿qué causa la obsolescencia?

    private static DashboardObsolescence Obsolescence(List<Fact> facts, DashboardRules rules)
    {
        var groups = rules.Obsolescence
            .Select(group =>
            {
                var metrics = new HashSet<string>(group.Metrics, StringComparer.Ordinal);
                var perApplication = facts
                    .Select(f => f.Breakdown.Metrics
                        .Where(m => metrics.Contains(m.MetricKey) && m.Weight > 0m && m.MappedScore is not null)
                        .Sum(m => m.Weight * (100m - m.MappedScore!.Value) / 100m))
                    .ToList();

                return (Group: group, Lost: perApplication.Sum(), Affected: perApplication.Count(l => l > 0m));
            })
            .ToList();

        var totalLost = groups.Sum(g => g.Lost);

        var factors = groups
            .Select(g => new DashboardObsolescenceFactor(
                g.Group.Key,
                Measured,
                g.Group.Currency,
                Round(g.Lost),
                Share(g.Lost, totalLost),
                g.Affected))
            .OrderByDescending(f => f.LostPoints)
            .ToList();

        // Fin de soporte y fin de vida: los pide la propuesta y los tendrá TLM. Hasta
        // entonces se muestran como lo que son, conceptos sin fuente.
        factors.Add(new DashboardObsolescenceFactor("eosExposure", NeedsData, true, 0m, 0m, 0));
        factors.Add(new DashboardObsolescenceFactor("eolExposure", NeedsData, true, 0m, 0m, 0));

        var currencies = facts.Where(f => f.Currency is not null).Select(f => f.Currency!.Value).ToList();

        return new DashboardObsolescence(
            factors,
            Round(factors.Where(f => f.Currency && f.Status == Measured).Sum(f => f.Share)),
            currencies.Count == 0 ? null : Round(currencies.Average()),
            currencies.Count);
    }

    // ------------------------------------------------- 6. ¿dónde falta resiliencia?

    private static DashboardResilience Resilience(List<Fact> facts)
    {
        DashboardResilienceDimension Measure(string key, Func<Fact, string?> level)
        {
            var known = facts.Select(level).Where(l => l is not null).ToList();
            return new DashboardResilienceDimension(key, Provisional, known.Count, Distribution(known!));
        }

        DashboardResilienceDimension Pending(string key) => new(key, NeedsData, 0, []);

        // El orden del mockup: primero lo que no se puede medir todavía queda en su lugar,
        // para que la ausencia de fuente se vea donde se esperaba el dato.
        List<DashboardResilienceDimension> dimensions =
        [
            Pending("availability"),
            Measure("recovery", f => f.Recovery),
            Pending("criticalDependencies"),
            Pending("singlePointsOfFailure"),
            Measure("operationalVulnerabilities", f => f.Operational),
        ];

        var overall = facts.Select(f => f.Resilience).Where(l => l is not null).ToList();
        var exposed = overall.Count(l => l is High or Critical);

        return new DashboardResilience(
            dimensions,
            Distribution(overall!),
            exposed,
            Share(exposed, overall.Count),
            overall.Count);
    }

    private static List<DashboardLevelShare> Distribution(List<string> levels)
        => [.. Levels.Select(level =>
        {
            var count = levels.Count(l => l == level);
            return new DashboardLevelShare(level, count, Share(count, levels.Count));
        })];

    // ------------------------------------------------------------------------

    internal static string Worst(string? first, string? second)
    {
        if (first is null)
        {
            return second!;
        }

        if (second is null)
        {
            return first;
        }

        return Array.IndexOf(Levels, first) >= Array.IndexOf(Levels, second) ? first : second;
    }

    private static decimal Share(int count, int total)
        => total == 0 ? 0m : Round(count / (decimal)total * 100m);

    private static decimal Share(decimal part, decimal total)
        => total == 0m ? 0m : Round(part / total * 100m);

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string RiskLevel(this DashboardRules rules, decimal risk)
        => risk >= rules.Risk.CriticalFrom ? Critical
            : risk >= rules.Risk.HighFrom ? High
            : risk >= rules.Risk.ModerateFrom ? Moderate
            : Low;

    /// <summary>
    /// Risk Score de una aplicación: el pilar de riesgo invertido a una escala de 0 a 100
    /// donde alto es peor. Null sin ese pilar. La historia por carga usa la misma fórmula.
    /// </summary>
    public static decimal? RiskScoreOf(ScoreBreakdown breakdown, DashboardRules rules)
    {
        ArgumentNullException.ThrowIfNull(breakdown);
        ArgumentNullException.ThrowIfNull(rules);

        var pillar = breakdown.Pillars.FirstOrDefault(p => p.Key == rules.Risk.Pillar);

        if (pillar?.Score is not { } score)
        {
            return null;
        }

        var risk = (100m - score) / 80m * 100m;
        return Math.Round(Math.Clamp(risk, 0m, 100m), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Lo que el dashboard sabe de una aplicación, calculado una sola vez y usado por
    /// todos los paneles: así una aplicación nunca cae en un nivel en un panel y en otro
    /// distinto en el de al lado.
    /// </summary>
    private sealed class Fact
    {
        private Fact(ApplicationSnapshot application, ScoreBreakdown breakdown, DashboardRules rules)
        {
            Application = application;
            Breakdown = breakdown;
            Rules = rules;
        }

        public ApplicationSnapshot Application { get; }
        public ScoreBreakdown Breakdown { get; }
        public DashboardRules Rules { get; }

        public decimal Score { get; private init; }
        public string Band { get; private init; } = string.Empty;
        public decimal? RiskScore { get; private init; }
        public string? RiskLevel { get; private init; }
        public decimal? Currency { get; private init; }
        public bool Legacy { get; private init; }
        public bool HighTechDebt { get; private init; }
        public int Criticality { get; private init; }
        public int? Opportunity { get; private init; }
        public decimal Uplift { get; private init; }
        public int Weeks { get; private init; }
        public bool Candidate { get; private init; }

        /// <summary>El objetivo del plan automatico, si la oportunidad sale de uno.</summary>
        public decimal? AutomaticTarget { get; private init; }
        public string? Recovery { get; private init; }
        public string? Operational { get; private init; }
        public string? Resilience { get; private init; }
        public HashSet<string> Complexity { get; } = new(StringComparer.Ordinal);

        public static Fact Of(
            ApplicationSnapshot application,
            ScoreBreakdown breakdown,
            ApplicationRoadmapPlan? plan,
            DashboardRules rules,
            DashboardOptions options)
        {
            var score = Math.Round(breakdown.EffectiveScore, 2, MidpointRounding.AwayFromZero);
            var risk = RiskScoreOf(breakdown, rules);
            var currency = CurrencyOf(application);
            var criticality = CriticalityOf(application, rules, options);

            // Sin plan, o si la aplicación sale del portafolio, no hay oportunidad que medir:
            // su score no sube, deja de existir.
            decimal? uplift = plan is { LeavesPortfolio: false, Projected: not null } ? plan.ProjectedDelta : null;
            int? opportunity = uplift is { } value ? OpportunityOf(value, rules) : null;

            var recovery = RecoveryOf(application, rules);
            var operational = OperationalOf(application, rules);

            return new Fact(application, breakdown, rules)
            {
                Score = score,
                Band = ScoreBands.Of(score),
                RiskScore = risk,
                RiskLevel = risk is { } r ? rules.RiskLevel(r) : null,
                Currency = currency,
                Legacy = currency < rules.Currency.LegacyBelow,
                HighTechDebt = breakdown.Metrics.Any(m =>
                    m.MetricKey == "techDebt" && m.MappedScore is { } mapped && mapped <= rules.Complexity.TechDebtMappedAtMost),
                Criticality = criticality,
                Opportunity = opportunity,
                Uplift = uplift is { } u ? Math.Round(u, 2, MidpointRounding.AwayFromZero) : 0m,
                Weeks = plan?.TotalWeeks ?? 0,
                AutomaticTarget = opportunity is not null ? plan?.AutomaticTarget : null,
                Candidate = opportunity is { } o
                    && criticality >= rules.Candidates.MinCriticality
                    && o >= rules.Candidates.MinOpportunity,
                Recovery = recovery,
                Operational = operational,
                Resilience = recovery is null && operational is null ? null : Worst(recovery, operational),
            };
        }

        public string? AxisValue(string axis) => axis switch
        {
            "region" => Application.Region,
            "capability" => Application.PrimaryBusinessCapability,
            "disposition" => Application.Disposition is { } d ? MapsNames.Display(d) : null,
            _ => Application.Country,
        };

        public DashboardMatrixPoint ToPoint() => new(
            Application.EaiCode,
            Application.ApplicationName,
            Criticality,
            Opportunity ?? 0,
            RiskScore,
            RiskLevel,
            Uplift,
            Weeks,
            Application.Roadmap is { } roadmap ? MapsNames.Display(roadmap) : null,
            Candidate,
            AutomaticTarget);

        public DashboardApplication ToRow() => new(
            Application.EaiCode,
            Application.ApplicationName,
            Application.Region,
            Application.Country,
            Application.Cio,
            Application.Cre,
            Application.PrimaryBusinessCapability,
            Application.ApplicationStatus,
            Application.CriticalApplicationIndicator == true,
            Application.Disposition is { } d ? MapsNames.Display(d) : null,
            Application.Roadmap is { } r ? MapsNames.Display(r) : null,
            Score,
            Band,
            RiskScore,
            RiskLevel,
            Currency,
            Legacy,
            Criticality,
            Opportunity,
            Candidate,
            Resilience,
            Recovery,
            Operational,
            [.. Complexity.Order(StringComparer.Ordinal)]);


        /// <summary>
        /// El Currency Score es el que entrega CMT: el <c>Raw_Tech_Score</c> del Currency
        /// Model Detail Report. Un 0 es falta de dato, como en el modelo de score (ADR 0019).
        /// </summary>
        private static decimal? CurrencyOf(ApplicationSnapshot application)
            => application.RawTechScore is { } score and > 0m
                ? Math.Round(score, 2, MidpointRounding.AwayFromZero)
                : null;

        private static int CriticalityOf(ApplicationSnapshot application, DashboardRules rules, DashboardOptions options)
        {
            var critical = application.CriticalApplicationIndicator == true;
            var broad = application.UserCount >= options.SignificantUserBase
                || application.L1BusinessCapabilityCount >= rules.Criticality.BroadCapabilities;

            return critical && broad ? 3 : critical ? 2 : broad ? 1 : 0;
        }

        private static int OpportunityOf(decimal uplift, DashboardRules rules)
            => uplift >= rules.Opportunity.VeryHighFrom ? 3
                : uplift >= rules.Opportunity.HighFrom ? 2
                : uplift >= rules.Opportunity.MediumFrom ? 1
                : 0;

        /// <summary>
        /// Recuperación: fuera del alcance de DR es un hueco alto, o crítico si la
        /// aplicación lo es. Dentro, decide el piso del rango de RTO.
        /// </summary>
        private static string? RecoveryOf(ApplicationSnapshot application, DashboardRules rules)
        {
            if (application.DisasterRecoveryInScope is not { } inScope)
            {
                return null;
            }

            if (!inScope)
            {
                return application.CriticalApplicationIndicator == true ? Critical : High;
            }

            if (RtoFloorHours(application.RecoveryTimeObjectiveRange) is not { } hours)
            {
                return Moderate;
            }

            return hours >= rules.Resilience.RecoveryHighFromHours ? High
                : hours >= rules.Resilience.RecoveryModerateFromHours ? Moderate
                : Low;
        }

        /// <summary>
        /// Vulnerabilidad operacional: la peor lectura entre el score operacional y la
        /// cobertura de monitoreo. Una capacidad de monitoreo basta, como en el modelo.
        /// </summary>
        private static string? OperationalOf(ApplicationSnapshot application, DashboardRules rules)
        {
            string? byScore = application.OperationalScore switch
            {
                null => null,
                var s when s < rules.Resilience.OperationalCriticalBelow => Critical,
                var s when s < rules.Resilience.OperationalHighBelow => High,
                var s when s < rules.Resilience.OperationalModerateBelow => Moderate,
                _ => Low,
            };

            string? byMonitoring = application.Monitoring?.AnyEnabled switch
            {
                null => null,
                true => Low,
                false => application.CriticalApplicationIndicator == true ? Critical : High,
            };

            return byScore is null && byMonitoring is null ? null : Worst(byScore, byMonitoring);
        }

        /// <summary>El piso de un rango como "24-48 hrs." o "72+ hrs.".</summary>
        private static int? RtoFloorHours(string? range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return null;
            }

            var digits = new string([.. range.TrimStart().TakeWhile(char.IsDigit)]);

            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ? hours : null;
        }
    }
}
