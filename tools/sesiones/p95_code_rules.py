from patchlib import patch

patch(r"src\shared\Modernization.Insights\DashboardRules.cs", [
("""    /// <summary>Los parámetros de la pantalla Insights.</summary>
    public InsightsRules Insights { get; init; } = new();
""",
"""    /// <summary>Los parámetros de la pantalla Insights.</summary>
    public InsightsRules Insights { get; init; } = new();

    /// <summary>
    /// Los parámetros de la pantalla Code: obsolescencia y riesgo según lo que declaran los
    /// repositorios (ADR 0026). Una versión de reglas anterior a la pantalla no los trae y
    /// toma los de fábrica.
    /// </summary>
    public CodeRules Code { get; init; } = new();
"""),
("""/// <summary>Las definiciones del dashboard no se pudieron cargar o no son consistentes.</summary>
public sealed class DashboardRulesException : Exception""",
"""/// <summary>
/// La pantalla Code (ADR 0026): cuándo un componente está por llegar a su fin de vida, cuándo
/// una biblioteca está desactualizada o un repositorio abandonado, cuánto suma cada hallazgo
/// al índice de riesgo de la aplicación y desde dónde empieza cada nivel.
/// </summary>
public sealed record CodeRules
{
    public string? Note { get; init; }

    /// <summary>Un fin de vida dentro de estos meses cuenta como próximo.</summary>
    public int ApproachingMonths { get; init; } = 12;

    /// <summary>Versiones mayores por detrás de la última a partir de las cuales una biblioteca está desactualizada.</summary>
    public int OutdatedMajorsBehind { get; init; } = 2;

    /// <summary>Meses sin commits a partir de los cuales un repositorio cuenta como abandonado.</summary>
    public int StaleMonths { get; init; } = 12;

    public CodeRiskPoints Points { get; init; } = new();

    /// <summary>Niveles del índice de riesgo de código: suma de puntos, sin tope.</summary>
    public decimal ModerateFrom { get; init; } = 10m;
    public decimal HighFrom { get; init; } = 25m;
    public decimal CriticalFrom { get; init; } = 50m;

    /// <summary>Cuántas bibliotecas lista el panel de dependencias.</summary>
    public int TopLibraries { get; init; } = 12;

    /// <summary>El nivel de un índice. Una aplicación sin repositorios no tiene nivel.</summary>
    public string LevelOf(decimal index)
        => index >= CriticalFrom ? "critical"
            : index >= HighFrom ? "high"
            : index >= ModerateFrom ? "moderate"
            : "low";
}

/// <summary>Lo que suma cada hallazgo, por cada vez que aparece.</summary>
public sealed record CodeRiskPoints
{
    public decimal EolComponent { get; init; } = 10m;
    public decimal ApproachingComponent { get; init; } = 4m;
    public decimal CriticalVulnerability { get; init; } = 5m;
    public decimal HighVulnerability { get; init; } = 2m;
    public decimal DeprecatedLibrary { get; init; } = 3m;
    public decimal OutdatedLibrary { get; init; } = 1m;
    public decimal StaleRepository { get; init; } = 5m;
}

/// <summary>Las definiciones del dashboard no se pudieron cargar o no son consistentes.</summary>
public sealed class DashboardRulesException : Exception"""),
])

patch(r"src\shared\Modernization.Insights\AnalyticsCatalog.cs", [
("""    public const string InsightsArea = "insights";

    /// <summary>Las áreas, en el orden de las preguntas.</summary>
    public static readonly IReadOnlyList<string> Areas =
        [Currency, Prioritization, ComplexityArea, RiskArea, ObsolescenceArea, ResilienceArea, InsightsArea];
""",
"""    public const string InsightsArea = "insights";
    public const string CodeArea = "code";

    /// <summary>Las áreas, en el orden de las preguntas; la pantalla Code va al final.</summary>
    public static readonly IReadOnlyList<string> Areas =
        [Currency, Prioritization, ComplexityArea, RiskArea, ObsolescenceArea, ResilienceArea, InsightsArea, CodeArea];
"""),
("""        Whole(InsightsArea, "insights.duplicateDefault", r => r.Insights.DuplicateDefault,
            (r, v) => r with { Insights = r.Insights with { DuplicateDefault = v } }, 2, 5),
    ];
""",
"""        Whole(InsightsArea, "insights.duplicateDefault", r => r.Insights.DuplicateDefault,
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
    ];
"""),
("""        new("resilience.operational", r =>
            r.Resilience.OperationalModerateBelow > r.Resilience.OperationalHighBelow
            && r.Resilience.OperationalHighBelow > r.Resilience.OperationalCriticalBelow),
    ];""",
"""        new("resilience.operational", r =>
            r.Resilience.OperationalModerateBelow > r.Resilience.OperationalHighBelow
            && r.Resilience.OperationalHighBelow > r.Resilience.OperationalCriticalBelow),
        new("code.levels", r =>
            r.Code.ModerateFrom < r.Code.HighFrom && r.Code.HighFrom < r.Code.CriticalFrom),
    ];"""),
("""    public const string Hours = "hours";
""",
"""    public const string Hours = "hours";

    /// <summary>Una cantidad de meses.</summary>
    public const string Months = "months";
"""),
])

patch(r"src\shared\Modernization.Insights\Modernization.Insights.csproj", [
("""    <EmbeddedResource Include="..\\..\\..\\contracts\\dashboard-v1.json" LogicalName="dashboard-v1.json" />""",
"""    <EmbeddedResource Include="..\\..\\..\\contracts\\dashboard-v1.json" LogicalName="dashboard-v1.json" />
    <EmbeddedResource Include="..\\..\\..\\contracts\\eol-catalog-v1.json" LogicalName="eol-catalog-v1.json" />"""),
])

patch(r"contracts\dashboard-v1.json", [
("""    "duplicateDefault": 3
  },
""",
"""    "duplicateDefault": 3
  },

  "code": {
    "note": "Code screen (ADR 0026): evidence from the repositories each application owns in GitHub Enterprise. A component is approaching its end of life when its cycle ends within approachingMonths, per the endoflife.date snapshot in contracts/eol-catalog-v1.json. A library is outdated when it is outdatedMajorsBehind or more major versions behind the latest release; a repository is stale after staleMonths without commits, or when it is archived. The code risk index adds the points below for every finding, and its levels follow moderateFrom < highFrom < criticalFrom. It does not change the Modernization Score.",
    "approachingMonths": 12,
    "outdatedMajorsBehind": 2,
    "staleMonths": 12,
    "points": { "eolComponent": 10, "approachingComponent": 4, "criticalVulnerability": 5, "highVulnerability": 2, "deprecatedLibrary": 3, "outdatedLibrary": 1, "staleRepository": 5 },
    "moderateFrom": 10,
    "highFrom": 25,
    "criticalFrom": 50,
    "topLibraries": 12
  },
"""),
("""    "insights": { "status": "provisional", "owner": "Enterprise Architecture" }
  }""",
"""    "insights": { "status": "provisional", "owner": "Enterprise Architecture" },
    "code": { "status": "provisional", "owner": "Enterprise Architecture" }
  }"""),
])
