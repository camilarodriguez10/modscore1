using System.Globalization;
using Modernization.Domain;
using Modernization.Scoring.Engine;
using Modernization.WhatIf;

namespace Modernization.Roadmap;

/// <summary>
/// El future plan de una aplicación hacia el objetivo del catálogo, paso por paso: qué hacer,
/// sobre qué métrica, desde qué valor hasta cuál, quién lo hace y cuántos puntos suma en orden
/// (pedido del 2026-10-21).
/// </summary>
/// <remarks>
/// Las palancas son las mismas, en el mismo orden y con las mismas cantidades que da el what-if
/// al mismo objetivo (<see cref="WhatIfEngine.ReachTarget"/>): el score después del último paso
/// es exactamente el del what-if.
/// </remarks>
/// <param name="Target">El objetivo del catálogo (90).</param>
/// <param name="IsReachable">False si ni con todas las palancas se alcanza; el plan es entonces lo más cerca que se llega.</param>
/// <param name="IsAlreadyMet">True si la aplicación ya está en el objetivo: no hay nada que planificar.</param>
/// <param name="Baseline">El score de hoy.</param>
/// <param name="Projected">El score al terminar el plan.</param>
/// <param name="Ceiling">El techo con todas las palancas a su máximo.</param>
/// <param name="Steps">Los pasos, en el orden en que suman.</param>
public sealed record ActionPlan(
    decimal Target,
    bool IsReachable,
    bool IsAlreadyMet,
    ScoreBreakdown Baseline,
    ScoreBreakdown Projected,
    ScoreBreakdown Ceiling,
    IReadOnlyList<ActionStep> Steps);

/// <summary>Un paso del plan.</summary>
/// <param name="Order">Posición en el plan, desde 1.</param>
/// <param name="LeverKey">La palanca del what-if que representa.</param>
/// <param name="LeverName">Su nombre.</param>
/// <param name="PillarKey">El pilar que mueve.</param>
/// <param name="MetricKey">La métrica que mueve.</param>
/// <param name="MetricName">El nombre de la métrica en las reglas vigentes.</param>
/// <param name="Owner">Quién lo ejecuta: security, infrastructure, development, architecture u operations.</param>
/// <param name="Parameter">La cantidad, en las palancas de productos.</param>
/// <param name="From">El valor de la métrica antes del paso, como lo muestra el cálculo.</param>
/// <param name="To">El valor después del paso.</param>
/// <param name="MappedFrom">El puntaje de la métrica antes (null si no tenía dato).</param>
/// <param name="MappedTo">El puntaje después.</param>
/// <param name="WeightAdded">Peso que entra al cálculo porque la métrica pasa a tener dato.</param>
/// <param name="Points">Puntos que suma este paso, aplicado después de los anteriores.</param>
/// <param name="ScoreAfter">El score después del paso.</param>
/// <param name="Actions">Las acciones concretas, con la evidencia de la aplicación.</param>
public sealed record ActionStep(
    int Order,
    string LeverKey,
    string LeverName,
    string PillarKey,
    string MetricKey,
    string MetricName,
    string Owner,
    int? Parameter,
    Notice From,
    Notice To,
    decimal? MappedFrom,
    decimal? MappedTo,
    decimal WeightAdded,
    decimal Points,
    decimal ScoreAfter,
    IReadOnlyList<Notice> Actions);

/// <summary>Arma el <see cref="ActionPlan"/> de una aplicación a partir de su plan hacia el objetivo.</summary>
public static class ActionPlanBuilder
{
    public const string Security = "security";
    public const string Infrastructure = "infrastructure";
    public const string Development = "development";
    public const string Architecture = "architecture";
    public const string Operations = "operations";

    public static ActionPlan Build(ApplicationSnapshot snapshot, TargetResult target, WhatIfEngine whatIf, ScoringEngine engine)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(whatIf);
        ArgumentNullException.ThrowIfNull(engine);

        var steps = new List<ActionStep>();
        var current = snapshot;
        var before = target.Baseline;

