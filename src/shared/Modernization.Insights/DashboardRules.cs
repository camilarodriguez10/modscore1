using System.Text.Json;

namespace Modernization.Insights;

/// <summary>
/// Las definiciones con que se responden las seis preguntas del portafolio —el
/// dashboard ejecutivo y la pantalla Insights—, como datos versionados.
/// </summary>
/// <remarks>
/// Los valores de fábrica viven en <c>contracts/dashboard-v1.json</c>. Desde el
/// 2026-09-18 viajan además con cada versión de reglas y se editan desde el portal,
/// con el mismo borrador, simulación y publicación que un peso del score (ADR 0025):
/// cada número que se puede cambiar está en <see cref="AnalyticsCatalog"/>, con su
/// rango. Lo que no está en el catálogo —de dónde sale un dato, qué métrica explica
/// qué concepto— depende del código y se cambia desplegando.
/// </remarks>
public sealed record DashboardRules
{
    public const string DefaultVersion = "dashboard-v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<DashboardRules> DefaultRules = new(() => Load(DefaultVersion));

    public required string Version { get; init; }

    public string? Description { get; init; }

    public required CurrencyRules Currency { get; init; }
    public required RiskRules Risk { get; init; }
    public required OpportunityRules Opportunity { get; init; }
    public required CriticalityRules Criticality { get; init; }
    public required CandidateRules Candidates { get; init; }
    public required ComplexityRules Complexity { get; init; }
    public required ConcentrationRules Concentration { get; init; }
    public required ResilienceRules Resilience { get; init; }
    public required IReadOnlyList<ObsolescenceGroup> Obsolescence { get; init; }

    /// <summary>Los parámetros de la pantalla Insights.</summary>
    public InsightsRules Insights { get; init; } = new();

    /// <summary>
    /// Estado y dueño de cada área (<see cref="AnalyticsCatalog.Areas"/>). Un área que no
    /// aparece es provisional y sin dueño.
    /// </summary>
    public IReadOnlyDictionary<string, AnalyticsArea> Areas { get; init; } = new Dictionary<string, AnalyticsArea>();

    public static DashboardRules Default => DefaultRules.Value;

    /// <summary>"provisional" si alguna área lo es; "agreed" cuando el negocio confirmó todas.</summary>
    public string Status => AnalyticsCatalog.Areas.Any(area => AreaOf(area).Status == AnalyticsArea.Provisional)
        ? AnalyticsArea.Provisional
        : AnalyticsArea.Agreed;

    public AnalyticsArea AreaOf(string key)
        => Areas.TryGetValue(key, out var area) ? area : new AnalyticsArea();

    public static DashboardRules Load(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var assembly = typeof(DashboardRules).Assembly;
        var resourceName = version + ".json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new DashboardRulesException(
                $"No existen las definiciones del dashboard '{version}'. Disponibles: " +
                $"{string.Join(", ", assembly.GetManifestResourceNames())}.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    public static DashboardRules Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        DashboardRules? rules;

        try
        {
            rules = JsonSerializer.Deserialize<DashboardRules>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new DashboardRulesException($"Las definiciones del dashboard no son JSON valido: {ex.Message}", ex);
        }

        if (rules is null)
        {
            throw new DashboardRulesException("Las definiciones del dashboard estan vacias.");
        }

        var problems = rules.Problems();

        if (problems.Count > 0)
        {
            throw new DashboardRulesException(
                "Las definiciones del dashboard no son consistentes: " + string.Join(", ", problems) + ".");
        }

        return rules;
    }

    /// <summary>La forma en que se guarda con una version de reglas.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Las reglas que no se cumplen, por su clave (<see cref="AnalyticsCatalog.Constraints"/>
    /// y la de cada parámetro fuera de rango). Vacío si todo es coherente.
    /// </summary>
    /// <remarks>
    /// Un umbral fuera de orden no falla en ningún cálculo: produce niveles
    /// silenciosamente equivocados. Por eso se revisa al cargar y al editar.
    /// </remarks>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();

        foreach (var parameter in AnalyticsCatalog.Parameters)
        {
            if (!parameter.Accepts(parameter.Get(this)))
            {
                problems.Add(parameter.Key);
            }
        }

