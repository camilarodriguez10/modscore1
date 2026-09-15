using Modernization.Domain;

namespace Modernization.Scoring.Engine;

/// <summary>
/// Motor de scoring. Puro y determinista: sin reloj, sin red, sin disco.
/// La misma entrada produce siempre la misma salida, que es lo que permite
/// reproducir el dashboard exactamente y auditar cualquier score a mano.
/// </summary>
/// <remarks>
/// Formula (BRD, "Scoring Method"):
/// <code>Score = 20 + (Numerador / Denominador) x 80</code>
/// donde el numerador suma los puntajes ponderados de las metricas con dato y el
/// denominador suma sus pesos. Las metricas sin dato se excluyen de ambos.
/// </remarks>
public sealed class ScoringEngine
{
    private readonly Ruleset _ruleset;

    public ScoringEngine(Ruleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ruleset.Validate();
        _ruleset = ruleset;
    }

    public string RulesetVersion => _ruleset.Version;

    /// <summary>
    /// Las reglas con que calcula. El what-if las lee para fijar el objetivo de las
    /// palancas que llevan una metrica a su mejor banda.
    /// </summary>
    public Ruleset Ruleset => _ruleset;

    /// <summary>
    /// Calcula el score de una aplicacion con desglose completo.
    /// </summary>
    /// <param name="snapshot">Datos canonicos de la aplicacion.</param>
    /// <param name="scoreOverride">Override manual auditado, si aplica.</param>
    /// <param name="calculatedAt">
    /// Marca de tiempo del calculo. Se pasa desde afuera para que el motor siga
    /// siendo determinista; por defecto usa el momento de captura del snapshot.
    /// </param>
    public ScoreBreakdown Calculate(
        ApplicationSnapshot snapshot,
        ScoreOverride? scoreOverride = null,
        DateTimeOffset? calculatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var evaluations = new List<MetricEvaluation>(_ruleset.Metrics.Count);

        foreach (var metric in _ruleset.Metrics)
        {
            evaluations.Add(Evaluate(metric, snapshot));
        }

        var (numerator, denominator) = Accumulate(evaluations);

        var pillars = _ruleset.Pillars
            .Select(pillar => BuildPillar(pillar, evaluations))
            .ToList();

        return new ScoreBreakdown
        {
            EaiCode = snapshot.EaiCode,
            ApplicationName = snapshot.ApplicationName,
            RulesetVersion = _ruleset.Version,
            Numerator = numerator,
            Denominator = denominator,
            Score = Normalize(numerator, denominator),
            Pillars = pillars,
            Metrics = evaluations,
            Override = scoreOverride,
            CalculatedAt = calculatedAt ?? snapshot.CapturedAt,
        };
    }

    // -----------------------------------------------------------------------

    private static MetricEvaluation Evaluate(MetricDefinition metric, ApplicationSnapshot snapshot)
    {
        var input = MetricExtractors.Extract(metric.Input, snapshot, metric);
        var isInformational = metric.Weight == 0;

        var provenance = new SourceProvenance
        {
            System = metric.Source.System,
            Report = metric.Source.Report,
            Field = metric.Source.Field,
            Provider = snapshot.Provider,
            ExtractedAt = snapshot.CapturedAt,
        };

        if (!input.HasData)
        {
            return new MetricEvaluation
            {
                MetricKey = metric.Key,
                Name = metric.Name,
                PillarKey = metric.Pillar,
                RawValue = null,
                Display = input.Display,
                MappedScore = null,
                Weight = metric.Weight,
                WeightedScore = null,
                IsIncluded = false,
                IsInformational = isInformational,
                Exclusion = input.Missing,
                RuleApplied = string.Empty,
                Provenance = provenance,
            };
        }

        var mapping = MetricMapper.Map(metric, input);

        return new MetricEvaluation
        {
            MetricKey = metric.Key,
            Name = metric.Name,
            PillarKey = metric.Pillar,
            RawValue = input.Raw,
            Display = input.Display,
            MappedScore = mapping.Score,
            Weight = metric.Weight,
            WeightedScore = metric.Weight * mapping.Score / 100m,
            IsIncluded = true,
            IsInformational = isInformational,
            Exclusion = null,
            RuleApplied = mapping.RuleApplied,
            Provenance = provenance,
        };
    }

    private static (decimal Numerator, decimal Denominator) Accumulate(IEnumerable<MetricEvaluation> evaluations)
    {
        decimal numerator = 0;
        decimal denominator = 0;

        foreach (var evaluation in evaluations)
        {
            if (!evaluation.IsIncluded)
            {
                continue;
            }

            numerator += evaluation.WeightedScore ?? 0m;
            denominator += evaluation.Weight;
        }

        return (numerator, denominator);
    }

    private PillarBreakdown BuildPillar(PillarDefinition pillar, IReadOnlyList<MetricEvaluation> evaluations)
    {
        var owned = evaluations
            .Where(e => string.Equals(e.PillarKey, pillar.Key, StringComparison.Ordinal))
            .ToList();

        var (numerator, denominator) = Accumulate(owned);

        return new PillarBreakdown
        {
            Key = pillar.Key,
            Name = pillar.Name,
            TotalWeight = pillar.NominalWeight,
            AvailableWeight = denominator,
            Numerator = numerator,
            Score = denominator == 0 ? null : Normalize(numerator, denominator),
            MetricKeys = owned.Select(e => e.MetricKey).ToList(),
        };
    }

    /// <summary>
    /// Score = Floor + (Numerador / Denominador) x Range, redondeado a 2 decimales
    /// que es la precision que publica el dashboard.
    /// Sin metricas con dato el resultado es el piso: el BRD nunca produce 0.
    /// </summary>
    private decimal Normalize(decimal numerator, decimal denominator)
    {
        if (denominator == 0)
        {
            return _ruleset.Normalization.Floor;
        }

        var raw = _ruleset.Normalization.Floor + (numerator / denominator * _ruleset.Normalization.Range);
        return Math.Round(raw, 2, MidpointRounding.AwayFromZero);
    }
}
