using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modernization.Sources.Database;

/// <summary>
/// El esquema de la base tiene la forma de las fuentes, no la del dominio: una
/// tabla por archivo, con sus mismas columnas, mas la carga a la que pertenece
/// cada fila.
/// </summary>
/// <remarks>
/// Guardar el dato crudo es lo que mantiene una sola traduccion en todo el sistema:
/// el ensamblador sigue siendo el unico que sabe que "1-Critical" es una severidad
/// y que APP_ID no es EAI_CODE. Si la base guardara el modelo canonico, habria dos
/// traducciones que mantener de acuerdo, y un mismo archivo podria puntuar distinto
/// segun por donde entrara.
/// <para>
/// La carga se ata con una <b>propiedad sombra</b>: los registros de fuente son los
/// mismos tipos que usan los adaptadores Dummy y File, sin un campo de persistencia
/// metido dentro.
/// </para>
/// </remarks>
public sealed class ModernizationDbContext(DbContextOptions<ModernizationDbContext> options) : DbContext(options)
{
    /// <summary>Columna sombra que ata cada fila a su carga.</summary>
    public const string VersionId = "VersionId";

    private const int IdentifierLength = 64;
    private const int LabelLength = 256;
    private const int TextLength = 1024;

    public DbSet<SnapshotVersionRow> Versions => Set<SnapshotVersionRow>();

    public DbSet<SourceRejectionRow> Rejections => Set<SourceRejectionRow>();

    public DbSet<EaiRecord> Eai => Set<EaiRecord>();

    public DbSet<ArchitecturalValueRecord> ArchitecturalValue => Set<ArchitecturalValueRecord>();

    public DbSet<ProductStrategyRecord> ProductStrategy => Set<ProductStrategyRecord>();

    public DbSet<TechDebtRecord> TechDebt => Set<TechDebtRecord>();

    public DbSet<ScaFlawRecord> ScaFlaws => Set<ScaFlawRecord>();

    public DbSet<OssScoreRecord> OssScore => Set<OssScoreRecord>();

    public DbSet<MonitoringRecord> Monitoring => Set<MonitoringRecord>();

    public DbSet<ApimOnboardingRecord> ApimOnboarding => Set<ApimOnboardingRecord>();

    /// <summary>Versiones de reglas publicadas desde el portal (Fase 12).</summary>
    public DbSet<RulesVersionRow> RulesVersions => Set<RulesVersionRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Ninguna columna de texto de las fuentes se acerca a este tope, y acotarlas
        // evita que un motor las cree como texto ilimitado, que no se puede indexar.
        configurationBuilder.Properties<string>().HaveMaxLength(TextLength);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SnapshotVersionRow>(entity =>
        {
            entity.ToTable("snapshot_version");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).HasMaxLength(IdentifierLength);
            entity.Property(v => v.Label).HasMaxLength(LabelLength);
            entity.Property(v => v.UploadedBy).HasMaxLength(IdentifierLength * 2);

            // Un JSON con un campo por columna de cada fuente: sin el tope de 1 KB.
            entity.Property(v => v.ColumnMaps).Metadata.SetMaxLength(null);
            entity.HasIndex(v => v.CapturedAtUtc);
        });

        modelBuilder.Entity<SourceRejectionRow>(entity =>
        {
            entity.ToTable("source_rejection");
            entity.Property<long>("Id").ValueGeneratedOnAdd();
            entity.HasKey("Id");
            entity.Property(r => r.VersionId).HasMaxLength(IdentifierLength);
            entity.Property(r => r.Source).HasMaxLength(LabelLength);
            entity.Property(r => r.Key).HasMaxLength(IdentifierLength);
            entity.HasIndex(r => r.VersionId);
        });

        modelBuilder.Entity<RulesVersionRow>(entity =>
        {
            entity.ToTable("rules_version");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).HasMaxLength(Modernization.Domain.Rules.RulesVersionId.MaxLength);
            entity.Property(v => v.BasedOn).HasMaxLength(Modernization.Domain.Rules.RulesVersionId.MaxLength);
            entity.Property(v => v.PublishedBy).HasMaxLength(IdentifierLength * 2);
            entity.Property(v => v.Reason).HasMaxLength(LabelLength * 2);

            // Los documentos son JSON de decenas de KB: sin el tope de 1 KB que la
            // convencion pone al resto del texto.
            entity.Property(v => v.Ruleset).Metadata.SetMaxLength(null);
            entity.Property(v => v.Levers).Metadata.SetMaxLength(null);
            entity.Property(v => v.Roadmap).Metadata.SetMaxLength(null);
            entity.Property(v => v.Changes).Metadata.SetMaxLength(null);
            entity.Property(v => v.Impact).Metadata.SetMaxLength(null);
            entity.Property(v => v.Analytics).Metadata.SetMaxLength(null);
            entity.Property(v => v.Analytics).HasDefaultValue(string.Empty);
        });

        Source<EaiRecord>(modelBuilder, "eai_master", entity =>
        {
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength);
            entity.Property(r => r.AppId).HasMaxLength(IdentifierLength);

            // Una aplicacion no puede aparecer dos veces en la misma carga: el
            // portafolio quedaria con un EAI duplicado y el contrato lo prohibe.
            entity.HasIndex(VersionId, nameof(EaiRecord.EaiCode)).IsUnique();
        });

        Source<ArchitecturalValueRecord>(modelBuilder, "architectural_value", entity =>
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength));

        Source<ProductStrategyRecord>(modelBuilder, "product_strategy", entity =>
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength));

        Source<TechDebtRecord>(modelBuilder, "tech_debt", entity =>
            entity.Property(r => r.AppId).HasMaxLength(IdentifierLength));

        Source<ScaFlawRecord>(modelBuilder, "sca_flaw", entity =>
        {
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength);
            entity.Property(r => r.AppId).HasMaxLength(IdentifierLength);
            entity.Property(r => r.SeverityDescription).HasMaxLength(LabelLength);
        });

        Source<OssScoreRecord>(modelBuilder, "oss_score", entity =>
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength));

        Source<MonitoringRecord>(modelBuilder, "monitoring", entity =>
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength));

        Source<ApimOnboardingRecord>(modelBuilder, "apim_onboarding", entity =>
            entity.Property(r => r.EaiCode).HasMaxLength(IdentifierLength));
    }

    /// <summary>
    /// Una tabla de fuente: clave sustituta, la carga como propiedad sombra e
    /// indice por carga, que es como se lee siempre.
    /// </summary>
    private static void Source<TRecord>(
        ModelBuilder modelBuilder,
        string table,
        Action<EntityTypeBuilder<TRecord>> configure)
        where TRecord : class
    {
        modelBuilder.Entity<TRecord>(entity =>
        {
            entity.ToTable(table);
            entity.Property<long>("Id").ValueGeneratedOnAdd();
            entity.HasKey("Id");
            entity.Property<string>(VersionId).HasMaxLength(IdentifierLength).IsRequired();
            entity.HasIndex(VersionId);

            configure(entity);
        });
    }
}

