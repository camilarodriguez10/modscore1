namespace Modernization.Domain.Code;

/// <summary>
/// Lo que se sabe de un repositorio sin guardar su código: los metadatos, los componentes
/// con ciclo de vida declarados en sus manifiestos y las dependencias con su estado.
/// </summary>
/// <remarks>
/// Es la forma que entregará el recolector de GitHub Enterprise (ADR 0026): todo sale de la
/// API —metadatos, grafo de dependencias o SBOM, alertas de Dependabot— y de leer archivos
/// de manifiesto conocidos. Ninguna línea de código fuente viaja ni se guarda.
/// </remarks>
public sealed record RepositoryInventory
{
    /// <summary>La aplicación a la que pertenece, por la propiedad del repositorio que la nombra.</summary>
    public required string EaiCode { get; init; }

    /// <summary>Organización y nombre: <c>org/repo</c>.</summary>
    public required string Name { get; init; }

    public required string DefaultBranch { get; init; }

    public string? PrimaryLanguage { get; init; }

    /// <summary>Último commit en la rama principal. Null si la fuente no lo informa.</summary>
    public DateOnly? LastCommit { get; init; }

    /// <summary>Archivado en GitHub: sin cambios posibles, pero todavía asociado a la aplicación.</summary>
    public bool Archived { get; init; }

    public required IReadOnlyList<RepositoryComponent> Components { get; init; }

    public required IReadOnlyList<RepositoryDependency> Dependencies { get; init; }
}

/// <summary>
/// Un runtime, framework, sistema operativo o base de datos con ciclo de vida publicado.
/// <c>Product</c> es el identificador de endoflife.date (<c>nodejs</c>, <c>dotnet</c>,
/// <c>oracle-jdk</c>...), para cruzarlo con el catálogo sin traducciones.
/// </summary>
public sealed record RepositoryComponent(string Product, string Version, string DetectedIn);

/// <summary>Una biblioteca del grafo de dependencias, con su versión y lo que se sabe de ella.</summary>
public sealed record RepositoryDependency
{
    /// <summary>npm, NuGet, Maven, PyPI, Go.</summary>
    public required string Ecosystem { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    /// <summary>La última versión publicada en el registro. Null si no se pudo consultar.</summary>
    public string? Latest { get; init; }

    /// <summary>Declarada en el manifiesto; false si llega de forma transitiva.</summary>
    public bool Direct { get; init; } = true;

    /// <summary>Marcada como obsoleta por su autor en el registro.</summary>
    public bool Deprecated { get; init; }

    /// <summary>Alertas abiertas por severidad, del mismo origen que Dependabot.</summary>
    public int Critical { get; init; }
    public int High { get; init; }
    public int Medium { get; init; }
    public int Low { get; init; }
}

/// <summary>El inventario de código disponible y de dónde sale.</summary>
public sealed record CodeInventorySet
{
    /// <summary>Sample (datos de demostración), None (sin integración) o, más adelante, GitHubEnterprise.</summary>
    public required string Provider { get; init; }

    /// <summary>True solo cuando los datos vienen de repositorios reales.</summary>
    public required bool Connected { get; init; }

    /// <summary>Cuándo se leyeron los repositorios. Null si no hay inventario.</summary>
    public DateTimeOffset? ScannedAt { get; init; }

    public required IReadOnlyList<RepositoryInventory> Repositories { get; init; }

    public static CodeInventorySet Empty(string provider) => new()
    {
        Provider = provider,
        Connected = false,
        Repositories = [],
    };
}

/// <summary>
/// Entrega el inventario de código. Igual que las fuentes del portafolio, el resto del
/// sistema depende de esta interfaz: pasar de los datos de demostración a GitHub
/// Enterprise es registrar otra implementación.
/// </summary>
public interface ICodeInventoryProvider
{
    string Provider { get; }

    Task<CodeInventorySet> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Sin integración: la pantalla lo dice en lugar de mostrar datos.</summary>
public sealed class NoCodeInventoryProvider : ICodeInventoryProvider
{
    public const string Name = "None";

    public string Provider => Name;

    public Task<CodeInventorySet> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CodeInventorySet.Empty(Name));
}
