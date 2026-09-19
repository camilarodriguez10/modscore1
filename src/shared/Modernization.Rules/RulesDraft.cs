namespace Modernization.Rules;

/// <summary>
/// Un borrador de reglas: solo lo que se puede cambiar desde el portal.
/// </summary>
/// <remarks>
/// El borrador no es un ruleset. No se puede agregar una metrica, cambiar de donde
/// lee su dato, moverla de pilar ni tocar la normalizacion: eso depende de codigo
/// —los extractores, la escala 20-100 que usan los tramos y el what-if— y cambiarlo
/// sin desplegar produciria scores silenciosamente incorrectos. Lo que si se puede
/// es lo que es dato de verdad: pesos, bandas, casos, compuestos, que palancas se
/// ofrecen y los parametros del roadmap (ADR 0018).
/// <para>
/// Una lista nula significa "sin cambios" en esa parte. Un elemento que no aparece
/// en su lista conserva lo que tenia.
/// </para>
/// </remarks>
public sealed record RulesDraft(
    IReadOnlyList<MetricDraft>? Metrics = null,
    IReadOnlyList<LeverDraft>? Levers = null,
    RoadmapDraft? Roadmap = null,
    AnalyticsDraft? Analytics = null);

/// <summary>
/// Una metrica editada. Bandas, casos y compuestos solo se aceptan en la metrica
/// cuyo tipo de mapeo los usa.
/// </summary>
public sealed record MetricDraft(
    string Key,
    decimal Weight,
    IReadOnlyList<BandDraft>? Bands = null,
    IReadOnlyDictionary<string, decimal>? Cases = null,
    IReadOnlyDictionary<string, decimal>? CategoryScores = null);

public sealed record BandDraft(decimal Min, decimal Max, decimal Score, string Label);

public sealed record LeverDraft(string Key, bool Enabled, int? DefaultParameter = null);

public sealed record RoadmapDraft(
    decimal HealthyScore,
    int SignificantUserBase,
    IReadOnlyList<RoadmapPathDraft>? Roadmaps = null);

/// <summary>Un roadmap editado: las palancas que entrega y la duracion de cada hito, en orden.</summary>
public sealed record RoadmapPathDraft(string Key, IReadOnlyList<string> Levers, IReadOnlyList<int> MilestoneWeeks);

/// <summary>
/// Las definiciones de las seis preguntas editadas: valores por clave del catalogo y el
/// estado y dueno de cada area. Lo que no aparece conserva lo que tenia.
/// </summary>
public sealed record AnalyticsDraft(
    IReadOnlyDictionary<string, decimal>? Values = null,
    IReadOnlyList<AnalyticsAreaDraft>? Areas = null);

/// <summary>Un area editada: "provisional" o "agreed", y quien responde por ella.</summary>
public sealed record AnalyticsAreaDraft(string Key, string Status, string? Owner = null);

/// <summary>
/// Topes de un borrador. Autenticado —y hasta administrador— no significa
/// inofensivo: un borrador con un millon de bandas no es una regla, es un ataque.
/// </summary>
public static class RulesLimits
{
    /// <summary>Cuerpo maximo de una peticion de simulacion o publicacion.</summary>
    public const long MaxRequestBytes = 128 * 1024;

    public const int MaxMetrics = 64;
    public const int MaxBands = 10;
    public const int MaxLevers = 64;
    public const int MaxRoadmaps = 16;
    public const int MaxLeversPerRoadmap = 32;
    public const int MaxBandLabelLength = 40;
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 280;
    public const int MaxWeeks = 104;
    public const int MaxUserBase = 10_000_000;
    public const decimal MaxBandBound = 1_000_000m;

    /// <summary>
    /// Los pesos suman 100. El motor normaliza por el peso disponible, asi que otro
    /// total calcularia igual; pero todo el sitio lee "60 de 100", y un total distinto
    /// haria mentir a cada una de esas pantallas.
    /// </summary>
    public const decimal TotalWeight = 100m;

    /// <summary>Cuantas versiones anteriores se listan.</summary>
    public const int MaxListedVersions = 50;

    /// <summary>Valores de definiciones en un borrador. El catalogo tiene 37.</summary>
    public const int MaxAnalyticsValues = 64;
}