        foreach (var constraint in AnalyticsCatalog.Constraints)
        {
            if (!constraint.Holds(this))
            {
                problems.Add(constraint.Key);
            }
        }

        if (string.IsNullOrWhiteSpace(Risk.Pillar))
        {
            problems.Add("risk.pillar");
        }

        if (Obsolescence.Count == 0
            || Obsolescence.Any(g => string.IsNullOrWhiteSpace(g.Key) || g.Metrics.Count == 0)
            || Obsolescence.Select(g => g.Key).Distinct(StringComparer.Ordinal).Count() != Obsolescence.Count
            // Una métrica en dos grupos contaría sus puntos dos veces.
            || Obsolescence.SelectMany(g => g.Metrics).Distinct(StringComparer.Ordinal).Count()
                != Obsolescence.Sum(g => g.Metrics.Count))
        {
            problems.Add("obsolescence.groups");
        }

        foreach (var (key, area) in Areas)
        {
            if (!AnalyticsCatalog.Areas.Contains(key, StringComparer.Ordinal) || !area.IsValid)
            {
                problems.Add("areas." + key);
            }
        }

        return problems;
    }
}

/// <summary>Estado de un área de definiciones y quién responde por ella.</summary>
public sealed record AnalyticsArea(string Status = AnalyticsArea.Provisional, string? Owner = null)
{
    public const string Provisional = "provisional";
    public const string Agreed = "agreed";
    public const int MaxOwnerLength = 60;

    public static readonly IReadOnlyList<string> Statuses = [Provisional, Agreed];

    public bool IsValid => Statuses.Contains(Status, StringComparer.Ordinal) && IsOwner(Owner);

    /// <summary>Una línea corta, sin caracteres de control. Vacío es "sin dueño".</summary>
    public static bool IsOwner(string? owner)
        => owner is null
            || (owner.Length <= MaxOwnerLength
                && owner.Trim().Length == owner.Length
                && !owner.Any(c => char.IsControl(c)
                    || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format));
}

/// <summary>
/// Currency Score: el score que entrega CMT, el <c>Raw_Tech_Score</c> del Currency Model
/// Detail Report, de 0 a 100. Se calcula por componente a partir de su fecha de fin de vida.
/// </summary>
public sealed record CurrencyRules
{
    public string? Note { get; init; }

    /// <summary>Por debajo de esto, la aplicación cuenta como plataforma legada.</summary>
    public decimal LegacyBelow { get; init; } = 60m;
}

/// <summary>Risk Score de 0 a 100, alto es peor, y sus niveles.</summary>
public sealed record RiskRules
{
    public string? Note { get; init; }

    /// <summary>Pilar del modelo que se invierte para obtener el riesgo.</summary>
    public string Pillar { get; init; } = "risk";

    public decimal ModerateFrom { get; init; } = 40m;
    public decimal HighFrom { get; init; } = 60m;
    public decimal CriticalFrom { get; init; } = 80m;

    /// <summary>Cuántas aplicaciones lista el panel como las de más riesgo.</summary>
    public int HotspotCount { get; init; } = 5;
}

/// <summary>Niveles de oportunidad según los puntos que suma el roadmap propio.</summary>
public sealed record OpportunityRules
{
    public string? Note { get; init; }
    public decimal MediumFrom { get; init; } = 5m;
    public decimal HighFrom { get; init; } = 10m;
    public decimal VeryHighFrom { get; init; } = 20m;
}

/// <summary>Niveles de criticidad de negocio.</summary>
public sealed record CriticalityRules
{
    public string? Note { get; init; }

    /// <summary>Capacidades a partir de las cuales una aplicación es transversal.</summary>
    public int BroadCapabilities { get; init; } = 3;
}

/// <summary>Qué parte de la matriz cuenta como candidata a modernizarse.</summary>
public sealed record CandidateRules
{
    public string? Note { get; init; }
    public int MinCriticality { get; init; } = 2;
    public int MinOpportunity { get; init; } = 2;

    /// <summary>Cuántas aplicaciones lista el top de prioridades.</summary>
    public int TopCount { get; init; } = 10;
}

