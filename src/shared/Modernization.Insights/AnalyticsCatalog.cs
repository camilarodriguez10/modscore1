namespace Modernization.Insights;

/// <summary>
/// Cada número de <see cref="DashboardRules"/> que se puede cambiar desde el portal, con
/// su área, su tipo y su rango, y las reglas que los relacionan.
/// </summary>
/// <remarks>
/// Es la única lista: la validación al cargar, la del borrador, el documento que ve el
/// administrador y el que explica los cálculos salen de aquí. Un parámetro nuevo se
/// agrega en un solo lugar, y la pantalla de administración lo muestra sin cambios.
/// </remarks>
public static class AnalyticsCatalog
{
    public const string Currency = "currency";
    public const string Prioritization = "prioritization";
    public const string ComplexityArea = "complexity";
    public const string RiskArea = "risk";
    public const string ObsolescenceArea = "obsolescence";
    public const string ResilienceArea = "resilience";
    public const string InsightsArea = "insights";
    public const string CodeArea = "code";

    /// <summary>Las áreas, en el orden de las preguntas; la pantalla Code va al final.</summary>
    public static readonly IReadOnlyList<string> Areas =
        [Currency, Prioritization, ComplexityArea, RiskArea, ObsolescenceArea, ResilienceArea, InsightsArea, CodeArea];

