using Modernization.Domain;

namespace Modernization.Insights;

/// <summary>
/// Las seis preguntas que el portafolio tiene que poder responder, con el dato que
/// las sostiene.
/// </summary>
/// <remarks>
/// Todo sale de lo que el motor ya calculó y de las fuentes: ningún número nace
/// aquí. Cada elemento lleva su evidencia como aviso con código, para que la
/// interfaz redacte la frase ([ADR 0017](../../../docs/adr/0017-el-backend-no-redacta-frases.md)).
/// </remarks>
public sealed record PortfolioInsights
{
    /// <summary>¿Qué tan sano está el portafolio?</summary>
    public required PortfolioHealth Health { get; init; }

    /// <summary>¿Qué aplicaciones modernizar primero? Impacto contra esfuerzo.</summary>
    public required IReadOnlyList<ModernizationCandidate> Priorities { get; init; }

    /// <summary>¿Dónde reducir complejidad? Concentraciones que huelen a duplicación.</summary>
    public required IReadOnlyList<ComplexityCluster> Complexity { get; init; }

    /// <summary>¿Dónde se concentra el riesgo tecnológico?</summary>
    public required IReadOnlyList<RiskConcentration> Risk { get; init; }

    /// <summary>¿Qué está causando la obsolescencia? Puntos perdidos por métrica.</summary>
    public required IReadOnlyList<ObsolescenceDriver> Obsolescence { get; init; }

    /// <summary>¿Dónde están los huecos de resiliencia?</summary>
    public required ResilienceGaps Resilience { get; init; }

    /// <summary>Los umbrales con que se calculó, para que la pantalla los muestre.</summary>
    public required InsightsOptions Options { get; init; }
}

/// <summary>
/// Umbrales del análisis, ajustables desde la consulta.
/// </summary>
/// <remarks>
/// <c>HealthyScore</c> y <c>SignificantUserBase</c> salen del catálogo del roadmap, que se
/// edita desde el portal: cambiar ahí qué es "sano" cambia también este análisis.
/// <c>DuplicateThreshold</c> es a partir de cuántas aplicaciones una coincidencia ya es
/// una concentración, y <c>TopCount</c> cuántos elementos devuelve cada lista.
/// </remarks>
public sealed record InsightsOptions(
    decimal HealthyScore = 70m,
    int SignificantUserBase = 1000,
    int DuplicateThreshold = 3,
    int TopCount = 10)
{
    public const int MaxTopCount = 50;

    public InsightsOptions Sane() => this with
    {
        DuplicateThreshold = Math.Clamp(DuplicateThreshold, 2, 50),
        TopCount = Math.Clamp(TopCount, 1, MaxTopCount),
    };
}

// ----------------------------------------------------------------- ¿qué tan sano?

/// <summary>
/// La salud del portafolio en cinco números.
/// </summary>
/// <remarks>
/// El promedio es simple —cada aplicación pesa igual— y va junto a la mediana, que
/// delata cuando una cola mueve el promedio. <c>DataCoverage</c> es la parte del peso
/// del modelo que tuvo dato: un score alto con cobertura baja dice poco.
/// <c>CriticalBelowHealthy</c> cuenta las aplicaciones críticas bajo el umbral sano,
/// que es donde primero hay que mirar.
/// </remarks>
public sealed record PortfolioHealth(
    int Applications,
    decimal AverageScore,
    decimal MedianScore,
    decimal DataCoverage,
    int CriticalBelowHealthy,
    IReadOnlyList<BandShare> Bands);

/// <summary>Cuántas aplicaciones caen en un tramo de score, y qué parte del total son.</summary>
public sealed record BandShare(string Code, string Name, int Applications, decimal Share);

// ------------------------------------------------------------- ¿qué modernizar?

/// <summary>
/// Una aplicación candidata a modernizarse, con el impacto y el esfuerzo que el dato
/// sostiene.
/// </summary>
/// <remarks>
/// <c>Uplift</c> son los puntos que sube el score si se ejecuta su roadmap, medidos con
/// el motor de what-if. <c>Weeks</c> son las semanas de los hitos de ese roadmap: es el
/// único esfuerzo que hay en los datos de hoy. <c>BusinessWeight</c> pondera criticidad,
/// usuarios y capacidades; <c>Impact</c> es el uplift por ese peso, y
/// <c>ValuePerWeek</c> —impacto entre semanas— es el orden de la lista.
/// </remarks>
public sealed record ModernizationCandidate(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    decimal CurrentScore,
    decimal? ProjectedScore,
    decimal Uplift,
    int Weeks,
    decimal BusinessWeight,
    decimal Impact,
    decimal ValuePerWeek,
    RoadmapType? Roadmap,
    IReadOnlyList<Notice> Evidence);

