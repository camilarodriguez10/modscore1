using System.Globalization;
using System.Security.Cryptography;
using Modernization.Domain.Sources;
using Modernization.Domain;

namespace Modernization.Sources.Files;

/// <summary>
/// Adaptador de archivos. Lee los CSV de un directorio de versiones y los
/// normaliza con el mismo ensamblador que usa el adaptador Dummy.
/// </summary>
/// <remarks>
/// La historia es el layout del disco: cada carga vive en su propio subdirectorio
/// con nombre ordenable, y el mas reciente es la version actual.
/// <code>
/// data/history/
///   20260908-1200-carga-0809/
///     eai_master.csv
///     product_strategy.csv
///     ...
///   20261115-0930-carga-1115/
///     ...
/// </code>
/// Cuando la Fase 09 mueva esto a base de datos, un directorio pasa a ser una fila
/// de la tabla de snapshots y el resto del sistema no se entera.
/// </remarks>
public sealed class FileSnapshotProvider : IApplicationSnapshotProvider
{
    public const string ProviderName = "File";

    /// <summary>Metadatos de la carga, junto a los CSV.</summary>
    internal const string ManifestFileName = "manifest.json";

    private readonly string _rootDirectory;
    private readonly SourceSchemaCatalog _catalog;

    public FileSnapshotProvider(string rootDirectory, SourceSchemaCatalog? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = rootDirectory;
        _catalog = catalog ?? SourceSchemaCatalog.Default;
    }

    public string Provider => ProviderName;

    public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ReadVersions());
    }

    public async Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
    {
        var versions = ReadVersions();

        if (versions.Count == 0)
        {
            // El estado inicial legitimo del adaptador, no un fallo: la pantalla
            // de cargas tiene que seguir funcionando para poder salir de el.
            throw new NoSnapshotAvailableException();
        }

        var version = versionId is { Length: > 0 }
            ? versions.SingleOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal))
                ?? throw new SourceSchemaException($"The upload '{versionId}' does not exist.")
            : versions[0];

        return await ReadVersionAsync(version, cancellationToken).ConfigureAwait(false);
    }

    public Task<SnapshotSet> GetAllAsync(CancellationToken cancellationToken = default)
        => GetAllAsync(null, cancellationToken);

    // -----------------------------------------------------------------------

    private IReadOnlyList<SnapshotVersion> ReadVersions()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        var versions = new List<SnapshotVersion>();

        foreach (var directory in Directory.EnumerateDirectories(_rootDirectory))
        {
            var manifest = UploadManifest.TryRead(directory);
            if (manifest is null)
            {
                continue;
            }

            versions.Add(new SnapshotVersion
            {
                Id = manifest.Id,
                CapturedAt = manifest.CapturedAt,
                Label = manifest.Label,
                Provider = ProviderName,
                ApplicationCount = manifest.ApplicationCount,
                IsCurrent = false,
            });
        }

        // Mas reciente primero; la primera es la actual.
        versions.Sort((a, b) => b.CapturedAt.CompareTo(a.CapturedAt));

        return versions.Count == 0
            ? []
            : [versions[0] with { IsCurrent = true }, .. versions.Skip(1)];
    }

    private Task<SnapshotSet> ReadVersionAsync(SnapshotVersion version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.Combine(_rootDirectory, version.Id);
        var manifest = UploadManifest.TryRead(directory);

        var eai = ReadFile(directory, manifest, "eai_master.csv", version, FileRecordMappers.ToEai);
        var archValue = ReadFile(directory, manifest, "architectural_value_by_eai.csv", version, FileRecordMappers.ToArchitecturalValue);
        var productStrategy = ReadFile(directory, manifest, "product_strategy.csv", version, FileRecordMappers.ToProductStrategy);
        var techDebt = ReadFile(directory, manifest, "tech_debt.csv", version, FileRecordMappers.ToTechDebt);
        var sca = ReadFile(directory, manifest, "veracode_sca_open_flaws.csv", version, FileRecordMappers.ToScaFlaw);
        var oss = ReadFile(directory, manifest, "oss_score_by_eai.csv", version, FileRecordMappers.ToOssScore);
        var monitoring = ReadFile(directory, manifest, "monitoreo.csv", version, FileRecordMappers.ToMonitoring);
        var apim = ReadFile(directory, manifest, "apim_onboarded_eais.csv", version, FileRecordMappers.ToApimOnboarding);

        var assembled = SnapshotAssembler.Assemble(new SnapshotAssembler.Input
        {
            Provider = ProviderName,
            CapturedAt = version.CapturedAt,
            Version = version,
            Eai = eai,
            ArchitecturalValue = archValue,
            ProductStrategy = productStrategy,
            TechDebt = techDebt,
            ScaFlaws = sca,
            OssScore = oss,
            Monitoring = monitoring,
            ApimOnboarding = apim,
        });

        return Task.FromResult(assembled);
    }

    /// <summary>
    /// Lee una fuente de la carga, con todos sus archivos. Un archivo ausente no es un error:
    /// significa que esa fuente no se entrego, y sus metricas quedan sin dato — exactamente
    /// lo mismo que una aplicacion ausente de un archivo presente.
    /// </summary>
    private SourceReadResult<TRecord> ReadFile<TRecord>(
        string directory,
        UploadManifest? manifest,
        string fileName,
        SnapshotVersion version,
        Func<CsvSourceReader.Row, TRecord?> map)
    {
        var schema = _catalog.Require(fileName);
        var parts = PartsOf(directory, manifest, fileName);

        if (parts.Count == 0)
        {
            return new SourceReadResult<TRecord>
            {
                SourceName = schema.Source,
                Provider = ProviderName,
                Origin = $"{version.Id}/{fileName} (ausente)",
                Records = [],
                Rejections = [],
                ExtractedAt = version.CapturedAt,
            };
        }

        var read = new CsvSourceReader(schema).ReadParts(
            [.. parts.Select(p => new CsvSourceReader.SourcePart(() => new StreamReader(p.Path), p.Label))]);

        if (read.FirstError is { } error)
        {
            throw new SourceSchemaException(error);
        }

        var outcome = read.Merged;
        var records = new List<TRecord>(outcome.Rows.Count);
        var rejections = new List<SourceRejection>(outcome.Rejections);

        foreach (var row in outcome.Rows)
        {
            var record = map(row);

            if (record is null)
            {
                rejections.Add(new SourceRejection(
                    row.Number,
                    Notice.Of(NoticeCodes.RowNotConvertible),
                    row.Text(schema.KeyColumn)));
                continue;
            }

            records.Add(record);
        }

        return new SourceReadResult<TRecord>
        {
            SourceName = schema.Source,
            Provider = ProviderName,
            Origin = parts.Count == 1 ? $"{version.Id}/{fileName}" : $"{version.Id}/{fileName} ({parts.Count} files)",
            Records = records,
            Rejections = rejections,
            ExtractedAt = version.CapturedAt,
        };
    }

    /// <summary>
    /// Los archivos guardados de una fuente, en el orden en que llegaron. Una carga anterior
    /// al 2026-10-07 no los declara: tiene uno por fuente, con el nombre canónico.
    /// </summary>
    private static List<(string Path, string? Label)> PartsOf(string directory, UploadManifest? manifest, string fileName)
    {
        var declared = manifest?.Parts
            .Where(p => string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        if (declared.Count == 0)
        {
            var path = Path.Combine(directory, fileName);
            return File.Exists(path) ? [(path, null)] : [];
        }

        // El manifiesto lo escribe esta misma aplicación, pero se lee como cualquier entrada:
        // un nombre guardado es un archivo plano de la carga, nunca una ruta.
        return [.. declared
            .Where(p => IncomingFiles.IsPlainFileName(p.StoredAs))
            .Select(p => (Path.Combine(directory, p.StoredAs), p.ClientFileName))
            .Where(p => File.Exists(p.Item1))];
    }
}

