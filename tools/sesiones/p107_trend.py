from patchlib import patch

patch(r"src\shared\Modernization.Insights\DashboardAnalyzer.cs", [
("""            var risk = RiskOf(breakdown, rules);""",
 """            var risk = RiskScoreOf(breakdown, rules);"""),
("""        /// <summary>El pilar de riesgo, invertido a una escala de 0 a 100 donde alto es peor.</summary>
        private static decimal? RiskOf(ScoreBreakdown breakdown, DashboardRules rules)
        {""",
"""        /// <summary>El pilar de riesgo, invertido a una escala de 0 a 100 donde alto es peor.</summary>
        public static decimal? RiskScoreOf(ScoreBreakdown breakdown, DashboardRules rules)
        {
            ArgumentNullException.ThrowIfNull(breakdown);
            ArgumentNullException.ThrowIfNull(rules);
"""),
])

patch(r"src\shared\Modernization.Domain\Sources\SnapshotVersion.cs", [
("""    /// <summary>Peso disponible en esa version: explica un salto sin merito real.</summary>
    public required decimal? AvailableWeight { get; init; }
}""",
"""    /// <summary>Peso disponible en esa version: explica un salto sin merito real.</summary>
    public required decimal? AvailableWeight { get; init; }

    /// <summary>Currency Score de CMT en esa version. Null sin dato.</summary>
    public decimal? Currency { get; init; }

    /// <summary>Risk Score (0-100, mas alto es peor) en esa version. Null sin pilar de riesgo.</summary>
    public decimal? RiskScore { get; init; }
}"""),
])

patch(r"src\system\Modernization.SystemApi\Contracts\DashboardContracts.cs", [
("""/// <summary>
/// La salud del portafolio filtrado en una carga. <c>HealthScore</c> y
/// <c>AtRiskShare</c> son null cuando ninguna aplicacion de esa carga pasa el filtro.
/// </summary>
public sealed record DashboardTrendPoint(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    int Applications,
    decimal? HealthScore,
    decimal? AtRiskShare);""",
"""/// <summary>
/// La salud del portafolio filtrado en una carga. <c>HealthScore</c> y
/// <c>AtRiskShare</c> son null cuando ninguna aplicacion de esa carga pasa el filtro;
/// <c>Currency</c> (Currency Score promedio de CMT) y <c>RiskScore</c> (promedio), cuando
/// ninguna tiene el dato.
/// </summary>
public sealed record DashboardTrendPoint(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    int Applications,
    decimal? HealthScore,
    decimal? AtRiskShare,
    decimal? Currency = null,
    decimal? RiskScore = null);"""),
])

patch(r"src\system\Modernization.SystemApi\Services\PortfolioService.cs", [
# history per app
("""                Score = visible ? match!.Score : null,
                AvailableWeight = visible ? match!.Weight : null,
            });
        }

        return points;
    }

    /// <summary>Score promedio del portafolio dentro del alcance, en cada carga.</summary>""",
"""                Score = visible ? match!.Score : null,
                AvailableWeight = visible ? match!.Weight : null,
                Currency = visible ? match!.Currency : null,
                RiskScore = visible ? match!.Risk : null,
            });
        }

        return points;
    }

    /// <summary>Score promedio del portafolio dentro del alcance, en cada carga.</summary>"""),
# trend
("""            var atRisk = visible.Count(p => ScoreBands.Of(p.Score!.Value) is "critical" or "at-risk");

            points.Add(new DashboardTrendPoint(
                version.Id,
                version.Label,
                version.CapturedAt,
                visible.Count,
                visible.Count == 0
                    ? null
                    : Math.Round(visible.Average(p => p.Score!.Value), 2, MidpointRounding.AwayFromZero),
                visible.Count == 0
                    ? null
                    : Math.Round(atRisk / (decimal)visible.Count * 100m, 2, MidpointRounding.AwayFromZero)));""",
"""            var atRisk = visible.Count(p => ScoreBands.Of(p.Score!.Value) is "critical" or "at-risk");

            points.Add(new DashboardTrendPoint(
                version.Id,
                version.Label,
                version.CapturedAt,
                visible.Count,
                visible.Count == 0
                    ? null
                    : Math.Round(visible.Average(p => p.Score!.Value), 2, MidpointRounding.AwayFromZero),
                visible.Count == 0
                    ? null
                    : Math.Round(atRisk / (decimal)visible.Count * 100m, 2, MidpointRounding.AwayFromZero),
                Average(visible.Select(p => p.Currency)),
                Average(visible.Select(p => p.Risk))));"""),
("""    // -----------------------------------------------------------------------

    private static bool Allows(PortfolioScope scope, ApplicationSnapshot application)""",
"""    // -----------------------------------------------------------------------

    /// <summary>El promedio de los valores con dato, o null si ninguno lo tiene.</summary>
    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();

        return present.Count == 0 ? null : Math.Round(present.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static bool Allows(PortfolioScope scope, ApplicationSnapshot application)"""),
# compute points
("""        foreach (var application in snapshot.Applications)
        {
            decimal? score = null;
            decimal? weight = null;

            try
            {
                var breakdown = model.Engine.Calculate(application);
                score = breakdown.EffectiveScore;
                weight = breakdown.Denominator;
            }""",
"""        foreach (var application in snapshot.Applications)
        {
            decimal? score = null;
            decimal? weight = null;
            decimal? risk = null;

            try
            {
                var breakdown = model.Engine.Calculate(application);
                score = breakdown.EffectiveScore;
                weight = breakdown.Denominator;
                risk = Insights.DashboardAnalyzer.RiskScoreOf(breakdown, model.Analytics);
            }"""),
("""            points.Add(new VersionPoint(
                application.EaiCode,
                application.Lob,
                application.Country,
                application.Cio,
                score,
                weight,
                AttributesOf(application)));""",
"""            points.Add(new VersionPoint(
                application.EaiCode,
                application.Lob,
                application.Country,
                application.Cio,
                score,
                weight,
                // El Currency Score de CMT: un 0 es falta de dato, como en el score.
                application.RawTechScore is { } currency and > 0m
                    ? Math.Round(currency, 2, MidpointRounding.AwayFromZero)
                    : null,
                risk,
                AttributesOf(application)));"""),
("""    private sealed record VersionPoint(
        string EaiCode,
        string? Lob,
        string? Country,
        string? Cio,
        decimal? Score,
        decimal? Weight,
        ApplicationSnapshot Attributes);""",
"""    private sealed record VersionPoint(
        string EaiCode,
        string? Lob,
        string? Country,
        string? Cio,
        decimal? Score,
        decimal? Weight,
        decimal? Currency,
        decimal? Risk,
        ApplicationSnapshot Attributes);"""),
])

