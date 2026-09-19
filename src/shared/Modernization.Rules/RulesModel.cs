using System.Text.Json;
using System.Text.Json.Serialization;
using Modernization.Domain;
using Modernization.Domain.Rules;
using Modernization.Insights;
using Modernization.Roadmap;
using Modernization.Scoring.Engine;
using Modernization.WhatIf;

namespace Modernization.Rules;

/// <summary>Quien publico una version de reglas, cuando, por que y con que efecto.</summary>
public sealed record RulesVersionInfo
{
    public required string Id { get; init; }

    public string? BasedOn { get; init; }

    /// <summary>Null en las reglas de fabrica, que nadie publico.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    public required string PublishedBy { get; init; }

    public required string Reason { get; init; }

    public required IReadOnlyList<Notice> Changes { get; init; }

    public RulesImpactSummary? Impact { get; init; }

    /// <summary>True para el ruleset que viaja embebido en el ensamblado.</summary>
    public bool IsBuiltIn { get; init; }
}

/// <summary>
/// Un juego completo de reglas y los motores construidos con el.
/// </summary>
/// <remarks>
/// Ruleset, palancas, roadmap y definiciones de las seis preguntas se versionan
/// juntos porque dependen entre si: los objetivos de las palancas salen de las bandas
/// del ruleset, la proyeccion de cada roadmap sale de sus palancas, y el dashboard lee
/// el pilar de riesgo, las metricas y la base de usuarios del resto (ADR 0025).
/// Publicar solo una parte podria dejar un roadmap proyectando con una palanca que el
/// laboratorio ya no ofrece.
/// <para>
/// Es inmutable. Cambiar las reglas es construir otro modelo y reemplazarlo de una
/// vez: una peticion en curso termina con las reglas con que empezo.
/// </para>
/// </remarks>
public sealed class RulesModel
{
    /// <summary>Quien figura como autor de las reglas de fabrica.</summary>
    public const string BuiltInPublisher = "system";

    private static readonly Lazy<RulesModel> BuiltInModel = new(() => new RulesModel(
        new RulesVersionInfo
        {
            Id = RulesetCatalog.DefaultVersion,
            PublishedBy = BuiltInPublisher,
            Reason = string.Empty,
            Changes = [],
            IsBuiltIn = true,
        },
        RulesetCatalog.Default,
        LeverSettings.Default,
        RoadmapCatalog.Default,
        DashboardRules.Default));

    /// <summary>Con las definiciones de fabrica de las seis preguntas.</summary>
    /// <exception cref="RulesException">Las partes no son coherentes entre si.</exception>
    public RulesModel(RulesVersionInfo info, Ruleset ruleset, LeverSettings levers, RoadmapCatalog roadmap)
        : this(info, ruleset, levers, roadmap, DashboardRules.Default)
    {
    }

    /// <exception cref="RulesException">Las partes no son coherentes entre si.</exception>
    public RulesModel(
        RulesVersionInfo info,
        Ruleset ruleset,
        LeverSettings levers,
        RoadmapCatalog roadmap,
        DashboardRules analytics)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(levers);
        ArgumentNullException.ThrowIfNull(roadmap);
        ArgumentNullException.ThrowIfNull(analytics);

        try
        {
            ruleset.Validate();
            roadmap.Validate();

            Engine = new ScoringEngine(ruleset);
            LeverCatalog = new LeverCatalog(ruleset, levers);
        }
        catch (Exception ex) when (ex is RulesetException or RoadmapException or ArgumentException)
        {
            throw new RulesException("Las reglas no son validas: " + ex.Message, ex);
        }

        // Un roadmap que entrega una palanca apagada proyectaria con algo que el
        // laboratorio ya no ofrece: el plan futuro y el what-if dejarian de hablar
        // del mismo catalogo.
        foreach (var definition in roadmap.Roadmaps)
        {
            foreach (var key in definition.Levers)
            {
                if (LeverCatalog.Find(key) is null)
                {
                    throw new RulesException(
                        $"El roadmap '{definition.Key}' entrega la palanca '{key}', que no existe o esta apagada.");
                }
            }
        }

        // Las definiciones nombran partes del ruleset: el pilar que se invierte para el
        // riesgo y las metricas que explican la obsolescencia. Una clave que no existe no
        // falla en ningun calculo, deja un panel en cero sin decir por que.
        if (analytics.Problems() is { Count: > 0 } problems)
        {
            throw new RulesException("Las definiciones de las seis preguntas no son coherentes: " + string.Join(", ", problems));
        }

        if (!ruleset.Pillars.Any(p => string.Equals(p.Key, analytics.Risk.Pillar, StringComparison.Ordinal)))
        {
            throw new RulesException($"El pilar de riesgo '{analytics.Risk.Pillar}' no existe en el ruleset.");
        }

        foreach (var metric in analytics.Obsolescence.SelectMany(g => g.Metrics))
        {
            if (!ruleset.Metrics.Any(m => string.Equals(m.Key, metric, StringComparison.Ordinal)))
            {
                throw new RulesException($"La metrica '{metric}' de obsolescencia no existe en el ruleset.");
            }
        }

