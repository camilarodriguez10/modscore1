namespace Modernization.Domain;

/// <summary>
/// Resultado completo y explicable de un calculo de score.
/// No es logging: es el contrato de API que alimenta la pantalla "como se calculo".
/// Cada numero que la UI muestra debe poder trazarse hasta aqui.
/// </summary>
public sealed record ScoreBreakdown
{
    public required string EaiCode { get; init; }
    public required string ApplicationName { get; init; }

    /// <summary>Version del ruleset con la que se calculo. Los rulesets son inmutables.</summary>
    public required string RulesetVersion { get; init; }

    /// <summary>Suma de los scores ponderados de las metricas con dato.</summary>
    public required decimal Numerator { get; init; }

    /// <summary>Suma de los pesos de las metricas con dato.</summary>
    public required decimal Denominator { get; init; }

    /// <summary>Score normalizado al rango 20-100.</summary>
    public required decimal Score { get; init; }

    public required IReadOnlyList<PillarBreakdown> Pillars { get; init; }

    /// <summary>Todas las metricas evaluadas, incluidas las excluidas y las informativas.</summary>
    public required IReadOnlyList<MetricEvaluation> Metrics { get; init; }

    /// <summary>Override manual aplicado, si lo hay. Siempre auditado.</summary>
    public ScoreOverride? Override { get; init; }

    public required DateTimeOffset CalculatedAt { get; init; }

    /// <summary>Score efectivo: el override si existe, si no el calculado.</summary>
    public decimal EffectiveScore => Override?.Value ?? Score;
}

/// <summary>Agregado de un pilar. Usa exactamente la misma formula que el score global.</summary>
public sealed record PillarBreakdown
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    /// <summary>Peso nominal del pilar (50, 40, 10). Suma de los pesos de sus metricas.</summary>
    public required decimal TotalWeight { get; init; }

    /// <summary>Peso efectivamente disponible: excluye las metricas sin dato.</summary>
    public required decimal AvailableWeight { get; init; }

    public required decimal Numerator { get; init; }

    /// <summary>Score del pilar en rango 20-100, o null si ninguna metrica tiene dato.</summary>
    public required decimal? Score { get; init; }

    public required IReadOnlyList<string> MetricKeys { get; init; }
}

/// <summary>
/// Evaluacion de una metrica. Lleva todo lo necesario para reproducir el calculo
/// a mano: valor crudo, regla aplicada, score mapeado, peso y procedencia.
/// </summary>
public sealed record MetricEvaluation
{
    public required string MetricKey { get; init; }
    public required string Name { get; init; }
    public required string PillarKey { get; init; }

    /// <summary>Valor crudo tal como vino de la fuente.</summary>
    public required object? RawValue { get; init; }

    /// <summary>
    /// Como se lee el valor crudo, como codigo y datos: la interfaz arma la frase
    /// (ADR 0017). P. ej. <c>value.mfa { state: "No (with users)", users: "120" }</c>.
    /// </summary>
    public required Notice Display { get; init; }

    /// <summary>Score mapeado 0-100, o null si no hay dato.</summary>
    public required decimal? MappedScore { get; init; }

    public required decimal Weight { get; init; }

    /// <summary>peso x score / 100. Null cuando no hay dato.</summary>
    public required decimal? WeightedScore { get; init; }

    /// <summary>False cuando la metrica se excluye del numerador y del denominador.</summary>
    public required bool IsIncluded { get; init; }

    /// <summary>Peso 0: se muestra pero no afecta el resultado.</summary>
    public required bool IsInformational { get; init; }

    /// <summary>Por que la metrica quedo fuera del calculo. Null si se incluyo.</summary>
    public required Notice? Exclusion { get; init; }

    /// <summary>Regla concreta que produjo el score, p. ej. "670-1000 (Critical/High) =&gt; 20".</summary>
    public required string RuleApplied { get; init; }

    public required SourceProvenance Provenance { get; init; }
}

/// <summary>
/// De donde salio el dato. Obligatorio en cada metrica: sostiene la pantalla de
/// linaje y hace auditable la migracion de dummy a archivos y a base de datos.
/// </summary>
public sealed record SourceProvenance
{
    /// <summary>Sistema origen: EAI, CMT, RPT, SCA Scan, MTS, OCP, DevOps.</summary>
    public required string System { get; init; }

    /// <summary>Reporte o archivo concreto dentro del sistema.</summary>
    public required string Report { get; init; }

    /// <summary>Campo o columna leida.</summary>
    public required string Field { get; init; }

    /// <summary>Adaptador que sirvio el dato: Dummy, File o Database.</summary>
    public string? Provider { get; init; }

    public DateTimeOffset? ExtractedAt { get; init; }
}

/// <summary>Override manual del score. Requiere justificacion y queda auditado.</summary>
public sealed record ScoreOverride
{
    public required decimal Value { get; init; }
    public required string Reason { get; init; }
    public required string AppliedBy { get; init; }
    public required DateTimeOffset AppliedAt { get; init; }
}
