using System.Text.Json;

namespace Modernization.Insights;

/// <summary>
/// Las definiciones del dashboard ejecutivo, como datos versionados.
/// </summary>
/// <remarks>
/// La propuesta del dashboard deja varias definiciones en TBD o las remite a
/// «conceptos disponibles» de Currency y Stability que todavía no existen por escrito.
/// Cada una vive aquí con un default y una nota que dice de dónde sale, para que la
/// discusión sea sobre el criterio y no sobre dónde está escondido
/// (<c>contracts/dashboard-v1.json</c>, ADR 0024).
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

    /// <summary>"provisional" mientras el negocio no confirme las definiciones.</summary>
    public string Status { get; init; } = "provisional";

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

    public static DashboardRules Default => DefaultRules.Value;

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

        rules.Validate();
        return rules;
    }

    /// <summary>
    /// Un umbral fuera de orden no falla en ningun calculo: produce niveles
    /// silenciosamente equivocados. Por eso se valida al cargar.
    /// </summary>
    private void Validate()
    {
        var problems = new List<string>();

        void Check(bool ok, string problem)
        {
            if (!ok)
            {
                problems.Add(problem);
            }
        }

        Check(Currency.LegacyBelow is > 0m and <= 100m, "currency.legacyBelow tiene que estar entre 0 y 100.");
        Check(!string.IsNullOrWhiteSpace(Risk.Pillar), "risk.pillar no puede estar vacio.");
        Check(
            Risk.ModerateFrom > 0m && Risk.ModerateFrom < Risk.HighFrom && Risk.HighFrom < Risk.CriticalFrom && Risk.CriticalFrom <= 100m,
            "Los niveles de riesgo tienen que ir en orden: 0 < moderado < alto < critico <= 100.");
        Check(
            Opportunity.MediumFrom > 0m && Opportunity.MediumFrom < Opportunity.HighFrom && Opportunity.HighFrom < Opportunity.VeryHighFrom,
            "Los niveles de oportunidad tienen que ir en orden: 0 < media < alta < muy alta.");
        Check(Criticality.BroadCapabilities >= 1, "criticality.broadCapabilities tiene que ser 1 o mas.");
        Check(Candidates.MinCriticality is >= 0 and <= 3, "candidates.minCriticality va de 0 a 3.");
        Check(Candidates.MinOpportunity is >= 0 and <= 3, "candidates.minOpportunity va de 0 a 3.");
        Check(Complexity.DuplicateThreshold >= 2, "complexity.duplicateThreshold tiene que ser 2 o mas.");
        Check(Complexity.LanguageThreshold >= 2, "complexity.languageThreshold tiene que ser 2 o mas.");
        Check(Concentration.TopShare is > 0m and <= 1m, "concentration.topShare va de 0 a 1.");
        Check(
            Concentration.ModerateFrom > 0m && Concentration.ModerateFrom < Concentration.HighFrom
                && Concentration.HighFrom < Concentration.CriticalFrom && Concentration.CriticalFrom <= 1m,
            "Los niveles de concentracion tienen que ir en orden entre 0 y 1.");
        Check(
            Resilience.RecoveryModerateFromHours >= 0 && Resilience.RecoveryModerateFromHours < Resilience.RecoveryHighFromHours,
            "Las horas de recuperacion tienen que ir en orden: moderado < alto.");
        Check(
            Resilience.OperationalModerateBelow > Resilience.OperationalHighBelow
                && Resilience.OperationalHighBelow > Resilience.OperationalCriticalBelow
                && Resilience.OperationalCriticalBelow > 0m && Resilience.OperationalModerateBelow <= 100m,
            "Los cortes del score operacional tienen que ir de mayor a menor: moderado > alto > critico.");
        Check(Obsolescence.Count > 0, "obsolescence necesita al menos un grupo.");
        Check(
            Obsolescence.Select(g => g.Key).Distinct(StringComparer.Ordinal).Count() == Obsolescence.Count,
            "Las claves de obsolescence no pueden repetirse.");
        Check(
            Obsolescence.All(g => !string.IsNullOrWhiteSpace(g.Key) && g.Metrics.Count > 0),
            "Cada grupo de obsolescence necesita clave y al menos una metrica.");
        Check(
            Obsolescence.SelectMany(g => g.Metrics).Distinct(StringComparer.Ordinal).Count()
                == Obsolescence.Sum(g => g.Metrics.Count),
            "Una metrica no puede estar en dos grupos de obsolescence: sus puntos se contarian dos veces.");

        if (problems.Count > 0)
        {
            throw new DashboardRulesException(
                "Las definiciones del dashboard no son consistentes: " + string.Join(" ", problems));
        }
    }
}

/// <summary>Currency Score: la parte de los componentes de CMT que es moderna.</summary>
public sealed record CurrencyRules
{
    public string? Note { get; init; }

    /// <summary>Por debajo de esto, la aplicacion cuenta como plataforma legada.</summary>
    public decimal LegacyBelow { get; init; } = 50m;
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
}

/// <summary>Niveles de oportunidad segun los puntos que suma el roadmap propio.</summary>
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

    /// <summary>Capacidades a partir de las cuales una aplicacion es transversal.</summary>
    public int BroadCapabilities { get; init; } = 3;
}

/// <summary>Que parte de la matriz cuenta como candidata a modernizarse.</summary>
public sealed record CandidateRules
{
    public string? Note { get; init; }
    public int MinCriticality { get; init; } = 2;
    public int MinOpportunity { get; init; } = 2;
}

/// <summary>Umbrales de las concentraciones de complejidad.</summary>
public sealed record ComplexityRules
{
    public string? Note { get; init; }
    public int DuplicateThreshold { get; init; } = 3;
    public int LanguageThreshold { get; init; } = 3;
}

/// <summary>Indice de concentracion del riesgo y sus niveles.</summary>
public sealed record ConcentrationRules
{
    public string? Note { get; init; }

    /// <summary>Que parte de las celdas —las de mas riesgo— se mira.</summary>
    public decimal TopShare { get; init; } = 0.2m;

    public decimal ModerateFrom { get; init; } = 0.4m;
    public decimal HighFrom { get; init; } = 0.6m;
    public decimal CriticalFrom { get; init; } = 0.8m;
}

/// <summary>Como se lee la resiliencia con los datos de hoy.</summary>
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
/// Un concepto de obsolescencia: las metricas cuyos puntos perdidos explica. Los que
/// son de ciclo de vida o vigencia tecnologica suman el porcentaje del titular.
/// </summary>
public sealed record ObsolescenceGroup
{
    public required string Key { get; init; }
    public bool Currency { get; init; }
    public required IReadOnlyList<string> Metrics { get; init; }
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