    public static readonly IReadOnlyList<AnalyticsParameter> Parameters =
    [
        // Currency Score
        Score(Currency, "currency.legacyBelow", r => r.Currency.LegacyBelow,
            (r, v) => r with { Currency = r.Currency with { LegacyBelow = v } }, min: 0.01m),

        // ¿Qué modernizar primero?
        Whole(Prioritization, "criticality.broadCapabilities", r => r.Criticality.BroadCapabilities,
            (r, v) => r with { Criticality = r.Criticality with { BroadCapabilities = v } }, 1, 50),
        Points(Prioritization, "opportunity.mediumFrom", r => r.Opportunity.MediumFrom,
            (r, v) => r with { Opportunity = r.Opportunity with { MediumFrom = v } }),
        Points(Prioritization, "opportunity.highFrom", r => r.Opportunity.HighFrom,
            (r, v) => r with { Opportunity = r.Opportunity with { HighFrom = v } }),
        Points(Prioritization, "opportunity.veryHighFrom", r => r.Opportunity.VeryHighFrom,
            (r, v) => r with { Opportunity = r.Opportunity with { VeryHighFrom = v } }),
        Whole(Prioritization, "candidates.minCriticality", r => r.Candidates.MinCriticality,
            (r, v) => r with { Candidates = r.Candidates with { MinCriticality = v } }, 0, 3, AnalyticsKind.Level),
        Whole(Prioritization, "candidates.minOpportunity", r => r.Candidates.MinOpportunity,
            (r, v) => r with { Candidates = r.Candidates with { MinOpportunity = v } }, 0, 3, AnalyticsKind.Level),
        Whole(Prioritization, "candidates.topCount", r => r.Candidates.TopCount,
            (r, v) => r with { Candidates = r.Candidates with { TopCount = v } }, 1, 50),

        // ¿Dónde reducir complejidad?
        Whole(ComplexityArea, "complexity.duplicateThreshold", r => r.Complexity.DuplicateThreshold,
            (r, v) => r with { Complexity = r.Complexity with { DuplicateThreshold = v } }, 2, 50),
        Whole(ComplexityArea, "complexity.languageThreshold", r => r.Complexity.LanguageThreshold,
            (r, v) => r with { Complexity = r.Complexity with { LanguageThreshold = v } }, 2, 50),
        Score(ComplexityArea, "complexity.techDebtMappedAtMost", r => r.Complexity.TechDebtMappedAtMost,
            (r, v) => r with { Complexity = r.Complexity with { TechDebtMappedAtMost = v } }, min: 0m),

        // ¿Dónde se concentra el riesgo?
        Score(RiskArea, "risk.moderateFrom", r => r.Risk.ModerateFrom,
            (r, v) => r with { Risk = r.Risk with { ModerateFrom = v } }, min: 0.01m),
        Score(RiskArea, "risk.highFrom", r => r.Risk.HighFrom,
            (r, v) => r with { Risk = r.Risk with { HighFrom = v } }, min: 0.01m),
        Score(RiskArea, "risk.criticalFrom", r => r.Risk.CriticalFrom,
            (r, v) => r with { Risk = r.Risk with { CriticalFrom = v } }, min: 0.01m),
        Whole(RiskArea, "risk.hotspotCount", r => r.Risk.HotspotCount,
            (r, v) => r with { Risk = r.Risk with { HotspotCount = v } }, 1, 50),
        Share(RiskArea, "concentration.topShare", r => r.Concentration.TopShare,
            (r, v) => r with { Concentration = r.Concentration with { TopShare = v } }),
        Share(RiskArea, "concentration.moderateFrom", r => r.Concentration.ModerateFrom,
            (r, v) => r with { Concentration = r.Concentration with { ModerateFrom = v } }),
        Share(RiskArea, "concentration.highFrom", r => r.Concentration.HighFrom,
            (r, v) => r with { Concentration = r.Concentration with { HighFrom = v } }),
        Share(RiskArea, "concentration.criticalFrom", r => r.Concentration.CriticalFrom,
            (r, v) => r with { Concentration = r.Concentration with { CriticalFrom = v } }),

        // ¿Dónde faltan defensas?
        Whole(ResilienceArea, "resilience.recoveryModerateFromHours", r => r.Resilience.RecoveryModerateFromHours,
            (r, v) => r with { Resilience = r.Resilience with { RecoveryModerateFromHours = v } }, 0, 720, AnalyticsKind.Hours),
        Whole(ResilienceArea, "resilience.recoveryHighFromHours", r => r.Resilience.RecoveryHighFromHours,
            (r, v) => r with { Resilience = r.Resilience with { RecoveryHighFromHours = v } }, 1, 720, AnalyticsKind.Hours),
        Score(ResilienceArea, "resilience.operationalModerateBelow", r => r.Resilience.OperationalModerateBelow,
            (r, v) => r with { Resilience = r.Resilience with { OperationalModerateBelow = v } }, min: 0.01m),
        Score(ResilienceArea, "resilience.operationalHighBelow", r => r.Resilience.OperationalHighBelow,
            (r, v) => r with { Resilience = r.Resilience with { OperationalHighBelow = v } }, min: 0.01m),
        Score(ResilienceArea, "resilience.operationalCriticalBelow", r => r.Resilience.OperationalCriticalBelow,
            (r, v) => r with { Resilience = r.Resilience with { OperationalCriticalBelow = v } }, min: 0.01m),

        // Pantalla Insights
        Weight("insights.businessWeight.critical", r => r.Insights.BusinessWeight.Critical,
            (r, v) => r with { Insights = r.Insights with { BusinessWeight = r.Insights.BusinessWeight with { Critical = v } } }),
        Weight("insights.businessWeight.significantUsers", r => r.Insights.BusinessWeight.SignificantUsers,
            (r, v) => r with { Insights = r.Insights with { BusinessWeight = r.Insights.BusinessWeight with { SignificantUsers = v } } }),
        Weight("insights.businessWeight.broadCapabilities", r => r.Insights.BusinessWeight.BroadCapabilities,
            (r, v) => r with { Insights = r.Insights with { BusinessWeight = r.Insights.BusinessWeight with { BroadCapabilities = v } } }),
        Whole(InsightsArea, "insights.businessWeight.capabilitiesFrom", r => r.Insights.BusinessWeight.CapabilitiesFrom,
            (r, v) => r with { Insights = r.Insights with { BusinessWeight = r.Insights.BusinessWeight with { CapabilitiesFrom = v } } }, 1, 50),
        Points(InsightsArea, "insights.riskIndex.criticalFlaw", r => r.Insights.RiskIndex.CriticalFlaw,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { CriticalFlaw = v } } }, min: 0m),
        Points(InsightsArea, "insights.riskIndex.highFlaw", r => r.Insights.RiskIndex.HighFlaw,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { HighFlaw = v } } }, min: 0m),
        Points(InsightsArea, "insights.riskIndex.weakKenna", r => r.Insights.RiskIndex.WeakKenna,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { WeakKenna = v } } }, min: 0m),
        Whole(InsightsArea, "insights.riskIndex.weakKennaFrom", r => r.Insights.RiskIndex.WeakKennaFrom,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { WeakKennaFrom = v } } }, 1, 1000, AnalyticsKind.Kenna),
        Points(InsightsArea, "insights.riskIndex.obsoleteComponent", r => r.Insights.RiskIndex.ObsoleteComponent,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { ObsoleteComponent = v } } }, min: 0m),
        Points(InsightsArea, "insights.riskIndex.exposedCritical", r => r.Insights.RiskIndex.ExposedCritical,
            (r, v) => r with { Insights = r.Insights with { RiskIndex = r.Insights.RiskIndex with { ExposedCritical = v } } }, min: 0m),
        Score(InsightsArea, "insights.lowOperationalBelow", r => r.Insights.LowOperationalBelow,
            (r, v) => r with { Insights = r.Insights with { LowOperationalBelow = v } }, min: 0.01m),
        Whole(InsightsArea, "insights.duplicateDefault", r => r.Insights.DuplicateDefault,
            (r, v) => r with { Insights = r.Insights with { DuplicateDefault = v } }, 2, 5),

        // Pantalla Code: componentes y bibliotecas de los repositorios
        Whole(CodeArea, "code.approachingMonths", r => r.Code.ApproachingMonths,
            (r, v) => r with { Code = r.Code with { ApproachingMonths = v } }, 1, 60, AnalyticsKind.Months),
        Whole(CodeArea, "code.outdatedMajorsBehind", r => r.Code.OutdatedMajorsBehind,
            (r, v) => r with { Code = r.Code with { OutdatedMajorsBehind = v } }, 1, 20),
        Whole(CodeArea, "code.staleMonths", r => r.Code.StaleMonths,
            (r, v) => r with { Code = r.Code with { StaleMonths = v } }, 1, 60, AnalyticsKind.Months),
        Points(CodeArea, "code.points.eolComponent", r => r.Code.Points.EolComponent,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { EolComponent = v } } }, min: 0m),
        Points(CodeArea, "code.points.approachingComponent", r => r.Code.Points.ApproachingComponent,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { ApproachingComponent = v } } }, min: 0m),
        Points(CodeArea, "code.points.criticalVulnerability", r => r.Code.Points.CriticalVulnerability,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { CriticalVulnerability = v } } }, min: 0m),
        Points(CodeArea, "code.points.highVulnerability", r => r.Code.Points.HighVulnerability,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { HighVulnerability = v } } }, min: 0m),
        Points(CodeArea, "code.points.deprecatedLibrary", r => r.Code.Points.DeprecatedLibrary,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { DeprecatedLibrary = v } } }, min: 0m),
        Points(CodeArea, "code.points.outdatedLibrary", r => r.Code.Points.OutdatedLibrary,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { OutdatedLibrary = v } } }, min: 0m),
        Points(CodeArea, "code.points.staleRepository", r => r.Code.Points.StaleRepository,
            (r, v) => r with { Code = r.Code with { Points = r.Code.Points with { StaleRepository = v } } }, min: 0m),
        Points(CodeArea, "code.moderateFrom", r => r.Code.ModerateFrom,
            (r, v) => r with { Code = r.Code with { ModerateFrom = v } }),
        Points(CodeArea, "code.highFrom", r => r.Code.HighFrom,
            (r, v) => r with { Code = r.Code with { HighFrom = v } }),
        Points(CodeArea, "code.criticalFrom", r => r.Code.CriticalFrom,
            (r, v) => r with { Code = r.Code with { CriticalFrom = v } }),
        Whole(CodeArea, "code.topLibraries", r => r.Code.TopLibraries,
            (r, v) => r with { Code = r.Code with { TopLibraries = v } }, 1, 50),
        Whole(CodeArea, "code.topActions", r => r.Code.TopActions,
            (r, v) => r with { Code = r.Code with { TopActions = v } }, 1, 50),
    ];

    /// <summary>
    /// Reglas entre parámetros. Cada una por su clave, que la interfaz nombra: un nivel
    /// alto por debajo del moderado no falla en ningún cálculo, clasifica mal.
    /// </summary>
    public static readonly IReadOnlyList<AnalyticsConstraint> Constraints =
    [
        new("opportunity.levels", r =>
            r.Opportunity.MediumFrom < r.Opportunity.HighFrom && r.Opportunity.HighFrom < r.Opportunity.VeryHighFrom),
        new("risk.levels", r =>
            r.Risk.ModerateFrom < r.Risk.HighFrom && r.Risk.HighFrom < r.Risk.CriticalFrom),
        new("concentration.levels", r =>
            r.Concentration.ModerateFrom < r.Concentration.HighFrom && r.Concentration.HighFrom < r.Concentration.CriticalFrom),
        new("resilience.recovery", r =>
            r.Resilience.RecoveryModerateFromHours < r.Resilience.RecoveryHighFromHours),
        new("resilience.operational", r =>
            r.Resilience.OperationalModerateBelow > r.Resilience.OperationalHighBelow
            && r.Resilience.OperationalHighBelow > r.Resilience.OperationalCriticalBelow),
        new("code.levels", r =>
            r.Code.ModerateFrom < r.Code.HighFrom && r.Code.HighFrom < r.Code.CriticalFrom),
    ];

    public static AnalyticsParameter? Find(string? key)
        => key is null ? null : Parameters.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));

    private static AnalyticsParameter Score(
        string area, string key, Func<DashboardRules, decimal> get, Func<DashboardRules, decimal, DashboardRules> set, decimal min)
        => new(key, area, AnalyticsKind.Score, min, 100m, 2, get, set);

    private static AnalyticsParameter Share(
        string area, string key, Func<DashboardRules, decimal> get, Func<DashboardRules, decimal, DashboardRules> set)
        => new(key, area, AnalyticsKind.Share, 0.01m, 1m, 2, get, set);

    private static AnalyticsParameter Points(
        string area, string key, Func<DashboardRules, decimal> get, Func<DashboardRules, decimal, DashboardRules> set, decimal min = 0.01m)
        => new(key, area, AnalyticsKind.Points, min, 100m, 2, get, set);

    private static AnalyticsParameter Weight(
        string key, Func<DashboardRules, decimal> get, Func<DashboardRules, decimal, DashboardRules> set)
        => new(key, InsightsArea, AnalyticsKind.Weight, 0m, 10m, 2, get, set);

    private static AnalyticsParameter Whole(
        string area,
        string key,
        Func<DashboardRules, int> get,
        Func<DashboardRules, int, DashboardRules> set,
        int min,
        int max,
        string kind = AnalyticsKind.Count)
        => new(key, area, kind, min, max, 0, r => get(r), (r, v) => set(r, (int)v));
}

