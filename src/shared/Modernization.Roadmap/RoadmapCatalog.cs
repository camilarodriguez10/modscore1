using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modernization.Domain;

namespace Modernization.Roadmap;

/// <summary>
/// Modelo MAPS como datos. Igual que el ruleset de scoring, los siete roadmaps,
/// sus hitos y las reglas de coherencia viven en un contrato JSON versionado y no
/// en codigo: cambiar la duracion de un hito o agregar una regla no es un deploy.
/// </summary>
public sealed record RoadmapCatalog
{
    public required string Version { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<DispositionDefinition> Dispositions { get; init; }
    public required IReadOnlyList<RoadmapDefinition> Roadmaps { get; init; }
    public required IReadOnlyList<CoherenceRuleDefinition> CoherenceRules { get; init; }

    /// <summary>Umbrales de las reglas de coherencia. Datos, no constantes.</summary>
    public required RoadmapParameters Parameters { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<RoadmapCatalog> DefaultCatalog = new(() => Load("v1"));

    public static RoadmapCatalog Default => DefaultCatalog.Value;

    public static RoadmapCatalog Load(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var resourceName = $"roadmap-{version}.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new RoadmapException(
                $"No existe el catalogo de roadmap '{version}'. Disponibles: " +
                $"{string.Join(", ", assembly.GetManifestResourceNames())}.");

        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    public static RoadmapCatalog FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        RoadmapCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<RoadmapCatalog>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new RoadmapException($"El catalogo de roadmap no es JSON valido: {ex.Message}", ex);
        }

        if (catalog is null)
        {
            throw new RoadmapException("El catalogo de roadmap esta vacio.");
        }

        catalog.Validate();
        return catalog;
    }

    /// <summary>
    /// El catalogo como JSON, con el mismo formato que lee <see cref="FromJson"/>. Es
    /// como se guarda una version publicada desde el portal (Fase 12).
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Un catalogo incompleto produciria planes futuros silenciosamente
    /// equivocados, asi que se valida al cargarlo.
    /// </summary>
    public void Validate()
    {
        var expected = Enum.GetValues<RoadmapType>();

        if (Roadmaps.Count != expected.Length)
        {
            throw new RoadmapException(
                $"MAPS define {expected.Length} roadmaps y el catalogo declara {Roadmaps.Count}.");
        }

        foreach (var roadmap in expected)
        {
            var declared = Roadmaps.SingleOrDefault(r => r.Key == roadmap)
                ?? throw new RoadmapException($"Falta el roadmap '{roadmap}' en el catalogo.");

            if (!Dispositions.Any(d => d.Key == declared.Disposition))
            {
                throw new RoadmapException(
                    $"El roadmap '{roadmap}' referencia la disposition inexistente '{declared.Disposition}'.");
            }

            if (declared.Milestones.Count == 0)
            {
                throw new RoadmapException($"El roadmap '{roadmap}' no declara hitos.");
            }
        }

        if (CoherenceRules.Count == 0)
        {
            throw new RoadmapException("El catalogo debe declarar al menos una regla de coherencia.");
        }

        if (Parameters.HealthyScore is < 20m or > 100m)
        {
            throw new RoadmapException(
                $"'healthyScore' debe estar dentro de la escala 20-100; se declaro {Parameters.HealthyScore}.");
        }

        if (Parameters.SignificantUserBase < 0)
        {
            throw new RoadmapException("'significantUserBase' no puede ser negativo.");
        }
    }

    public RoadmapDefinition Require(RoadmapType roadmap)
        => Roadmaps.SingleOrDefault(r => r.Key == roadmap)
            ?? throw new RoadmapException($"El catalogo no declara el roadmap '{roadmap}'.");

    public DispositionDefinition RequireDisposition(ApplicationDisposition disposition)
        => Dispositions.SingleOrDefault(d => d.Key == disposition)
            ?? throw new RoadmapException($"El catalogo no declara la disposition '{disposition}'.");

    public CoherenceRuleDefinition RequireRule(string key)
        => CoherenceRules.SingleOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal))
            ?? throw new RoadmapException($"El catalogo no declara la regla de coherencia '{key}'.");

    /// <summary>True si el par disposition/roadmap es uno de los siete de MAPS.</summary>
    public bool IsValidPair(ApplicationDisposition disposition, RoadmapType roadmap)
        => Roadmaps.Any(r => r.Key == roadmap && r.Disposition == disposition);
}

/// <summary>
/// Umbrales que usan las reglas de coherencia. Cambiar uno es publicar un
/// catalogo nuevo, no recompilar.
/// </summary>
public sealed record RoadmapParameters
{
    /// <summary>
    /// Score a partir del cual reconstruir desde cero pide justificacion. Coincide
    /// con el piso del tramo Solido que muestra la interfaz.
    /// </summary>
    public required decimal HealthyScore { get; init; }

    /// <summary>Usuarios a partir de los cuales una baja es un proyecto de migracion.</summary>
    public required int SignificantUserBase { get; init; }
}

public sealed record DispositionDefinition
{
    public required ApplicationDisposition Key { get; init; }
    public required string Name { get; init; }
    public required string Definition { get; init; }
}

public sealed record RoadmapDefinition
{
    public required RoadmapType Key { get; init; }
    public required string Name { get; init; }
    public required ApplicationDisposition Disposition { get; init; }

    /// <summary>Definicion textual del BRD.</summary>
    public required string Definition { get; init; }

    /// <summary>Puntaje que el BRD asigna a este roadmap.</summary>
    public required decimal MappedScore { get; init; }

    /// <summary>True para Retire y Consolidate: la aplicacion deja el portafolio.</summary>
    public required bool RemovesFromPortfolio { get; init; }

    /// <summary>
    /// Palancas del what-if que la ejecucion de este roadmap entrega de forma
    /// natural. Es lo que permite proyectar el score futuro con el mismo motor que
    /// calcula el actual, en vez de con un numero escrito a mano.
    /// </summary>
    public required IReadOnlyList<string> Levers { get; init; }

    /// <summary>Por que esas palancas y no otras.</summary>
    public required string LeverRationale { get; init; }

    public required IReadOnlyList<MilestoneDefinition> Milestones { get; init; }

    /// <summary>Duracion total del roadmap, sumando sus hitos.</summary>
    public int TotalWeeks => Milestones.Sum(m => m.Weeks);
}

public sealed record MilestoneDefinition
{
    public required string Name { get; init; }
    public required int Weeks { get; init; }
    public required string Owner { get; init; }
}

public sealed record CoherenceRuleDefinition
{
    public required string Key { get; init; }
    public required CoherenceSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
}

/// <summary>Gravedad de una incoherencia entre el plan declarado y los datos.</summary>
public enum CoherenceSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>Catalogo de roadmap invalido o incompleto.</summary>
public sealed class RoadmapException : Exception
{
    public RoadmapException(string message) : base(message) { }

    public RoadmapException(string message, Exception innerException) : base(message, innerException) { }

    public RoadmapException() { }
}