        Info = info;
        Ruleset = ruleset;
        Levers = levers;
        Roadmap = roadmap;
        Analytics = analytics;
        WhatIf = new WhatIfEngine(Engine, LeverCatalog);
        Planner = new RoadmapPlanner(roadmap, WhatIf, Engine);
        Analyzer = new PortfolioRoadmapAnalyzer(roadmap, Planner);
    }

    /// <summary>Las reglas de fabrica: ruleset v1.3, todas las palancas y el catalogo MAPS v1.</summary>
    public static RulesModel BuiltIn => BuiltInModel.Value;

    public RulesVersionInfo Info { get; }

    public string Id => Info.Id;

    public Ruleset Ruleset { get; }

    public LeverSettings Levers { get; }

    public RoadmapCatalog Roadmap { get; }

    /// <summary>Las definiciones con que se responden las seis preguntas.</summary>
    public DashboardRules Analytics { get; }

    public ScoringEngine Engine { get; }

    public LeverCatalog LeverCatalog { get; }

    public WhatIfEngine WhatIf { get; }

    public RoadmapPlanner Planner { get; }

    public PortfolioRoadmapAnalyzer Analyzer { get; }

    /// <summary>
    /// El mismo contenido con otra identidad. Al publicar, el ruleset y el catalogo
    /// llevan el identificador de la version: es lo que cada score registra.
    /// </summary>
    public RulesModel WithInfo(RulesVersionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new RulesModel(info, Ruleset with { Version = info.Id }, Levers, Roadmap with { Version = info.Id }, Analytics);
    }

    /// <summary>Reconstruye y valida una version guardada.</summary>
    /// <exception cref="RulesException">El documento guardado no es valido.</exception>
    public static RulesModel FromStored(StoredRulesVersion stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        try
        {
            var ruleset = Ruleset.FromJson(stored.Ruleset);
            var roadmap = RoadmapCatalog.FromJson(stored.Roadmap);
            var levers = JsonSerializer.Deserialize<LeverSettings>(stored.Levers, RulesJson.Options)
                ?? throw new RulesException($"La version '{stored.Id}' no trae ajustes de palancas.");

            var changes = string.IsNullOrWhiteSpace(stored.Changes)
                ? []
                : JsonSerializer.Deserialize<List<Notice>>(stored.Changes, RulesJson.Options) ?? [];

            var impact = string.IsNullOrWhiteSpace(stored.Impact)
                ? null
                : JsonSerializer.Deserialize<RulesImpactSummary>(stored.Impact, RulesJson.Options);

            // Las versiones anteriores a que las definiciones viajaran con las reglas no
            // las traen: usan las de fabrica.
            var analytics = string.IsNullOrWhiteSpace(stored.Analytics)
                ? DashboardRules.Default
                : DashboardRules.Parse(stored.Analytics);

            return new RulesModel(
                new RulesVersionInfo
                {
                    Id = stored.Id,
                    BasedOn = stored.BasedOn,
                    PublishedAt = stored.PublishedAt,
                    PublishedBy = stored.PublishedBy,
                    Reason = stored.Reason,
                    Changes = changes,
                    Impact = impact,
                },
                ruleset,
                levers,
                roadmap,
                analytics);
        }
        catch (Exception ex) when (ex is JsonException or RulesetException or RoadmapException or ArgumentException
            or DashboardRulesException)
        {
            throw new RulesException($"La version de reglas '{stored.Id}' guardada no es valida: {ex.Message}", ex);
        }
    }

    /// <summary>La forma en que se guarda. Solo tiene sentido para una version publicada.</summary>
    public StoredRulesVersion ToStored()
    {
        if (Info.PublishedAt is not { } publishedAt)
        {
            throw new InvalidOperationException("Las reglas de fabrica no se guardan: viajan en el ensamblado.");
        }

        return new StoredRulesVersion
        {
            Id = Info.Id,
            BasedOn = Info.BasedOn,
            PublishedAt = publishedAt,
            PublishedBy = Info.PublishedBy,
            Reason = Info.Reason,
            Ruleset = Ruleset.ToJson(),
            Levers = JsonSerializer.Serialize(Levers, RulesJson.Options),
            Roadmap = Roadmap.ToJson(),
            Changes = JsonSerializer.Serialize(Info.Changes, RulesJson.Options),
            Impact = Info.Impact is null ? string.Empty : JsonSerializer.Serialize(Info.Impact, RulesJson.Options),
            Analytics = Analytics.ToJson(),
        };
    }
}

/// <summary>Reglas incoherentes o una version guardada que no se puede leer.</summary>
public sealed class RulesException : Exception
{
    public RulesException() { }

    public RulesException(string message) : base(message) { }

    public RulesException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Opciones de JSON de las partes de una version que no tienen formato propio.</summary>
internal static class RulesJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
