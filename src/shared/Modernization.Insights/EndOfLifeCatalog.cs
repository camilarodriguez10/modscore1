using System.Text.Json;

namespace Modernization.Insights;

/// <summary>
/// Las fechas de fin de vida de los runtimes, frameworks, sistemas operativos y bases de
/// datos que puede declarar un repositorio, tomadas de endoflife.date.
/// </summary>
/// <remarks>
/// La foto vigente vive en <c>contracts/eol-catalog-v1.json</c>, con la fecha en que se
/// consultó la API. La integración con GitHub Enterprise la refrescará con una frecuencia
/// fija (ADR 0026); mientras tanto, cambiarla es regenerar ese archivo y desplegar.
/// </remarks>
public sealed record EndOfLifeCatalog
{
    public const string DefaultVersion = "eol-catalog-v1";

    public const int MaxProducts = 500;
    public const int MaxCyclesPerProduct = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<EndOfLifeCatalog> DefaultCatalog = new(() => Load(DefaultVersion));

    public required string Version { get; init; }

    public string? Description { get; init; }

    /// <summary>De dónde salen las fechas: <c>endoflife.date</c>.</summary>
    public required string Source { get; init; }

    public required string SourceUrl { get; init; }

    /// <summary>El día en que se consultó la fuente.</summary>
    public required DateOnly RetrievedOn { get; init; }

    public required IReadOnlyList<EndOfLifeProduct> Products { get; init; }

    public static EndOfLifeCatalog Default => DefaultCatalog.Value;

    public int CycleCount => Products.Sum(p => p.Cycles.Count);

    public static EndOfLifeCatalog Load(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var assembly = typeof(EndOfLifeCatalog).Assembly;

        using var stream = assembly.GetManifestResourceStream(version + ".json")
            ?? throw new EndOfLifeCatalogException($"No existe el catalogo de fin de vida '{version}'.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    public static EndOfLifeCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        EndOfLifeCatalog? catalog;

        try
        {
            catalog = JsonSerializer.Deserialize<EndOfLifeCatalog>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new EndOfLifeCatalogException($"El catalogo de fin de vida no es JSON valido: {ex.Message}", ex);
        }

        if (catalog is null)
        {
            throw new EndOfLifeCatalogException("El catalogo de fin de vida esta vacio.");
        }

        var problems = catalog.Problems();

        if (problems.Count > 0)
        {
            throw new EndOfLifeCatalogException(
                "El catalogo de fin de vida no es consistente: " + string.Join(", ", problems) + ".");
        }

        return catalog;
    }

    /// <summary>Lo que no cuadra, por producto. Vacío si el catálogo se puede usar.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();

        if (Products.Count == 0 || Products.Count > MaxProducts)
        {
            problems.Add("products");
        }

        if (Products.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != Products.Count)
        {
            problems.Add("products.duplicated");
        }

        foreach (var product in Products)
        {
            if (string.IsNullOrWhiteSpace(product.Key)
                || string.IsNullOrWhiteSpace(product.Name)
                || !EndOfLifeKinds.All.Contains(product.Kind, StringComparer.Ordinal)
                || product.Cycles.Count == 0
                || product.Cycles.Count > MaxCyclesPerProduct
                || product.Cycles.Any(c => string.IsNullOrWhiteSpace(c.Cycle))
                || product.Cycles.Select(c => c.Cycle).Distinct(StringComparer.Ordinal).Count() != product.Cycles.Count
                // Un ciclo sin fecha y sin marca de discontinuado dice "con soporte"; con las
                // dos a la vez, no dice nada.
                || product.Cycles.Any(c => c.Eol is not null && c.Discontinued))
            {
                problems.Add(product.Key ?? "product");
            }
        }

        return problems;
    }

    public EndOfLifeProduct? Find(string product)
        => Products.FirstOrDefault(p => string.Equals(p.Key, product, StringComparison.Ordinal));

