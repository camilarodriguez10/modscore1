using System.Globalization;
using Modernization.Domain;
using Modernization.Roadmap;
using Modernization.Scoring.Engine;
using Modernization.WhatIf;

namespace Modernization.Rules;

/// <summary>El resultado de aplicar un borrador: o errores, o un candidato con sus cambios.</summary>
public sealed record RulesEdit
{
    public required IReadOnlyList<Notice> Errors { get; init; }

    public required IReadOnlyList<Notice> Changes { get; init; }

    /// <summary>Las reglas resultantes, sin identidad todavia. Null si hubo errores.</summary>
    public RulesModel? Candidate { get; init; }

    public bool IsValid => Errors.Count == 0 && Candidate is not null;
}

/// <summary>
/// Aplica un borrador sobre las reglas vigentes y lo valida.
/// </summary>
/// <remarks>
/// Todo error se reporta como aviso con su codigo, y se reportan todos a la vez:
/// quien edita veinte pesos necesita ver los tres que estan mal, no descubrirlos de
/// uno en uno. Un valor recibido que no corresponde a nada del catalogo no se repite
/// en el aviso —se da su posicion—: no hay motivo para devolverle a nadie un texto
/// que el mismo envio.
/// <para>
/// Despues de la validacion propia, el candidato se construye con los mismos
/// constructores que validan un ruleset al cargarlo. Si algo pasara este filtro y
/// no aquel, el borrador se rechaza igual.
/// </para>
/// </remarks>
public static class RulesEditor
{
    public static RulesEdit Apply(RulesModel basis, RulesDraft draft)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new List<Notice>();

        var ruleset = ApplyMetrics(basis.Ruleset, draft.Metrics, errors);
        var levers = ApplyLevers(basis.LeverCatalog, draft.Levers, errors);
        var roadmap = ApplyRoadmap(basis.Roadmap, basis.LeverCatalog, levers, draft.Roadmap, errors);

        if (errors.Count > 0)
        {
            return new RulesEdit { Errors = errors, Changes = [] };
        }

        RulesModel candidate;

        try
        {
            candidate = new RulesModel(
                basis.Info with { BasedOn = basis.Id, PublishedAt = null, Changes = [], Impact = null, IsBuiltIn = false },
                ruleset,
                levers,
                roadmap);
        }
        catch (RulesException)
        {
            return new RulesEdit { Errors = [Notice.Of(NoticeCodes.RulesInvalid)], Changes = [] };
        }

