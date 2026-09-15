using System.Globalization;

namespace Modernization.Scoring.Engine;

/// <summary>Resultado de mapear un valor crudo a la escala 0-100.</summary>
/// <param name="Score">Puntaje mapeado.</param>
/// <param name="RuleApplied">Regla concreta que lo produjo, para mostrar en la UI.</param>
public readonly record struct MappingResult(decimal Score, string RuleApplied);

/// <summary>
/// Traduce un valor crudo a puntaje segun la definicion del ruleset.
/// No conoce el modelo de dominio: opera solo sobre el valor y la regla.
/// </summary>
public static class MetricMapper
{
    public static MappingResult Map(MetricDefinition metric, MetricInput input)
    {
        ArgumentNullException.ThrowIfNull(metric);

        return metric.Mapping.Kind switch
        {
            MappingKind.Identity => MapIdentity(metric, input),
            MappingKind.NumericBands => MapBands(metric, input),
            MappingKind.Categorical => MapCategorical(metric, input),
            _ => throw new RulesetException(
                $"La metrica '{metric.Key}' declara un tipo de mapeo desconocido: {metric.Mapping.Kind}."),
        };
    }

    private static MappingResult MapIdentity(MetricDefinition metric, MetricInput input)
    {
        var value = ToDecimal(metric, input.Raw);
        var clamped = Math.Clamp(value, 0m, 100m);
        var label = metric.Mapping.IdentityLabel ?? "direct value on the 0-100 scale";

        return new MappingResult(clamped, $"{Format(value)} => {Format(clamped)} ({label})");
    }

    private static MappingResult MapBands(MetricDefinition metric, MetricInput input)
    {
        if (metric.Mapping.Bands is not { Count: > 0 } bands)
        {
            throw new RulesetException($"La metrica '{metric.Key}' usa numericBands pero no declara bandas.");
        }

        var value = ToDecimal(metric, input.Raw);
        var band = bands.FirstOrDefault(b => b.Contains(value));

        if (band is null)
        {
            throw new RulesetException(
                $"El valor {Format(value)} de la metrica '{metric.Key}' no cae en ninguna banda declarada. " +
                "Las bandas del ruleset deben cubrir todo el rango posible.");
        }

        return new MappingResult(
            band.Score,
            $"{Format(band.Min)}-{Format(band.Max)} ({band.Label}) => {Format(band.Score)}");
    }

    private static MappingResult MapCategorical(MetricDefinition metric, MetricInput input)
    {
        if (metric.Mapping.Cases is not { Count: > 0 } cases)
        {
            throw new RulesetException($"La metrica '{metric.Key}' usa categorical pero no declara casos.");
        }

        var key = input.Raw as string
            ?? input.Raw?.ToString()
            ?? throw new RulesetException($"La metrica '{metric.Key}' recibio un valor nulo con mapeo categorical.");

        if (!cases.TryGetValue(key, out var score))
        {
            throw new RulesetException(
                $"El valor '{key}' de la metrica '{metric.Key}' no esta declarado en el ruleset. " +
                $"Casos validos: {string.Join(", ", cases.Keys.Order(StringComparer.Ordinal))}.");
        }

        return new MappingResult(score, $"{key} => {Format(score)}");
    }

    private static decimal ToDecimal(MetricDefinition metric, object? raw) => raw switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        double db => (decimal)db,
        float f => (decimal)f,
        string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        null => throw new RulesetException($"La metrica '{metric.Key}' recibio un valor nulo con mapeo numerico."),
        _ => throw new RulesetException(
            $"La metrica '{metric.Key}' recibio un valor de tipo {raw.GetType().Name}, que no es numerico."),
    };

    private static string Format(decimal value)
        => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