// --------------------------------------------------------- ¿dónde hay complejidad?

/// <summary>Qué tipo de concentración se encontró.</summary>
public enum ComplexityKind
{
    /// <summary>Varias aplicaciones para la misma capacidad en el mismo país.</summary>
    DuplicateCapabilityInCountry,

    /// <summary>La misma capacidad resuelta con aplicaciones distintas en varios países.</summary>
    CapabilityAcrossCountries,

    /// <summary>Aplicaciones marcadas para eliminar que siguen activas.</summary>
    DecommissionBacklog,

    /// <summary>Muchos lenguajes distintos para la misma capacidad.</summary>
    LanguageSpread,
}

/// <summary>
/// Una concentración que vale la pena mirar: qué es, cuántas aplicaciones y quiénes.
/// </summary>
/// <remarks>
/// No dice "esto sobra": dice dónde hay varias piezas resolviendo lo mismo, con su
/// evidencia, para que alguien que conoce el negocio lo confirme o lo descarte.
/// </remarks>
public sealed record ComplexityCluster(
    ComplexityKind Kind,
    string Dimension,
    int Applications,
    int Users,
    decimal AverageScore,
    IReadOnlyList<string> EaiCodes,
    Notice Evidence);

// ------------------------------------------------------- ¿dónde está el riesgo?

/// <summary>
/// Riesgo tecnológico acumulado en un grupo: LOB, país, CIO o capacidad.
/// </summary>
/// <remarks>
/// <c>RiskIndex</c> suma las señales del grupo con pesos distintos —un flaw crítico pesa
/// más que uno alto, y una aplicación crítica expuesta a internet pesa como varios—, y
/// sirve para ordenar dentro de este portafolio, no para comparar con otro.
/// </remarks>
public sealed record RiskConcentration(
    string Grouping,
    string Dimension,
    int Applications,
    int CriticalFlaws,
    int HighFlaws,
    int WeakVulnerabilityManagement,
    int ObsoleteComponents,
    int InternetFacingCritical,
    decimal AverageScore,
    decimal RiskIndex);

// -------------------------------------------------- ¿qué causa la obsolescencia?

/// <summary>
/// Cuánto score pierde el portafolio por cada métrica.
/// </summary>
/// <remarks>
/// <c>LostPoints</c> es el peso por la parte del puntaje que no se obtuvo, sumado en todo
/// el portafolio: responde "qué nos está costando puntos" con la aritmética del modelo.
/// <c>WeightWithoutData</c> es el peso que quedó fuera del cálculo por falta de dato, que
/// no es obsolescencia sino ceguera, y por eso va en su propia columna.
/// </remarks>
public sealed record ObsolescenceDriver(
    string MetricKey,
    string Name,
    string PillarKey,
    decimal LostPoints,
    decimal WeightWithoutData,
    int ApplicationsAffected,
    decimal Share);

// ------------------------------------------------- ¿dónde falta resiliencia?

/// <summary>
/// Huecos de continuidad: recuperación ante desastres, monitoreo, score operacional y
/// objetivos de recuperación declarados.
/// </summary>
/// <remarks>
/// Los tres primeros contadores miran solo aplicaciones críticas, que es donde un hueco
/// duele. La lista trae las aplicaciones con huecos, las críticas primero.
/// </remarks>
public sealed record ResilienceGaps(
    int CriticalWithoutDisasterRecovery,
    int CriticalWithoutMonitoring,
    int WithLowOperationalScore,
    int WithoutRecoveryObjectives,
    IReadOnlyList<ResilienceGapApplication> Applications);

/// <summary>Una aplicación con huecos de resiliencia, con el detalle de cuáles.</summary>
public sealed record ResilienceGapApplication(
    string EaiCode,
    string ApplicationName,
    string? Lob,
    string? Country,
    bool Critical,
    decimal Score,
    string? RecoveryTimeObjective,
    string? RecoveryPointObjective,
    IReadOnlyList<Notice> Gaps);