        foreach (var applied in target.Result?.Applied ?? [])
        {
            var lever = whatIf.Levers.Require(applied.Key);
            var next = lever.Apply(current, applied.Parameter);
            var after = engine.Calculate(next);

            var metricBefore = before.Metrics.FirstOrDefault(m => m.MetricKey == lever.MetricKey);
            var metricAfter = after.Metrics.FirstOrDefault(m => m.MetricKey == lever.MetricKey);

            steps.Add(new ActionStep(
                steps.Count + 1,
                lever.Key,
                lever.Name,
                lever.PillarKey,
                lever.MetricKey,
                metricAfter?.Name ?? metricBefore?.Name ?? lever.MetricKey,
                OwnerOf(lever.Key),
                applied.Parameter,
                metricBefore?.Display ?? Notice.Of(NoticeCodes.ValueNoData),
                metricAfter?.Display ?? Notice.Of(NoticeCodes.ValueNoData),
                metricBefore is { IsIncluded: true } ? metricBefore.MappedScore : null,
                metricAfter is { IsIncluded: true } ? metricAfter.MappedScore : null,
                after.Denominator - before.Denominator,
                Math.Round(after.Score - before.Score, 2, MidpointRounding.AwayFromZero),
                after.Score,
                ActionsOf(lever, current, next, applied.Parameter)));

            current = next;
            before = after;
        }