/// <summary>Qué mide un parámetro. La interfaz lo usa para la unidad y el paso.</summary>
public static class AnalyticsKind
{
    /// <summary>Un puntaje de 0 a 100.</summary>
    public const string Score = "score";

    /// <summary>Una fracción de 0 a 1.</summary>
    public const string Share = "share";

    /// <summary>Puntos de score o de índice.</summary>
    public const string Points = "points";

    /// <summary>Un peso que multiplica.</summary>
    public const string Weight = "weight";

    /// <summary>Un nivel de 0 (bajo) a 3 (muy alto).</summary>
    public const string Level = "level";

    /// <summary>Una cantidad entera: aplicaciones, capacidades, lenguajes.</summary>
    public const string Count = "count";

    public const string Hours = "hours";

    /// <summary>Una cantidad de meses.</summary>
    public const string Months = "months";

    /// <summary>Kenna, de 0 a 1000.</summary>
    public const string Kenna = "kenna";
}

/// <summary>Un número editable de las definiciones.</summary>
public sealed record AnalyticsParameter(
    string Key,
    string Area,
    string Kind,
    decimal Min,
    decimal Max,
    int Decimals,
    Func<DashboardRules, decimal> Get,
    Func<DashboardRules, decimal, DashboardRules> Set)
{
    /// <summary>Dentro del rango y con los decimales que admite.</summary>
    public bool Accepts(decimal value)
        => value >= Min && value <= Max && decimal.Round(value, Decimals) == value;
}

