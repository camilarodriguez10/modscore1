using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modernization.Scoring.Engine;

/// <summary>
/// Reglas de scoring como datos. Cambiar un peso o una banda es publicar un
/// ruleset nuevo, no recompilar. Los rulesets son inmutables y versionados: cada
/// score registra con cual se calculo.
/// </summary>
public sealed record Ruleset
{
    public required string Version { get; init; }
    public string? Description { get; init; }
    public required Normalization Normalization { get; init; }
    public required IReadOnlyList<PillarDefinition> Pillars { get; init; }
    public required IReadOnlyList<MetricDefinition> Metrics { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Carga y valida un ruleset desde JSON.</summary>
    /// <exception cref="RulesetException">El JSON es invalido o incoherente.</exception>
    public static Ruleset FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        Ruleset? ruleset;
        try
        {
            ruleset = JsonSerializer.Deserialize<Ruleset>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new RulesetException($"El ruleset no es JSON valido: {ex.Message}", ex);
        }

        if (ruleset is null)
        {
            throw new RulesetException("El ruleset esta vacio.");
        }

        ruleset.Validate();
        return ruleset;
    }

    /// <summary>Carga un ruleset desde disco.</summary>
    public static Ruleset FromFile(string path) => FromJson(File.ReadAllText(path));

    /// <summary>
    /// El ruleset como JSON, con el mismo formato que lee <see cref="FromJson"/>. Es
    /// como se guarda una version publicada desde el portal (Fase 12).
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Falla temprano ante un ruleset incoherente. Un ruleset mal formado produciria
    /// scores silenciosamente incorrectos, que es el peor modo de falla posible aqui.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new RulesetException("El ruleset debe declarar una version.");
        }

        if (Pillars.Count == 0)
        {
            throw new RulesetException("El ruleset debe declarar al menos un pilar.");
        }

        if (Metrics.Count == 0)
        {
            throw new RulesetException("El ruleset debe declarar al menos una metrica.");
        }

        var duplicatePillar = Pillars.GroupBy(p => p.Key, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicatePillar is not null)
        {
            throw new RulesetException($"Pilar duplicado: '{duplicatePillar.Key}'.");
        }

        var duplicateMetric = Metrics.GroupBy(m => m.Key, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateMetric is not null)
        {
            throw new RulesetException($"Metrica duplicada: '{duplicateMetric.Key}'.");
        }

        foreach (var metric in Metrics)
        {
            if (!Pillars.Any(p => string.Equals(p.Key, metric.Pillar, StringComparison.Ordinal)))
            {
                throw new RulesetException(
                    $"La metrica '{metric.Key}' referencia el pilar inexistente '{metric.Pillar}'.");
            }

            if (metric.Weight < 0)
            {
                throw new RulesetException($"La metrica '{metric.Key}' tiene peso negativo.");
            }

            if (metric.ZeroIsNoData && metric.Mapping.Kind != MappingKind.NumericBands)
            {
                throw new RulesetException(
                    $"La metrica '{metric.Key}' declara zeroIsNoData, que solo aplica a bandas numericas.");
            }
        }

        foreach (var pillar in Pillars)
        {
            var declared = Metrics
                .Where(m => string.Equals(m.Pillar, pillar.Key, StringComparison.Ordinal))
                .Sum(m => m.Weight);

            if (declared != pillar.NominalWeight)
            {
                throw new RulesetException(
                    $"El pilar '{pillar.Key}' declara peso nominal {pillar.NominalWeight} " +
                    $"pero sus metricas suman {declared}.");
            }
        }
    }
}

/// <summary>
/// Normalizacion al rango 20-100 (BRD, "ALL Sections").
/// Score = Floor + (Numerador / Denominador) x Range.
/// </summary>
public sealed record Normalization
{
    public required decimal Floor { get; init; }
    public required decimal Range { get; init; }
}

public sealed record PillarDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    /// <summary>Peso nominal del pilar. Debe coincidir con la suma de sus metricas.</summary>
    public required decimal NominalWeight { get; init; }
}

public sealed record MetricDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Pillar { get; init; }

    /// <summary>Peso 0 = informativa: se evalua y se muestra, pero no altera el score.</summary>
    public required decimal Weight { get; init; }

    /// <summary>Clave del extractor que lee el valor crudo del snapshot.</summary>
    public required string Input { get; init; }

    public required SourceDefinition Source { get; init; }
    public required MappingDefinition Mapping { get; init; }

    /// <summary>Puntajes por categoria para derivar un compuesto desde conteos.</summary>
    public CompositeDefinition? Composite { get; init; }

    /// <summary>
    /// BRD, "ALL Sections": "Null or zero values are excluded from scoring". Un 0
    /// sale del numerador y del denominador igual que un nulo. Queda en false donde
    /// la tabla del BRD puntua el 0 como dato propio (Tech Debt 0 -> 100, SCA
    /// None -> 100), porque la regla especifica gana sobre la general (ADR 0019).
    /// </summary>
    public bool ZeroIsNoData { get; init; }

    /// <summary>Nota de trazabilidad al BRD o al dashboard.</summary>
    public string? Note { get; init; }
}

public sealed record SourceDefinition
{
    public required string System { get; init; }
    public required string Report { get; init; }
    public required string Field { get; init; }
}

public enum MappingKind
{
    /// <summary>El valor crudo ya esta en escala 0-100.</summary>
    Identity,

    /// <summary>Bandas numericas con limites inclusivos.</summary>
    NumericBands,

    /// <summary>Diccionario de casos exactos sobre el valor crudo formateado.</summary>
    Categorical,
}

public sealed record MappingDefinition
{
    public required MappingKind Kind { get; init; }

    /// <summary>Bandas para <see cref="MappingKind.NumericBands"/>.</summary>
    public IReadOnlyList<BandDefinition>? Bands { get; init; }

    /// <summary>Casos para <see cref="MappingKind.Categorical"/>.</summary>
    public IReadOnlyDictionary<string, decimal>? Cases { get; init; }

    /// <summary>Etiqueta de la regla cuando el mapeo es identidad.</summary>
    public string? IdentityLabel { get; init; }
}

public sealed record BandDefinition
{
    public required decimal Min { get; init; }
    public required decimal Max { get; init; }
    public required decimal Score { get; init; }
    public required string Label { get; init; }

    public bool Contains(decimal value) => value >= Min && value <= Max;
}

/// <summary>
/// Puntajes por categoria usados para derivar un valor compuesto 0-100 como
/// promedio ponderado por conteo. Solo se aplica cuando la fuente no entrega el
/// compuesto ya calculado.
/// </summary>
public sealed record CompositeDefinition
{
    public required IReadOnlyDictionary<string, decimal> CategoryScores { get; init; }
}

/// <summary>Ruleset invalido o incoherente.</summary>
public sealed class RulesetException : Exception
{
    public RulesetException(string message) : base(message) { }

    public RulesetException(string message, Exception innerException) : base(message, innerException) { }
}
