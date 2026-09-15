using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;

namespace Modernization.Sources.Database;

/// <summary>Configuración del adaptador de base de datos.</summary>
/// <param name="Provider">Motor. Hoy solo <c>Sqlite</c>; el definitivo depende de RFC-07.</param>
/// <param name="ConnectionString">Cadena de conexion, que viene del gestor de secretos.</param>
/// <param name="RetainVersions">Cuantas cargas conservar. 0 conserva todas.</param>
public sealed record DatabaseAdapterOptions(string Provider, string ConnectionString, int RetainVersions = 0);

/// <summary>
/// Registro del adaptador. Es el unico punto que la capa System necesita conocer
/// para servirse de una base en vez de archivos.
/// </summary>
public static class DatabaseAdapter
{
    public const string Sqlite = "Sqlite";

    public static IServiceCollection AddDatabaseSnapshotAdapter(
        this IServiceCollection services,
        DatabaseAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddDbContextFactory<ModernizationDbContext>(builder => Configure(builder, options));
        services.AddSingleton<IApplicationSnapshotProvider, DatabaseSnapshotProvider>();
        services.AddSingleton<ISnapshotPublisher>(provider => new DatabaseSnapshotPublisher(
            provider.GetRequiredService<IDbContextFactory<ModernizationDbContext>>(),
            provider.GetService<SourceSchemaCatalog>(),
            options.RetainVersions));

        // Las reglas se guardan en la misma base que los datos que puntuan.
        services.AddSingleton<Modernization.Domain.Rules.IRulesStore, DatabaseRulesStore>();

        return services;
    }

    /// <summary>
    /// Aplica las migraciones pendientes. En un despliegue con varias replicas esto
    /// lo hace un job antes de arrancar, no cada proceso.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var contexts = services.GetRequiredService<IDbContextFactory<ModernizationDbContext>>();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void Configure(DbContextOptionsBuilder builder, DatabaseAdapterOptions options)
    {
        if (!string.Equals(options.Provider, Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            // El motor definitivo es RFC-07 y sigue abierto. El modelo es agnostico:
            // agregar SQL Server o PostgreSQL es un paquete y un juego de
            // migraciones, no un rediseno. Fallar aqui es preferible a arrancar
            // contra un motor que nadie verifico.
            throw new InvalidOperationException(
                $"'Database:Provider' admite hoy '{Sqlite}'. El motor de despliegue se decide en RFC-07.");
        }

        builder.UseSqlite(options.ConnectionString);
    }
}

/// <summary>
/// Fabrica de diseño: la usa <c>dotnet ef</c> para generar migraciones sin levantar
/// la aplicacion. La cadena de conexion sale del entorno y nunca del repositorio.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ModernizationDbContext>
{
    public ModernizationDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ModernizationDbContext>();

        DatabaseAdapter.Configure(
            builder,
            new DatabaseAdapterOptions(
                DatabaseAdapter.Sqlite,
                Environment.GetEnvironmentVariable("MODSCORE_DB_CONNECTION") ?? "Data Source=modernization-design.db"));

        return new ModernizationDbContext(builder.Options);
    }
}
