using Modernization.Sources.Files;

namespace Modernization.Sources.Connectors;

/// <summary>Una fuente configurada, ya resuelta contra el catálogo de contratos.</summary>
/// <param name="Key">La raíz del nombre canónico: <c>tech_debt</c>.</param>
/// <param name="Schema">El contrato de la fuente.</param>
/// <param name="ConnectionName">El nombre de la conexión que la lee.</param>
/// <param name="Connection">La conexión.</param>
/// <param name="Options">De dónde se lee.</param>
public sealed record ResolvedSource(
    string Key,
    SourceSchema Schema,
    string ConnectionName,
    ConnectionOptions Connection,
    SourceOptions Options);

/// <summary>
/// La forma de la sección <c>Connectors</c>, revisada al arrancar: una configuración que no
/// puede funcionar detiene el proceso con todos sus problemas juntos, en vez de fallar en la
/// primera lectura. Lo que depende de secretos —TLS, contraseñas— se revisa al conectar.
/// </summary>
public static class ConnectorConfiguration
{
    /// <summary>Los problemas de la configuración, en inglés para el log de arranque. Vacío si sirve.</summary>
    public static IReadOnlyList<string> Validate(ConnectorOptions options, SourceSchemaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);

        var problems = new List<string>();

        if (!options.Enabled)
        {
            return problems;
        }

        if (options.Sources.Count == 0)
        {
            problems.Add("Connectors:Enabled is true but Connectors:Sources is empty.");
        }

        if (options.ConnectTimeoutSeconds is < 1 or > 300)
        {
            problems.Add("Connectors:ConnectTimeoutSeconds must be between 1 and 300.");
        }

        if (options.CommandTimeoutSeconds is < 1 or > 3600)
        {
            problems.Add("Connectors:CommandTimeoutSeconds must be between 1 and 3600.");
        }

        if (options.PullInterval is { } interval && interval < ConnectorOptions.MinPullInterval)
        {
            problems.Add($"Connectors:PullInterval must be at least {ConnectorOptions.MinPullInterval:c}: every pull publishes a version.");
        }

        foreach (var (name, connection) in options.Connections)
        {
            if (ConnectorProviders.Normalize(connection.Provider) is null)
            {
                problems.Add($"Connectors:Connections:{name}:Provider is '{connection.Provider}'. Valid providers: {string.Join(", ", ConnectorProviders.All)}.");
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionString) && string.IsNullOrWhiteSpace(connection.ConnectionStringFile))
            {
                problems.Add($"Connectors:Connections:{name} needs ConnectionString (from the secrets directory) or ConnectionStringFile.");
            }
        }

        foreach (var (key, source) in options.Sources)
        {
            if (SchemaFor(catalog, key) is null)
            {
                problems.Add($"Connectors:Sources:{key} is not a source. Valid sources: {string.Join(", ", catalog.Files.Select(f => KeyOf(f.FileName)))}.");
            }

            if (FindConnection(options, source.Connection) is null)
            {
                problems.Add($"Connectors:Sources:{key}:Connection is '{source.Connection}', which is not in Connectors:Connections.");
            }

            try
            {
                SourceQueries.StatementFor(key, source);
            }
            catch (ConnectorException ex)
            {
                problems.Add($"Connectors:Sources:{key}: {ex.Message}");
            }
        }

        var duplicated = options.Sources.Keys
            .GroupBy(KeyOf, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicated.Count > 0)
        {
            problems.Add($"Connectors:Sources lists the same source twice: {string.Join(", ", duplicated)}.");
        }

        return problems;
    }

    /// <summary>Las fuentes que se pueden leer, en el orden del catálogo.</summary>
    public static IReadOnlyList<ResolvedSource> Resolve(ConnectorOptions options, SourceSchemaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);

        var resolved = new List<ResolvedSource>();

        foreach (var schema in catalog.Files)
        {
            var key = KeyOf(schema.FileName);
            var entry = options.Sources.FirstOrDefault(s => string.Equals(KeyOf(s.Key), key, StringComparison.OrdinalIgnoreCase));

            if (entry.Value is null || FindConnection(options, entry.Value.Connection) is not { } connection)
            {
                continue;
            }

            resolved.Add(new ResolvedSource(key, schema, connection.Key, connection.Value, entry.Value));
        }

        return resolved;
    }

    /// <summary>La raíz del nombre canónico: <c>tech_debt.csv</c> y <c>tech_debt</c> dan <c>tech_debt</c>.</summary>
    public static string KeyOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        return trimmed.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    private static SourceSchema? SchemaFor(SourceSchemaCatalog catalog, string key)
        => catalog.Files.FirstOrDefault(f => string.Equals(KeyOf(f.FileName), KeyOf(key), StringComparison.OrdinalIgnoreCase));

    private static KeyValuePair<string, ConnectionOptions>? FindConnection(ConnectorOptions options, string? name)
    {
        foreach (var entry in options.Connections)
        {
            if (string.Equals(entry.Key, name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}