/// <summary>Una carga publicada. Equivale a un directorio en el adaptador de archivos.</summary>
public sealed class SnapshotVersionRow
{
    public required string Id { get; set; }

    /// <summary>
    /// Momento de la captura, siempre en UTC. Se guarda como fecha sin desfase a
    /// proposito: SQLite no sabe ordenar por DateTimeOffset, y ordenar por la
    /// captura es exactamente lo que hace toda consulta de versiones. Guardar lo
    /// unico que el sistema produce —UTC— evita depender del motor para eso.
    /// </summary>
    public required DateTime CapturedAtUtc { get; set; }

    public required string Label { get; set; }

    /// <summary>Subject seudonimo de quien la publico.</summary>
    public required string UploadedBy { get; set; }

    public required int ApplicationCount { get; set; }

    /// <summary>
    /// Qué columna de cada archivo cargó cada campo, como JSON. Null en las cargas
    /// anteriores al 2026-09-28, que no lo registraban.
    /// </summary>
    public string? ColumnMaps { get; set; }
}

/// <summary>El mapping de una carga, como se guarda en su fila.</summary>
internal static class ColumnMapStorage
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);

    public static string? Serialize(IReadOnlyList<Modernization.Domain.Sources.SourceColumnMap> maps)
        => maps.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(maps, Options);

    public static IReadOnlyList<Modernization.Domain.Sources.SourceColumnMap> Deserialize(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Modernization.Domain.Sources.SourceColumnMap>>(stored, Options) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            // Un registro corrupto deja la próxima carga sin sugerencia, no sin carga.
            return [];
        }
    }
}

/// <summary>
/// Fila descartada al publicar, con su motivo.
/// </summary>
/// <remarks>
/// Se guarda porque la pantalla de linaje la necesita: con archivos el rechazo se
/// vuelve a calcular en cada lectura, pero aqui el archivo original ya no existe.
/// Sin esta tabla, migrar a base de datos apagaria en silencio la mitad de esa
/// pantalla.
/// </remarks>
public sealed class SourceRejectionRow
{
    public required string VersionId { get; set; }

    public required string Source { get; set; }

    public required int RowNumber { get; set; }

    public required string Reason { get; set; }

    public string? Key { get; set; }
}