patch(r"src\system\Modernization.SystemApi\Contracts\CodeContracts.cs", [
("""public sealed record CodeResponse(
    string RulesetVersion,
    CodeSourceResponse Source,
    CodePortfolio Analysis,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación con sus repositorios, componentes y dependencias.</summary>
public sealed record CodeApplicationResponse(
    string RulesetVersion,
    DateOnly AsOf,
    CodeSourceResponse Source,
    CodeApplicationDetail Detail,
    AnalyticsDocument Definitions);""",
"""/// <remarks>
/// <c>Trend</c> es el portafolio filtrado en cada carga: score, Currency Score de CMT y
/// Risk Score promedio. La proyección a uno, dos y tres años viaja en el análisis.
/// </remarks>
public sealed record CodeResponse(
    string RulesetVersion,
    CodeSourceResponse Source,
    CodePortfolio Analysis,
    IReadOnlyList<DashboardTrendPoint> Trend,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación con sus repositorios, componentes y dependencias, y su evolución por carga.</summary>
public sealed record CodeApplicationResponse(
    string RulesetVersion,
    DateOnly AsOf,
    CodeSourceResponse Source,
    CodeApplicationDetail Detail,
    IReadOnlyList<CodeHistoryPoint> History,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación en una carga. Null donde no existía, estaba fuera del alcance o no tenía el dato.</summary>
public sealed record CodeHistoryPoint(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? Currency,
    decimal? RiskScore)
{
    public static CodeHistoryPoint From(Domain.Sources.ScoreHistoryPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return new CodeHistoryPoint(point.VersionId, point.Label, point.CapturedAt, point.Score, point.Currency, point.RiskScore);
    }
}"""),
])

patch(r"src\system\Modernization.SystemApi\Endpoints\CodeEndpoints.cs", [
("""        var analysis = CodeAnalyzer.Analyze(applications, inventory, catalog, rules.Analytics, Today(time));

        return Results.Ok(new CodeResponse(
            rules.Info.Id,
            CodeSourceResponse.From(inventory, catalog),
            analysis,
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));""",
"""        var analysis = CodeAnalyzer.Analyze(applications, inventory, catalog, rules.Analytics, Today(time));
        var trend = await portfolio.GetDashboardTrendAsync(scope, filter, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new CodeResponse(
            rules.Info.Id,
            CodeSourceResponse.From(inventory, catalog),
            analysis,
            trend,
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));"""),
("""        var today = Today(time);
        var detail = CodeAnalyzer.Detail(application, inventory, catalog, rules.Analytics, today);

        return Results.Ok(new CodeApplicationResponse(
            rules.Info.Id,
            today,
            CodeSourceResponse.From(inventory, catalog),
            detail,
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));""",
"""        var today = Today(time);
        var detail = CodeAnalyzer.Detail(application, inventory, catalog, rules.Analytics, today);
        var history = await portfolio.GetHistoryAsync(eaiCode, scope, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new CodeApplicationResponse(
            rules.Info.Id,
            today,
            CodeSourceResponse.From(inventory, catalog),
            detail,
            [.. history.Select(CodeHistoryPoint.From)],
            AnalyticsDocument.From(rules.Analytics, rules.Roadmap.Parameters.SignificantUserBase)));"""),
])