        return new ActionPlan(
            target.Target,
            target.IsReachable,
            target.IsAlreadyMet,
            target.Baseline,
            target.Result?.Projected ?? target.Baseline,
            target.Ceiling,
            steps);
    }

    /// <summary>Quién ejecuta cada palanca.</summary>
    public static string OwnerOf(string leverKey) => leverKey switch
    {
        "mfa.enable" or "sca.remediateCritical" or "sca.remediateHigh" or "kenna.reduce" => Security,
        "dr.include" or "monitoring.enable" => Infrastructure,
        "archValue.modernize" or "productStrategy.invest" => Architecture,
        "operational.improve" => Operations,
        _ => Development,
    };

    /// <summary>
    /// Las acciones concretas de una palanca sobre esta aplicación. Cada una es un aviso con sus
    /// datos: la interfaz redacta la frase (ADR 0017).
    /// </summary>
    private static List<Notice> ActionsOf(ScenarioLever lever, ApplicationSnapshot before, ApplicationSnapshot after, int? parameter)
    {
        var actions = new List<Notice>();

        switch (lever.Key)
        {
            case "mfa.enable":
                actions.Add(Notice.Of(NoticeCodes.PlanMfa, ("users", before.UserCount)));
                break;

            case "dr.include":
                actions.Add(Notice.Of(
                    NoticeCodes.PlanDisasterRecovery,
                    ("rto", before.RecoveryTimeObjectiveRange),
                    ("rpo", before.RecoveryPointObjectiveRange)));
                break;

            case "sca.remediateCritical":
                ScaActions(actions, before, "Critical", before.ScaFlaws?.Critical);
                break;

            case "sca.remediateHigh":
                ScaActions(actions, before, "High", before.ScaFlaws?.High);
                break;

            case "kenna.reduce":
                actions.Add(Notice.Of(NoticeCodes.PlanKenna, ("from", before.KennaScore), ("to", after.KennaScore)));
                break;

            case "oss.improve":
                actions.Add(Notice.Of(NoticeCodes.PlanOss, ("from", before.OpenSourceScore), ("to", after.OpenSourceScore)));
                break;

            case "rawTech.improve":
                actions.Add(Notice.Of(NoticeCodes.PlanRawTech, ("from", before.RawTechScore), ("to", after.RawTechScore)));

                if (before.ArchitecturalValue is { ProvidedValue: null } value
                    && ((value.ObsoleteCount ?? 0) + (value.NotApprovedForUseCount ?? 0)) > 0)
                {
                    actions.Add(Notice.Of(
                        NoticeCodes.PlanRawTechComponents,
                        ("obsolete", value.ObsoleteCount ?? 0),
                        ("notApproved", value.NotApprovedForUseCount ?? 0)));
                }

                break;

            case "api.onboardApim":
                actions.Add(Notice.Of(NoticeCodes.PlanApi, ("current", before.NativeApi?.ToString())));
                break;

            case "techDebt.clear":
                actions.Add(Notice.Of(NoticeCodes.PlanTechDebt, ("from", before.TechDebtScore)));
                break;

            case "archValue.modernize":
                actions.Add(Notice.Of(
                    NoticeCodes.PlanArchValue,
                    ("count", parameter),
                    ("obsolete", Moved(before.ArchitecturalValue?.ObsoleteCount, after.ArchitecturalValue?.ObsoleteCount)),
                    ("notApproved", Moved(before.ArchitecturalValue?.NotApprovedForUseCount, after.ArchitecturalValue?.NotApprovedForUseCount)),
                    ("heritage", Moved(before.ArchitecturalValue?.HeritageCount, after.ArchitecturalValue?.HeritageCount))));
                break;

            case "productStrategy.invest":
                actions.Add(Notice.Of(
                    NoticeCodes.PlanProductStrategy,
                    ("count", parameter),
                    ("eliminate", Moved(before.ProductStrategy?.EliminateCount, after.ProductStrategy?.EliminateCount)),
                    ("migrate", Moved(before.ProductStrategy?.MigrateCount, after.ProductStrategy?.MigrateCount)),
                    ("tolerate", Moved(before.ProductStrategy?.TolerateCount, after.ProductStrategy?.TolerateCount))));
                break;

            case "monitoring.enable":
                actions.Add(Notice.Of(NoticeCodes.PlanMonitoring, ("missing", MissingMonitoring(before.Monitoring))));
                break;

            case "operational.improve":
                actions.Add(Notice.Of(NoticeCodes.PlanOperational, ("from", before.OperationalScore), ("to", after.OperationalScore)));
                break;

            default:
                actions.Add(Notice.Of(NoticeCodes.PlanGeneric, ("action", lever.Action)));
                break;
        }

        return actions;
    }

    /// <summary>
    /// Los hallazgos de una severidad: cuántos hay que cerrar y, de los más graves, la
    /// librería, la vulnerabilidad y su CVSS.
    /// </summary>
    private static void ScaActions(List<Notice> actions, ApplicationSnapshot snapshot, string severity, int? count)
    {
        actions.Add(Notice.Of(NoticeCodes.PlanScaRemediate, ("severity", severity), ("count", count)));

        var findings = snapshot.OpenScaFindings.Where(f => f.Severity == severity).ToList();

        foreach (var finding in findings)
        {
            actions.Add(Notice.Of(
                NoticeCodes.PlanScaFinding,
                ("library", finding.LibraryName),
                ("vendor", finding.LibraryVendor),
                ("cve", finding.CveId),
                ("vulnerability", finding.VulnerabilityName),
                ("cvss", finding.Cvssv3Score),
                ("age", finding.AgeDays)));
        }

        if (count is { } total && total > findings.Count && findings.Count > 0)
        {
            actions.Add(Notice.Of(NoticeCodes.PlanScaMore, ("more", total - findings.Count), ("severity", severity)));
        }
    }

    private static int Moved(int? before, int? after) => Math.Max(0, (before ?? 0) - (after ?? 0));

    /// <summary>Las capacidades de monitoreo que la aplicación no tiene, separadas por comas.</summary>
    private static string MissingMonitoring(MonitoringCapabilities? monitoring)
    {
        var missing = new List<string>();

        if (monitoring?.ElasticAgent != true)
        {
            missing.Add("elastic");
        }

        if (monitoring?.ApplicationPerformanceMonitoring != true)
        {
            missing.Add("apm");
        }

        if (monitoring?.SyntheticMonitoring != true)
        {
            missing.Add("synthetic");
        }

        if (monitoring?.GeneralMonitoring != true)
        {
            missing.Add("general");
        }

        return string.Join(',', missing.Select(m => m.ToString(CultureInfo.InvariantCulture)));
    }
}