/// <summary>Umbrales de las concentraciones de complejidad.</summary>
public sealed record ComplexityRules
{
    public string? Note { get; init; }
    public int DuplicateThreshold { get; init; } = 3;
    public int LanguageThreshold { get; init; } = 3;

    /// <summary>
    /// Deuda técnica alta: el Tech Debt Score mapeado queda en este puntaje o por debajo.
    /// Con las bandas del BRD, 20 es la peor.
    /// </summary>
    public decimal TechDebtMappedAtMost { get; init; } = 20m;
}

/// <summary>Índice de concentración del riesgo y sus niveles.</summary>
public sealed record ConcentrationRules
{
    public string? Note { get; init; }

    /// <summary>Qué parte de las celdas —las de más riesgo— se mira.</summary>
    public decimal TopShare { get; init; } = 0.2m;

    public decimal ModerateFrom { get; init; } = 0.4m;
    public decimal HighFrom { get; init; } = 0.6m;
    public decimal CriticalFrom { get; init; } = 0.8m;
}

/// <summary>Cómo se lee la resiliencia con los datos de hoy.</summary>
public sealed record ResilienceRules
{
    public string? Note { get; init; }
    public int RecoveryModerateFromHours { get; init; } = 24;
    public int RecoveryHighFromHours { get; init; } = 48;
    public decimal OperationalModerateBelow { get; init; } = 80m;
    public decimal OperationalHighBelow { get; init; } = 60m;
    public decimal OperationalCriticalBelow { get; init; } = 40m;
}

/// <summary>
/// Un concepto de obsolescencia: las métricas cuyos puntos perdidos explica. Los que
/// son de ciclo de vida o vigencia tecnológica suman el porcentaje del titular.
/// </summary>
public sealed record ObsolescenceGroup
{
    public required string Key { get; init; }
    public bool Currency { get; init; }
    public required IReadOnlyList<string> Metrics { get; init; }
}

/// <summary>
/// Los parámetros de la pantalla Insights (ADR 0023): el peso de negocio de la lista de
/// prioridades, el índice de riesgo por grupo y el corte del score operacional.
/// </summary>
public sealed record InsightsRules
{
    public BusinessWeightRules BusinessWeight { get; init; } = new();
    public RiskIndexRules RiskIndex { get; init; } = new();

    /// <summary>Score operacional por debajo de esto cuenta como hueco de resiliencia.</summary>
    public decimal LowOperationalBelow { get; init; } = 60m;

    /// <summary>
    /// A partir de cuántas aplicaciones una coincidencia es concentración, cuando la URL
    /// no lo dice. La pantalla ofrece de 2 a 5.
    /// </summary>
    public int DuplicateDefault { get; init; } = 3;
}

/// <summary>Peso de negocio: 1 más lo que suma cada atributo que la aplicación cumple.</summary>
public sealed record BusinessWeightRules
{
    public decimal Critical { get; init; } = 1m;
    public decimal SignificantUsers { get; init; } = 0.5m;
    public decimal BroadCapabilities { get; init; } = 0.25m;

    /// <summary>Capacidades a partir de las cuales suma <see cref="BroadCapabilities"/>.</summary>
    public int CapabilitiesFrom { get; init; } = 3;
}

/// <summary>Índice de riesgo por grupo: cada conteo por su peso.</summary>
public sealed record RiskIndexRules
{
    public decimal CriticalFlaw { get; init; } = 3m;
    public decimal HighFlaw { get; init; } = 1m;
    public decimal WeakKenna { get; init; } = 5m;

    /// <summary>
    /// Kenna a partir de esto cuenta como gestión de vulnerabilidades débil. Kenna va de 0
    /// a 1000 y más alto es peor: 670 es donde empieza la banda Critical/High del BRD.
    /// </summary>
    public int WeakKennaFrom { get; init; } = 670;

    public decimal ObsoleteComponent { get; init; } = 2m;
    public decimal ExposedCritical { get; init; } = 10m;
}

/// <summary>Las definiciones del dashboard no se pudieron cargar o no son consistentes.</summary>
public sealed class DashboardRulesException : Exception
{
    public DashboardRulesException()
    {
    }

    public DashboardRulesException(string message)
        : base(message)
    {
    }

    public DashboardRulesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
