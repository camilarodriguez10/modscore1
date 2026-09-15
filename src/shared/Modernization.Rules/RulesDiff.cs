using System.Globalization;
using Modernization.Domain;
using Modernization.Scoring.Engine;

namespace Modernization.Rules;

/// <summary>
/// Que cambio entre dos juegos de reglas, como avisos que la interfaz redacta.
/// </summary>
/// <remarks>
/// Se calcula comparando el resultado, no leyendo el borrador: un borrador que
/// reenvia todas las metricas con sus mismos valores no cambio nada, y asi lo dice.
/// Es lo que queda guardado con la version y lo que se audita.
/// </remarks>
internal static class RulesDiff
{
    public static IReadOnlyList<Notice> Compare(RulesModel before, RulesModel after)
    {
        var changes = new List<Notice>();

        CompareMetrics(before.Ruleset, after.Ruleset, changes);
        CompareLevers(before, after, changes);
        CompareRoadmap(before, after, changes);

        return changes;
    }

    private static void CompareMetrics(Ruleset before, Ruleset after, List<Notice> changes)
    {
        foreach (var metric in after.Metrics)
        {
            var old = before.Metrics.First(m => string.Equals(m.Key, metric.Key, StringComparison.Ordinal));

            if (old.Weight != metric.Weight)
            {
                changes.Add(Notice.Of(NoticeCodes.ChangeWeight, ("metric", metric.Name), ("from", old.Weight), ("to", metric.Weight)));
            }

            if (Describe(old.Mapping.Bands) != Describe(metric.Mapping.Bands))
            {
                changes.Add(Notice.Of(
                    NoticeCodes.ChangeBands,
                    ("metric", metric.Name),
                    ("from", Describe(old.Mapping.Bands)),
                    ("to", Describe(metric.Mapping.Bands))));
            }

            CompareScores(old.Mapping.Cases, metric.Mapping.Cases, NoticeCodes.ChangeCase, "case", metric.Name, changes);
            CompareScores(
                old.Composite?.CategoryScores,
                metric.Composite?.CategoryScores,
                NoticeCodes.ChangeCategory,
                "category",
                metric.Name,
                changes);
        }
    }

    private static void CompareScores(
        IReadOnlyDictionary<string, decimal>? before,
        IReadOnlyDictionary<string, decimal>? after,
        string code,
        string itemName,
        string metricName,
        List<Notice> changes)
    {
        if (before is null || after is null)
        {
            return;
        }

        foreach (var (key, value) in after)
        {
            if (before.TryGetValue(key, out var old) && old != value)
            {
                changes.Add(Notice.Of(code, ("metric", metricName), (itemName, key), ("from", old), ("to", value)));
            }
        }
    }

    private static void CompareLevers(RulesModel before, RulesModel after, List<Notice> changes)
    {
        foreach (var lever in after.LeverCatalog.All)
        {
            var old = before.LeverCatalog.All.First(l => string.Equals(l.Key, lever.Key, StringComparison.Ordinal));

            if (old.Enabled != lever.Enabled)
            {
                changes.Add(Notice.Of(
                    NoticeCodes.ChangeLeverEnabled,
                    ("lever", lever.Name),
                    ("enabled", lever.Enabled ? "true" : "false")));
            }

            if (old.DefaultParameter != lever.DefaultParameter)
            {
                changes.Add(Notice.Of(
                    NoticeCodes.ChangeLeverParameter,
                    ("lever", lever.Name),
                    ("from", old.DefaultParameter),
                    ("to", lever.DefaultParameter)));
            }
        }
    }

    private static void CompareRoadmap(RulesModel before, RulesModel after, List<Notice> changes)
    {
        var oldParameters = before.Roadmap.Parameters;
        var parameters = after.Roadmap.Parameters;

        if (oldParameters.HealthyScore != parameters.HealthyScore)
        {
            changes.Add(Notice.Of(NoticeCodes.ChangeHealthyScore, ("from", oldParameters.HealthyScore), ("to", parameters.HealthyScore)));
        }

        if (oldParameters.SignificantUserBase != parameters.SignificantUserBase)
        {
            changes.Add(Notice.Of(NoticeCodes.ChangeUserBase, ("from", oldParameters.SignificantUserBase), ("to", parameters.SignificantUserBase)));
        }

        foreach (var roadmap in after.Roadmap.Roadmaps)
        {
            var old = before.Roadmap.Roadmaps.First(r => r.Key == roadmap.Key);

            var added = roadmap.Levers.Except(old.Levers, StringComparer.Ordinal).ToList();
            var removed = old.Levers.Except(roadmap.Levers, StringComparer.Ordinal).ToList();

            if (added.Count > 0 || removed.Count > 0)
            {
                changes.Add(Notice.Of(
                    NoticeCodes.ChangeRoadmapLevers,
                    ("roadmap", roadmap.Name),
                    ("added", Names(after, added)),
                    ("removed", Names(before, removed))));
            }

            for (var i = 0; i < roadmap.Milestones.Count && i < old.Milestones.Count; i++)
            {
                if (old.Milestones[i].Weeks != roadmap.Milestones[i].Weeks)
                {
                    changes.Add(Notice.Of(
                        NoticeCodes.ChangeMilestoneWeeks,
                        ("roadmap", roadmap.Name),
                        ("milestone", roadmap.Milestones[i].Name),
                        ("from", old.Milestones[i].Weeks),
                        ("to", roadmap.Milestones[i].Weeks)));
                }
            }
        }
    }

    /// <summary>Los nombres de catalogo de unas palancas, en una lista legible.</summary>
    private static string Names(RulesModel model, List<string> keys)
        => string.Join(", ", keys.Select(key =>
            model.LeverCatalog.All.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.Ordinal))?.Name ?? key));

    /// <summary>Las bandas en una forma compacta y comparable: <c>0-59=20; 60-74=60</c>.</summary>
    private static string Describe(IReadOnlyList<BandDefinition>? bands)
        => bands is null
            ? string.Empty
            : string.Join("; ", bands.Select(b => string.Create(
                CultureInfo.InvariantCulture,
                $"{Number(b.Min)}-{Number(b.Max)}={Number(b.Score)}")));

    private static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
