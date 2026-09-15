using Modernization.Domain;
using Modernization.Scoring.Engine;

namespace Modernization.Rules;

/// <summary>Una metrica de una aplicacion puntuada con dos juegos de reglas.</summary>
public sealed record MetricComparison(
    string Key,
    string Name,
    string Pillar,
    Notice Display,
    decimal WeightFrom,
    decimal WeightTo,
    decimal? MappedFrom,
    decimal? MappedTo,
    decimal? WeightedFrom,
    decimal? WeightedTo,
    bool IncludedFrom,
    bool IncludedTo,
    string? RuleFrom,
    string? RuleTo,
    Notice? ExclusionFrom,
    Notice? ExclusionTo)
{
    /// <summary>True si cambio algo de lo que decide el score de esta metrica.</summary>
    public bool Changed => WeightFrom != WeightTo || MappedFrom != MappedTo || IncludedFrom != IncludedTo;
}

/// <summary>Un pilar de una aplicacion con dos juegos de reglas.</summary>
public sealed record PillarComparison(
    string Key,
    string Name,
    decimal? ScoreFrom,
    decimal? ScoreTo,
    decimal AvailableFrom,
    decimal AvailableTo,
    decimal TotalFrom,
    decimal TotalTo);

/// <summary>Una aplicacion puntuada con dos juegos de reglas, metrica por metrica.</summary>
/// <remarks>Un score null es un dato que no cae en ninguna banda de esas reglas.</remarks>
public sealed record ApplicationComparison(
    string EaiCode,
    string ApplicationName,
    decimal? ScoreFrom,
    decimal? ScoreTo,
    decimal? Delta,
    string? BandFrom,
    string? BandTo,
    decimal? NumeratorFrom,
    decimal? NumeratorTo,
    decimal? DenominatorFrom,
    decimal? DenominatorTo,
    IReadOnlyList<PillarComparison> Pillars,
    IReadOnlyList<MetricComparison> Metrics);

/// <summary>
/// Compara dos juegos de reglas: que cambio y que le hace a una aplicacion concreta.
/// </summary>
/// <remarks>
/// Es la pregunta de antes de publicar: "¿y a mi aplicacion que le pasa?". Se responde con
/// los mismos motores que puntuan, no con una estimacion. Si el dato de la aplicacion no
/// cae en ninguna banda de un lado, ese lado queda sin score y sus metricas se muestran
/// igual, con los pesos de cada version.
/// </remarks>
public static class RulesComparison
{
    /// <summary>Que cambio entre dos juegos de reglas, como avisos.</summary>
    public static IReadOnlyList<Notice> Changes(RulesModel from, RulesModel to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        return RulesDiff.Compare(from, to);
    }

    public static ApplicationComparison Compare(ApplicationSnapshot application, RulesModel from, RulesModel to)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var before = TryCalculate(from, application);
        var after = TryCalculate(to, application);

        var metrics = to.Ruleset.Metrics
            .Select(definition =>
            {
                var old = before?.Metrics.FirstOrDefault(m => string.Equals(m.MetricKey, definition.Key, StringComparison.Ordinal));
                var now = after?.Metrics.FirstOrDefault(m => string.Equals(m.MetricKey, definition.Key, StringComparison.Ordinal));
                var previous = from.Ruleset.Metrics.FirstOrDefault(m => string.Equals(m.Key, definition.Key, StringComparison.Ordinal));

                return new MetricComparison(
                    definition.Key,
                    definition.Name,
                    definition.Pillar,
                    (now ?? old)?.Display ?? Notice.Of(NoticeCodes.ValueNoData),
                    previous?.Weight ?? 0m,
                    definition.Weight,
                    old?.MappedScore,
                    now?.MappedScore,
                    old?.WeightedScore,
                    now?.WeightedScore,
                    old?.IsIncluded ?? false,
                    now?.IsIncluded ?? false,
                    old?.RuleApplied,
                    now?.RuleApplied,
                    old?.Exclusion,
                    now?.Exclusion);
            })
            .ToList();

        var pillars = to.Ruleset.Pillars
            .Select(pillar =>
            {
                var old = before?.Pillars.FirstOrDefault(p => string.Equals(p.Key, pillar.Key, StringComparison.Ordinal));
                var now = after?.Pillars.FirstOrDefault(p => string.Equals(p.Key, pillar.Key, StringComparison.Ordinal));

                return new PillarComparison(
                    pillar.Key,
                    pillar.Name,
                    old?.Score,
                    now?.Score,
                    old?.AvailableWeight ?? 0m,
                    now?.AvailableWeight ?? 0m,
                    old?.TotalWeight ?? 0m,
                    now?.TotalWeight ?? 0m);
            })
            .ToList();

        var scoreFrom = before?.EffectiveScore;
        var scoreTo = after?.EffectiveScore;

        return new ApplicationComparison(
            application.EaiCode,
            application.ApplicationName,
            scoreFrom,
            scoreTo,
            scoreFrom is { } b && scoreTo is { } a ? Math.Round(a - b, 2, MidpointRounding.AwayFromZero) : null,
            scoreFrom is { } bandFrom ? ScoreBands.Of(bandFrom) : null,
            scoreTo is { } bandTo ? ScoreBands.Of(bandTo) : null,
            before?.Numerator,
            after?.Numerator,
            before?.Denominator,
            after?.Denominator,
            pillars,
            metrics);
    }

    private static ScoreBreakdown? TryCalculate(RulesModel model, ApplicationSnapshot application)
    {
        try
        {
            return model.Engine.Calculate(application);
        }
        catch (RulesetException)
        {
            return null;
        }
    }
}
