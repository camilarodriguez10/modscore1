using Modernization.Domain;
using Modernization.Insights;
using Modernization.Scoring.Engine;

namespace Modernization.Rules;

/// <summary>
/// Un juego de reglas en la forma en que se edita: cada valor editable con lo que
/// hace falta para entenderlo, y nada que no se pueda cambiar desde el portal salvo
/// como contexto.
/// </summary>
/// <remarks>
/// Es la pantalla de administracion y a la vez el punto de partida de un borrador:
/// un borrador que devuelve este documento tal cual no cambia nada.
/// </remarks>
public sealed record RulesDocument(
    string Version,
    decimal Floor,
    decimal Range,
    IReadOnlyList<RulesPillar> Pillars,
    IReadOnlyList<RulesMetric> Metrics,
    IReadOnlyList<RulesLever> Levers,
    RulesRoadmap Roadmap,
    AnalyticsDocument Analytics)
{
    public static RulesDocument From(RulesModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var ruleset = model.Ruleset;
        var roadmaps = model.Roadmap.Roadmaps;

        return new RulesDocument(
            model.Id,
            ruleset.Normalization.Floor,
            ruleset.Normalization.Range,
            [.. ruleset.Pillars.Select(p => new RulesPillar(p.Key, p.Name, p.NominalWeight))],
            [.. ruleset.Metrics.Select(m => new RulesMetric(
                m.Key,
                m.Name,
                m.Pillar,
                m.Weight,
                KindName(m.Mapping.Kind),
                m.Mapping.Kind == MappingKind.NumericBands
                    ? [.. (m.Mapping.Bands ?? []).Select(b => new BandDraft(b.Min, b.Max, b.Score, b.Label))]
                    : null,
                m.Mapping.Kind == MappingKind.Categorical ? m.Mapping.Cases : null,
                m.Composite?.CategoryScores,
                m.Source.System,
                m.Note))],
            [.. model.LeverCatalog.All.Select(l => new RulesLever(
                l.Key,
                l.Name,
                l.Action,
                l.MetricKey,
                l.PillarKey,
                l.Enabled,
                l.IsParameterized,
                l.DefaultParameter,
                [.. roadmaps
                    .Where(r => r.Levers.Contains(l.Key, StringComparer.Ordinal))
                    .Select(r => r.Key.ToString())]))],
            new RulesRoadmap(
                model.Roadmap.Parameters.HealthyScore,
                model.Roadmap.Parameters.SignificantUserBase,
                [.. roadmaps.Select(r => new RulesRoadmapPath(
                    r.Key.ToString(),
                    r.Name,
                    MapsNames.Display(r.Disposition),
                    r.RemovesFromPortfolio,
                    r.Levers,
                    [.. r.Milestones.Select(m => new RulesMilestone(m.Name, m.Owner, m.Weeks))]))]),
            AnalyticsDocument.From(model.Analytics, model.Roadmap.Parameters.SignificantUserBase));
    }

    private static string KindName(MappingKind kind) => kind switch
    {
        MappingKind.NumericBands => "numericBands",
        MappingKind.Categorical => "categorical",
        _ => "identity",
    };
}

public sealed record RulesPillar(string Key, string Name, decimal NominalWeight);

/// <summary>
/// Una metrica. Trae bandas, casos o puntajes por categoria segun su tipo de mapeo;
/// lo que no aplica viaja nulo.
/// </summary>
public sealed record RulesMetric(
    string Key,
    string Name,
    string Pillar,
    decimal Weight,
    string Kind,
    IReadOnlyList<BandDraft>? Bands,
    IReadOnlyDictionary<string, decimal>? Cases,
    IReadOnlyDictionary<string, decimal>? CategoryScores,
    string SourceSystem,
    string? Note);

/// <summary>Una palanca, con los roadmaps que la entregan.</summary>
public sealed record RulesLever(
    string Key,
    string Name,
    string Action,
    string MetricKey,
    string PillarKey,
    bool Enabled,
    bool IsParameterized,
    int? DefaultParameter,
    IReadOnlyList<string> UsedBy);

public sealed record RulesRoadmap(decimal HealthyScore, int SignificantUserBase, IReadOnlyList<RulesRoadmapPath> Roadmaps);

public sealed record RulesRoadmapPath(
    string Key,
    string Name,
    string Disposition,
    bool RemovesFromPortfolio,
    IReadOnlyList<string> Levers,
    IReadOnlyList<RulesMilestone> Milestones);

public sealed record RulesMilestone(string Name, string Owner, int Weeks);