    /// <summary>
    /// El ciclo al que pertenece una versión: el identificador más largo que coincide con
    /// su comienzo en un límite de punto. <c>3.10.2</c> es el ciclo 3.10, no el 3.1;
    /// <c>2.12.4</c> de Log4j es la rama 2.12, no la 2.
    /// </summary>
    /// <remarks>
    /// Java 8 se escribe <c>1.8.0_392</c>: si nada coincide y la versión empieza con
    /// <c>1.</c>, se prueba sin ese prefijo.
    /// </remarks>
    public EndOfLifeCycle? Match(string product, string version)
    {
        if (Find(product) is not { } entry || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim().TrimStart('v', 'V');

        return MatchIn(entry, trimmed)
            ?? (trimmed.StartsWith("1.", StringComparison.Ordinal) ? MatchIn(entry, trimmed[2..]) : null);
    }

    private static EndOfLifeCycle? MatchIn(EndOfLifeProduct product, string version)
        => product.Cycles
            .Where(c => string.Equals(version, c.Cycle, StringComparison.OrdinalIgnoreCase)
                || version.StartsWith(c.Cycle + ".", StringComparison.OrdinalIgnoreCase)
                || version.StartsWith(c.Cycle + "_", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Cycle.Length)
            .FirstOrDefault();
}

/// <summary>Un producto del catálogo y sus ciclos, del más nuevo al más viejo.</summary>
public sealed record EndOfLifeProduct(
    string Key,
    string Name,
    string Kind,
    string SourceUrl,
    IReadOnlyList<EndOfLifeCycle> Cycles);

/// <summary>
/// Un ciclo de vida: una versión mayor o menor con fechas propias. <c>Eol</c> es el fin del
/// soporte de seguridad gratuito; <c>ExtendedSupportEnds</c>, el del soporte pagado, que se
/// muestra pero no cuenta como vigencia.
/// </summary>
public sealed record EndOfLifeCycle
{
    public required string Cycle { get; init; }

    /// <summary>Nombre comercial cuando difiere del identificador: SQL Server 15.0 es 2019.</summary>
    public string? Label { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>Fin del soporte activo: después solo recibe parches de seguridad.</summary>
    public DateOnly? SupportEnds { get; init; }

    public DateOnly? Eol { get; init; }

    /// <summary>Sin soporte y sin fecha publicada, como jQuery 1 y 2.</summary>
    public bool Discontinued { get; init; }

    public DateOnly? ExtendedSupportEnds { get; init; }

    public bool Lts { get; init; }

    /// <summary>
    /// El estado en una fecha. Fin de vida pasado, próximo dentro de la ventana, solo
    /// seguridad cuando terminó el soporte activo, o con soporte.
    /// </summary>
    public string StatusOn(DateOnly asOf, int approachingMonths)
    {
        if (Discontinued || (Eol is { } eol && eol <= asOf))
        {
            return EndOfLifeStatus.EndOfLife;
        }

        if (Eol is { } upcoming && upcoming <= asOf.AddMonths(approachingMonths))
        {
            return EndOfLifeStatus.Approaching;
        }

        if (SupportEnds is { } support && support <= asOf)
        {
            return EndOfLifeStatus.SecurityOnly;
        }

        return EndOfLifeStatus.Supported;
    }

    /// <summary>Meses completos hasta el fin de vida; negativo si ya pasó. Null sin fecha.</summary>
    public int? MonthsToEol(DateOnly asOf)
    {
        if (Eol is not { } eol)
        {
            return null;
        }

        var months = ((eol.Year - asOf.Year) * 12) + eol.Month - asOf.Month;

        // Meses completos: del 20 de enero al 10 de marzo es uno, no dos.
        if (eol >= asOf && eol.Day < asOf.Day)
        {
            months--;
        }
        else if (eol < asOf && eol.Day > asOf.Day)
        {
            months++;
        }

        return months;
    }
}

/// <summary>Qué clase de componente es.</summary>
public static class EndOfLifeKinds
{
    public const string Runtime = "runtime";
    public const string Framework = "framework";
    public const string Library = "library";

    /// <summary>Servidor de aplicaciones: Tomcat.</summary>
    public const string Server = "server";
    public const string OperatingSystem = "os";
    public const string Database = "database";

    public static readonly IReadOnlyList<string> All = [Runtime, Framework, Library, Server, OperatingSystem, Database];
}

/// <summary>El estado de un componente respecto de su ciclo de vida.</summary>
public static class EndOfLifeStatus
{
    public const string EndOfLife = "eol";
    public const string Approaching = "approaching";
    public const string SecurityOnly = "security";
    public const string Supported = "supported";

    /// <summary>El producto o la versión no están en el catálogo: no se puede evaluar.</summary>
    public const string Unknown = "unknown";

    /// <summary>Del peor al mejor. <c>Unknown</c> va al final: no es bueno, es no saber.</summary>
    public static readonly IReadOnlyList<string> Ordered = [EndOfLife, Approaching, SecurityOnly, Supported, Unknown];

    public static int Rank(string status)
    {
        var index = Ordered.ToList().IndexOf(status);
        return index < 0 ? Ordered.Count : index;
    }
}

public sealed class EndOfLifeCatalogException : Exception
{
    public EndOfLifeCatalogException(string message)
        : base(message)
    {
    }

    public EndOfLifeCatalogException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EndOfLifeCatalogException()
    {
    }
}