        return new RulesEdit
        {
            Errors = [],
            Changes = RulesDiff.Compare(basis, candidate),
            Candidate = candidate,
        };
    }

    /// <summary>
    /// El motivo de una publicacion: una linea, sin caracteres de control, ni tan
    /// corta que no diga nada ni tan larga que no se lea en la auditoria.
    /// </summary>
    public static bool TryNormalizeReason(string? reason, out string normalized)
    {
        normalized = reason?.Trim() ?? string.Empty;

        return normalized.Length is >= RulesLimits.MinReasonLength and <= RulesLimits.MaxReasonLength
            && !normalized.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format);
    }

    // ------------------------------------------------------------- metricas

    private static Ruleset ApplyMetrics(Ruleset basis, IReadOnlyList<MetricDraft>? drafts, List<Notice> errors)
    {
        if (drafts is null)
        {
            return basis;
        }

        if (drafts.Count > RulesLimits.MaxMetrics)
        {
            errors.Add(Notice.Of(NoticeCodes.RulesTooLarge, ("part", "metrics"), ("max", RulesLimits.MaxMetrics)));
            return basis;
        }

        var metrics = basis.Metrics.ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var position = 0; position < drafts.Count; position++)
        {
            var draft = drafts[position];
            var index = draft is null
                ? -1
                : metrics.FindIndex(m => string.Equals(m.Key, draft.Key, StringComparison.Ordinal));

            if (draft is null || index < 0)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesMetricUnknown, ("position", position + 1)));
                continue;
            }

            var metric = metrics[index];

            if (!seen.Add(metric.Key))
            {
                errors.Add(Notice.Of(NoticeCodes.RulesDuplicate, ("item", metric.Name)));
                continue;
            }

            if (!IsScale(draft.Weight))
            {
                errors.Add(Notice.Of(NoticeCodes.RulesWeightInvalid, ("metric", metric.Name)));
            }

            var mapping = metric.Mapping;
            var composite = metric.Composite;

            if (draft.Bands is not null)
            {
                if (mapping.Kind != MappingKind.NumericBands)
                {
                    errors.Add(Notice.Of(NoticeCodes.RulesKindMismatch, ("metric", metric.Name)));
                }
                else
                {
                    mapping = mapping with { Bands = Bands(metric, draft.Bands, errors) };
                }
            }

            if (draft.Cases is not null)
            {
                if (mapping.Kind != MappingKind.Categorical || mapping.Cases is null)
                {
                    errors.Add(Notice.Of(NoticeCodes.RulesKindMismatch, ("metric", metric.Name)));
                }
                else
                {
                    mapping = mapping with
                    {
                        Cases = Scores(metric, mapping.Cases, draft.Cases, NoticeCodes.RulesCaseKeys, errors),
                    };
                }
            }

            if (draft.CategoryScores is not null)
            {
                if (composite is null)
                {
                    errors.Add(Notice.Of(NoticeCodes.RulesKindMismatch, ("metric", metric.Name)));
                }
                else
                {
                    composite = composite with
                    {
                        CategoryScores = Scores(metric, composite.CategoryScores, draft.CategoryScores, NoticeCodes.RulesCategoryKeys, errors),
                    };
                }
            }

            metrics[index] = metric with { Weight = draft.Weight, Mapping = mapping, Composite = composite };
        }

        var total = metrics.Sum(m => m.Weight);

        if (total != RulesLimits.TotalWeight)
        {
            errors.Add(Notice.Of(NoticeCodes.RulesWeightTotal, ("total", total), ("expected", RulesLimits.TotalWeight)));
        }

        // El peso de cada pilar no se edita: es la suma de sus metricas. Pedirlo por
        // separado solo abriria la puerta a que no coincidan.
        var pillars = basis.Pillars
            .Select(pillar => pillar with
            {
                NominalWeight = metrics
                    .Where(m => string.Equals(m.Pillar, pillar.Key, StringComparison.Ordinal))
                    .Sum(m => m.Weight),
            })
            .ToList();

        return basis with { Pillars = pillars, Metrics = metrics };
    }

    private static List<BandDefinition> Bands(MetricDefinition metric, IReadOnlyList<BandDraft> drafts, List<Notice> errors)
    {
        if (drafts.Count is 0 or > RulesLimits.MaxBands)
        {
            errors.Add(Notice.Of(NoticeCodes.RulesBandCount, ("metric", metric.Name), ("max", RulesLimits.MaxBands)));
            return [.. metric.Mapping.Bands ?? []];
        }

        var bands = new List<BandDefinition>(drafts.Count);

        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];

            if (draft is null)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesBandBounds, ("metric", metric.Name), ("band", i + 1)));
                continue;
            }

            if (!IsBound(draft.Min) || !IsBound(draft.Max) || draft.Min > draft.Max)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesBandBounds, ("metric", metric.Name), ("band", i + 1)));
            }

            if (!IsScale(draft.Score))
            {
                errors.Add(Notice.Of(NoticeCodes.RulesScoreInvalid, ("metric", metric.Name), ("item", i + 1)));
            }

            var label = draft.Label?.Trim() ?? string.Empty;

            if (!IsLabel(label))
            {
                errors.Add(Notice.Of(
                    NoticeCodes.RulesBandLabel,
                    ("metric", metric.Name),
                    ("band", i + 1),
                    ("max", RulesLimits.MaxBandLabelLength)));
            }

            bands.Add(new BandDefinition { Min = draft.Min, Max = draft.Max, Score = draft.Score, Label = label });
        }

        // Ordenadas y sin solaparse. Un valor que cae en dos bandas puntuaria segun
        // el orden de la lista, que no es una regla que nadie haya decidido.
        for (var i = 1; i < bands.Count; i++)
        {
            if (bands[i].Min <= bands[i - 1].Max)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesBandOrder, ("metric", metric.Name)));
                break;
            }
        }

        return bands;
    }

    /// <summary>
    /// Los puntajes de un diccionario de casos o categorias. Las claves no se editan:
    /// son los valores que produce el extractor, y una clave nueva nunca coincidiria
    /// con nada.
    /// </summary>
    private static Dictionary<string, decimal> Scores(
        MetricDefinition metric,
        IReadOnlyDictionary<string, decimal> current,
        IReadOnlyDictionary<string, decimal> draft,
        string keysCode,
        List<Notice> errors)
    {
        var result = new Dictionary<string, decimal>(current.Count, StringComparer.Ordinal);

        if (draft.Count != current.Count || draft.Keys.Any(key => !current.ContainsKey(key)))
        {
            errors.Add(Notice.Of(keysCode, ("metric", metric.Name)));

            foreach (var (key, value) in current)
            {
                result[key] = value;
            }

            return result;
        }

        // En el orden del ruleset, no en el del borrador.
        foreach (var key in current.Keys)
        {
            var score = draft[key];

            if (!IsScale(score))
            {
                errors.Add(Notice.Of(NoticeCodes.RulesScoreInvalid, ("metric", metric.Name), ("item", key)));
            }

            result[key] = score;
        }

        return result;
    }

    // ------------------------------------------------------------- palancas

    private static LeverSettings ApplyLevers(LeverCatalog catalog, IReadOnlyList<LeverDraft>? drafts, List<Notice> errors)
    {
        var settings = catalog.All.ToDictionary(
            lever => lever.Key,
            lever => new LeverSetting(lever.Key, lever.Enabled, lever.DefaultParameter),
            StringComparer.Ordinal);

        if (drafts is not null && drafts.Count > RulesLimits.MaxLevers)
        {
            errors.Add(Notice.Of(NoticeCodes.RulesTooLarge, ("part", "levers"), ("max", RulesLimits.MaxLevers)));
        }
        else if (drafts is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var position = 0; position < drafts.Count; position++)
            {
                var draft = drafts[position];
                var lever = draft is null
                    ? null
                    : catalog.All.FirstOrDefault(l => string.Equals(l.Key, draft.Key, StringComparison.Ordinal));

                if (draft is null || lever is null)
                {
                    errors.Add(Notice.Of(NoticeCodes.RulesLeverUnknown, ("position", position + 1)));
                    continue;
                }

                if (!seen.Add(lever.Key))
                {
                    errors.Add(Notice.Of(NoticeCodes.RulesDuplicate, ("item", lever.Name)));
                    continue;
                }

                int? parameter = null;

                if (lever.IsParameterized)
                {
                    parameter = draft.DefaultParameter ?? lever.DefaultParameter;

                    if (parameter is < 1 or > LeverSettings.MaxParameter)
                    {
                        errors.Add(Notice.Of(
                            NoticeCodes.RulesLeverParameter,
                            ("lever", lever.Name),
                            ("max", LeverSettings.MaxParameter)));
                    }
                }
                else if (draft.DefaultParameter is not null)
                {
                    errors.Add(Notice.Of(
                        NoticeCodes.RulesLeverParameter,
                        ("lever", lever.Name),
                        ("max", LeverSettings.MaxParameter)));
                }

                settings[lever.Key] = new LeverSetting(lever.Key, draft.Enabled, parameter);
            }
        }

        return new LeverSettings { Levers = [.. catalog.All.Select(lever => settings[lever.Key])] };
    }

    // -------------------------------------------------------------- roadmap

    private static RoadmapCatalog ApplyRoadmap(
        RoadmapCatalog basis,
        LeverCatalog catalog,
        LeverSettings levers,
        RoadmapDraft? draft,
        List<Notice> errors)
    {
        var parameters = basis.Parameters;
        var roadmaps = basis.Roadmaps.ToList();

        if (draft is not null)
        {
            if (draft.HealthyScore is < 20m or > 100m || decimal.Round(draft.HealthyScore, 2) != draft.HealthyScore)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesHealthyScore));
            }

            if (draft.SignificantUserBase is < 0 or > RulesLimits.MaxUserBase)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesUserBase, ("max", RulesLimits.MaxUserBase)));
            }

            parameters = parameters with
            {
                HealthyScore = draft.HealthyScore,
                SignificantUserBase = draft.SignificantUserBase,
            };

            if (draft.Roadmaps is { Count: > RulesLimits.MaxRoadmaps })
            {
                errors.Add(Notice.Of(NoticeCodes.RulesTooLarge, ("part", "roadmaps"), ("max", RulesLimits.MaxRoadmaps)));
            }
            else if (draft.Roadmaps is not null)
            {
                ApplyPaths(roadmaps, draft.Roadmaps, errors);
            }
        }

        // Coherencia con las palancas, se haya tocado el roadmap o no: apagar una
        // palanca que un roadmap entrega es un cambio de roadmap, y tiene que decirse.
        var enabled = levers.Levers.Where(l => l.Enabled).Select(l => l.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var roadmap in roadmaps)
        {
            if (roadmap.RemovesFromPortfolio && roadmap.Levers.Count > 0)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesRoadmapLeaves, ("roadmap", roadmap.Name)));
            }

            foreach (var key in roadmap.Levers.Where(key => !enabled.Contains(key)))
            {
                var name = catalog.All.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.Ordinal))?.Name;

                errors.Add(name is null
                    ? Notice.Of(NoticeCodes.RulesRoadmapLever, ("roadmap", roadmap.Name))
                    : Notice.Of(NoticeCodes.RulesRoadmapLever, ("roadmap", roadmap.Name), ("lever", name)));
            }
        }

        return basis with { Parameters = parameters, Roadmaps = roadmaps };
    }

    private static void ApplyPaths(List<RoadmapDefinition> roadmaps, IReadOnlyList<RoadmapPathDraft> drafts, List<Notice> errors)
    {
        var seen = new HashSet<RoadmapType>();

        for (var position = 0; position < drafts.Count; position++)
        {
            var draft = drafts[position];
            var index = draft is not null && TryRoadmap(draft.Key, out var type)
                ? roadmaps.FindIndex(r => r.Key == type)
                : -1;

            if (draft is null || index < 0)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesRoadmapUnknown, ("position", position + 1)));
                continue;
            }

            var definition = roadmaps[index];

            if (!seen.Add(definition.Key))
            {
                errors.Add(Notice.Of(NoticeCodes.RulesDuplicate, ("item", definition.Name)));
                continue;
            }

            var keys = draft.Levers ?? [];

            if (keys.Count > RulesLimits.MaxLeversPerRoadmap)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesTooLarge, ("part", "roadmapLevers"), ("max", RulesLimits.MaxLeversPerRoadmap)));
                continue;
            }

            if (keys.Any(key => key is null) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
            {
                errors.Add(Notice.Of(NoticeCodes.RulesDuplicate, ("item", definition.Name)));
                continue;
            }

            var weeks = draft.MilestoneWeeks ?? [];

            if (weeks.Count != definition.Milestones.Count || weeks.Any(w => w is < 1 or > RulesLimits.MaxWeeks))
            {
                errors.Add(Notice.Of(
                    NoticeCodes.RulesMilestones,
                    ("roadmap", definition.Name),
                    ("count", definition.Milestones.Count),
                    ("max", RulesLimits.MaxWeeks)));
                continue;
            }

            roadmaps[index] = definition with
            {
                Levers = [.. keys],
                Milestones = [.. definition.Milestones.Select((milestone, i) => milestone with { Weeks = weeks[i] })],
            };
        }
    }

    /// <summary>Roadmap por lista blanca de nombres: <c>Enum.TryParse</c> aceptaria tambien un numero.</summary>
    private static bool TryRoadmap(string? key, out RoadmapType type)
    {
        type = default;

        if (string.IsNullOrEmpty(key) || !Enum.GetNames<RoadmapType>().Contains(key, StringComparer.Ordinal))
        {
            return false;
        }

        type = Enum.Parse<RoadmapType>(key);
        return true;
    }

    // ------------------------------------------------------------- valores

    /// <summary>Un peso o un puntaje: entre 0 y 100, con hasta dos decimales.</summary>
    private static bool IsScale(decimal value)
        => value is >= 0m and <= 100m && decimal.Round(value, 2) == value;

    /// <summary>Un limite de banda: acotado, con hasta cuatro decimales.</summary>
    private static bool IsBound(decimal value)
        => Math.Abs(value) <= RulesLimits.MaxBandBound && decimal.Round(value, 4) == value;

    private static bool IsLabel(string label)
        => label.Length is > 0 and <= RulesLimits.MaxBandLabelLength
            && !label.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format);
}