/// <summary>
/// Un archivo guardado de una fuente: la fuente, el nombre con que quedó en la carga y el
/// nombre con que llegó.
/// </summary>
public sealed record UploadPart(string FileName, string StoredAs, string? ClientFileName);

/// <summary>Metadatos de una carga, guardados junto a sus CSV.</summary>
public sealed record UploadManifest
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required int ApplicationCount { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
    public string? UploadedBy { get; init; }

    /// <summary>
    /// Qué columna de cada archivo cargó cada campo. Vacío en las cargas anteriores al
    /// 2026-09-28, que no lo registraban.
    /// </summary>
    public IReadOnlyList<SourceColumnMap> ColumnMaps { get; init; } = [];

    /// <summary>
    /// Los archivos de cada fuente, en el orden en que llegaron. Una fuente puede llegar en
    /// varios desde el 2026-10-07 (ADR 0034). Vacío en las cargas anteriores.
    /// </summary>
    public IReadOnlyList<UploadPart> Parts { get; init; } = [];

    private static readonly System.Text.Json.JsonSerializerOptions Options =
        new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };

    public static UploadManifest? TryRead(string directory)
    {
        var path = Path.Combine(directory, FileSnapshotProvider.ManifestFileName);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<UploadManifest>(
                File.ReadAllText(path), Options);
        }
        catch (System.Text.Json.JsonException)
        {
            // Un manifiesto corrupto oculta la carga en vez de tumbar el listado.
            return null;
        }
    }

    public void Write(string directory)
        => File.WriteAllText(
            Path.Combine(directory, FileSnapshotProvider.ManifestFileName),
            System.Text.Json.JsonSerializer.Serialize(this, Options));

    /// <summary>
    /// Identificador ordenable por fecha, sin caracteres de ruta.
    /// </summary>
    /// <remarks>
    /// Lleva un sufijo aleatorio ademas de la marca de tiempo: con granularidad
    /// de segundos, dos cargas simultaneas producirian el mismo id y la segunda
    /// sobrescribiria a la primera sin aviso.
    /// </remarks>
    public static string NewId(DateTimeOffset capturedAt)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{capturedAt.UtcDateTime:yyyyMMdd-HHmmss}-{RandomNumberGenerator.GetInt32(0x1000, 0x10000):x4}");
}