/// <summary>Una regla entre parámetros, con la clave que la nombra.</summary>
public sealed record AnalyticsConstraint(string Key, Func<DashboardRules, bool> Holds);

/// <summary>
/// Las definiciones vigentes como se muestran: cada parámetro con su valor y su rango, y
/// cada área con su estado y su dueño.
/// </summary>
/// <remarks>
/// Lo ve el administrador para editar y lo reciben el dashboard e Insights para explicar
/// sus cálculos con los números que de verdad usaron. <c>SignificantUserBase</c> no se
/// edita aquí —es un parámetro del roadmap, en la misma versión de reglas—, pero la
/// criticidad y el peso de negocio dependen de él y la explicación lo nombra.
/// </remarks>
public sealed record AnalyticsDocument(
    IReadOnlyList<AnalyticsParameterValue> Parameters,
    IReadOnlyList<AnalyticsAreaValue> Areas,
    int SignificantUserBase)
{
    public static AnalyticsDocument From(DashboardRules rules, int significantUserBase)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new AnalyticsDocument(
            [.. AnalyticsCatalog.Parameters.Select(p => new AnalyticsParameterValue(
                p.Key, p.Area, p.Kind, p.Get(rules), p.Min, p.Max, p.Decimals))],
            [.. AnalyticsCatalog.Areas.Select(key =>
            {
                var area = rules.AreaOf(key);
                return new AnalyticsAreaValue(key, area.Status, area.Owner);
            })],
            significantUserBase);
    }
}

public sealed record AnalyticsParameterValue(
    string Key,
    string Area,
    string Kind,
    decimal Value,
    decimal Min,
    decimal Max,
    int Decimals);

public sealed record AnalyticsAreaValue(string Key, string Status, string? Owner);
