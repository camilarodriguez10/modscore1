using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;
using Modernization.Domain;

namespace Modernization.Sources.Database;

/// <summary>
/// Publica una carga en la base de datos: una transaccion por carga.
/// </summary>
/// <remarks>
/// El equivalente exacto del movimiento atomico del directorio en el adaptador de
/// archivos. Si algo falla a mitad, no queda una carga a medias: no queda nada.
/// <para>
/// Los archivos se leen y validan con el mismo <see cref="CsvSourceReader"/> y los
/// mismos contratos de columna, asi que un CSV que el adaptador de archivos acepta
/// es exactamente el que este acepta.
/// </para>
/// </remarks>
public sealed class DatabaseSnapshotPublisher(
    IDbContextFactory<ModernizationDbContext> contexts,
    SourceSchemaCatalog? catalog = null,
    int retainVersions = 0) : ISnapshotPublisher, IDisposable
{
    private readonly SourceSchemaCatalog _catalog = catalog ?? SourceSchemaCatalog.Default;

    /// <summary>El mismo emparejador que usa el adaptador de archivos.</summary>
    private readonly SourceFileMatcher _matcher = new(catalog);

    /// <summary>Una publicacion a la vez, igual que en el adaptador de archivos.</summary>
    private readonly SemaphoreSlim _publishing = new(1, 1);

    public string Provider => DatabaseSnapshotProvider.ProviderName;

    public void Dispose() => _publishing.Dispose();

    public async Task<PublishOutcome> PublishAsync(
        IReadOnlyList<IncomingSourceFile> files,
        string label,
        string uploadedBy,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            return PublishOutcome.Rejected(Notice.Of(NoticeCodes.UploadNoFiles));
        }

        var totalBytes = files.Sum(f => f.Length);
        if (totalBytes > PublishLimits.MaxTotalBytes)
        {
            return PublishOutcome.Rejected(
                Notice.Of(
                    NoticeCodes.UploadTooLarge,
                    ("size", Megabytes(totalBytes)),
                    ("max", Megabytes(PublishLimits.MaxTotalBytes))));
        }

        if (!await _publishing.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return PublishOutcome.Rejected(
                Notice.Of(NoticeCodes.UploadBusy));
        }

        try
        {
            var parsed = new ParsedLoad();
            var outcomes = new List<PublishedFileOutcome>(files.Count);
            var errors = new List<Notice>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var outcome = Parse(file, parsed, seen);
                outcomes.Add(outcome);

                if (outcome.Error is not null)
                {
                    errors.Add(outcome.Error.With("file", outcome.ClientFileName));
                }
            }

            // El maestro EAI es la espina dorsal: sin el no hay portafolio.
            if (!seen.Contains("eai_master.csv"))
            {
                errors.Add(Notice.Of(NoticeCodes.UploadMissingMaster, ("master", "eai_master.csv")));
            }

            if (parsed.Eai.Count == 0 && errors.Count == 0)
            {
                errors.Add(Notice.Of(NoticeCodes.UploadEmpty));
            }

            if (errors.Count > 0)
            {
                return PublishOutcome.Rejected(outcomes, errors);
            }

            var version = await CommitAsync(parsed, label, uploadedBy, capturedAt, cancellationToken)
                .ConfigureAwait(false);

            return new PublishOutcome(true, version, outcomes, [], version.ApplicationCount);
        }
        finally
        {
            _publishing.Release();
        }
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Lee un archivo entrante contra su contrato y acumula sus filas. El nombre
    /// del cliente solo sirve para emparejar; lo que se guarda es la fuente.
    /// </summary>
    private PublishedFileOutcome Parse(IncomingSourceFile file, ParsedLoad parsed, HashSet<string> seen)
    {
        var clientName = Path.GetFileName(file.ClientFileName);

        if (file.Length > PublishLimits.MaxFileBytes)
        {
            return new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(
                    NoticeCodes.FileTooLarge,
                    ("size", Megabytes(file.Length)),
                    ("max", Megabytes(PublishLimits.MaxFileBytes))));
        }

        // Un mapping es de una fuente concreta, igual que en el adaptador de archivos.
        if (file.ColumnMap is not null && string.IsNullOrWhiteSpace(file.AssignedFileName))
        {
            return new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(NoticeCodes.MappingNeedsSource));
        }

        // Se identifica por el encabezado, igual que en el adaptador de archivos:
        // el stream se abre una vez para leer la firma y otra para leer las filas.
        SourceFileMatch match;

        using (var probe = file.OpenRead())
        using (var probeReader = new StreamReader(probe))
        {
            match = _matcher.MatchReader(probeReader, clientName, file.AssignedFileName);
        }

        if (match.Schema is null)
        {
            return new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0, match.Problem)
            {
                DetectedColumns = match.DetectedColumns,
                Candidates = match.Candidates,
            };
        }

        var schema = match.Schema;

        if (!seen.Add(schema.FileName))
        {
            return new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0,
                Notice.Of(NoticeCodes.FileDuplicate, ("source", schema.Source)))
            {
                DetectedColumns = match.DetectedColumns,
            };
        }

        try
        {
            ColumnResolution resolution;

            using (var headerStream = file.OpenRead())
            using (var headerReader = new StreamReader(headerStream))
            {
                resolution = ColumnMapping.Resolve(schema, CsvSourceReader.ReadHeader(headerReader), file.ColumnMap);
            }

            if (!resolution.IsValid && file.ColumnMap is not null)
            {
                seen.Remove(schema.FileName);

                var problem = resolution.Problems.Count == 1
                    ? resolution.Problems[0]
                    : resolution.Problems[0].With("more", resolution.Problems.Count - 1);

                return new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0, problem)
                {
                    DetectedColumns = match.DetectedColumns,
                };
            }

            // Con un mapping, el encabezado se reescribe al leer: el lector de siempre lee
            // exactamente las columnas elegidas.
            var stream = file.OpenRead();
            using var reader = file.ColumnMap is null
                ? new StreamReader(stream)
                : ColumnMapping.Open(new StreamReader(stream), resolution);
            var outcome = new CsvSourceReader(schema).Read(reader);

            var rejected = parsed.Add(schema, outcome);
            parsed.ColumnMaps.Add(resolution.ToColumnMap());

            return new PublishedFileOutcome(
                clientName,
                schema.FileName,
                schema.Source,
                file.Length,
                outcome.Rows.Count - rejected,
                outcome.Rejections.Count + rejected,
                null)
            {
                DetectedColumns = match.DetectedColumns,
                RejectionReasons = rejected == 0
                    ? outcome.Reasons
                    : [.. outcome.Reasons, new RejectionReason(Notice.Of(NoticeCodes.RowNotConvertible), rejected, [])],
            };
        }
        catch (SourceSchemaException ex)
        {
            seen.Remove(schema.FileName);

            return new PublishedFileOutcome(
                clientName,
                schema.FileName,
                schema.Source,
                file.Length,
                0,
                0,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)))
            {
                DetectedColumns = match.DetectedColumns,
                Candidates = _catalog.ExpectedFileNames,
            };
        }
    }

    private async Task<SnapshotVersion> CommitAsync(
        ParsedLoad parsed,
        string label,
        string uploadedBy,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        var id = UploadManifest.NewId(capturedAt);

        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        db.Versions.Add(new SnapshotVersionRow
        {
            Id = id,
            CapturedAtUtc = capturedAt.UtcDateTime,
            Label = label,
            UploadedBy = uploadedBy,
            ApplicationCount = parsed.Eai.Count,
            ColumnMaps = ColumnMapStorage.Serialize(parsed.ColumnMaps),
        });

        parsed.AddTo(db, id);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await PruneAsync(db, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new SnapshotVersion
        {
            Id = id,
            CapturedAt = capturedAt,
            Label = label,
            Provider = DatabaseSnapshotProvider.ProviderName,
            ApplicationCount = parsed.Eai.Count,
            IsCurrent = true,
        };
    }

    /// <summary>El mapping de la carga vigente, guardado en su fila.</summary>
    public async Task<IReadOnlyList<SourceColumnMap>> GetCurrentColumnMapsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var stored = await db.Versions
            .OrderByDescending(v => v.CapturedAtUtc)
            .Select(v => v.ColumnMaps)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return ColumnMapStorage.Deserialize(stored);
    }

    /// <summary>
    /// Retencion declarativa: conserva las N cargas mas recientes. Con 0 no borra
    /// nada, que es el comportamiento del adaptador de archivos.
    /// </summary>
    private async Task PruneAsync(ModernizationDbContext db, CancellationToken cancellationToken)
    {
        if (retainVersions <= 0)
        {
            return;
        }

        var expired = await db.Versions
            .OrderByDescending(v => v.CapturedAtUtc)
            .Skip(retainVersions)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return;
        }

        await DeleteAsync<EaiRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<ArchitecturalValueRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<ProductStrategyRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<TechDebtRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<ScaFlawRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<OssScoreRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<MonitoringRecord>(db, expired, cancellationToken).ConfigureAwait(false);
        await DeleteAsync<ApimOnboardingRecord>(db, expired, cancellationToken).ConfigureAwait(false);

        await db.Rejections
            .Where(r => expired.Contains(r.VersionId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await db.Versions
            .Where(v => expired.Contains(v.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<int> DeleteAsync<TRecord>(
        ModernizationDbContext db,
        IReadOnlyList<string> versionIds,
        CancellationToken cancellationToken)
        where TRecord : class
        => db.Set<TRecord>()
            .Where(r => versionIds.Contains(EF.Property<string>(r, ModernizationDbContext.VersionId)))
            .ExecuteDeleteAsync(cancellationToken);

    private static string Megabytes(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Las filas de una carga, ya tipadas, antes de tocar la base.</summary>
    private sealed class ParsedLoad
    {
        public List<EaiRecord> Eai { get; } = [];

        public List<ArchitecturalValueRecord> ArchitecturalValue { get; } = [];

        public List<ProductStrategyRecord> ProductStrategy { get; } = [];

        public List<TechDebtRecord> TechDebt { get; } = [];

        public List<ScaFlawRecord> ScaFlaws { get; } = [];

        public List<OssScoreRecord> OssScore { get; } = [];

        public List<MonitoringRecord> Monitoring { get; } = [];

        public List<ApimOnboardingRecord> ApimOnboarding { get; } = [];

        public List<SourceRejectionRow> Rejections { get; } = [];

        /// <summary>Qué columna de cada archivo cargó cada campo.</summary>
        public List<SourceColumnMap> ColumnMaps { get; } = [];

        /// <summary>Convierte las filas leidas y devuelve cuantas no se pudieron convertir.</summary>
        public int Add(SourceSchema schema, CsvSourceReader.ReadOutcome outcome)
        {
            foreach (var rejection in outcome.Rejections)
            {
                Reject(schema, rejection.RowNumber, rejection.Reason, rejection.Key);
            }

            return schema.FileName switch
            {
                "eai_master.csv" => Convert(schema, outcome, Eai, FileRecordMappers.ToEai),
                "architectural_value_by_eai.csv" => Convert(schema, outcome, ArchitecturalValue, FileRecordMappers.ToArchitecturalValue),
                "product_strategy.csv" => Convert(schema, outcome, ProductStrategy, FileRecordMappers.ToProductStrategy),
                "tech_debt.csv" => Convert(schema, outcome, TechDebt, FileRecordMappers.ToTechDebt),
                "veracode_sca_open_flaws.csv" => Convert(schema, outcome, ScaFlaws, FileRecordMappers.ToScaFlaw),
                "oss_score_by_eai.csv" => Convert(schema, outcome, OssScore, FileRecordMappers.ToOssScore),
                "monitoreo.csv" => Convert(schema, outcome, Monitoring, FileRecordMappers.ToMonitoring),
                "apim_onboarded_eais.csv" => Convert(schema, outcome, ApimOnboarding, FileRecordMappers.ToApimOnboarding),
                _ => throw new SourceSchemaException(Notice.Of(NoticeCodes.ContractUnmapped, ("file", schema.FileName))),
            };
        }

        public void AddTo(ModernizationDbContext db, string versionId)
        {
            Attach(db, versionId, Eai);
            Attach(db, versionId, ArchitecturalValue);
            Attach(db, versionId, ProductStrategy);
            Attach(db, versionId, TechDebt);
            Attach(db, versionId, ScaFlaws);
            Attach(db, versionId, OssScore);
            Attach(db, versionId, Monitoring);
            Attach(db, versionId, ApimOnboarding);

            foreach (var rejection in Rejections)
            {
                rejection.VersionId = versionId;
                db.Rejections.Add(rejection);
            }
        }

        private int Convert<TRecord>(
            SourceSchema schema,
            CsvSourceReader.ReadOutcome outcome,
            List<TRecord> target,
            Func<CsvSourceReader.Row, TRecord?> map)
            where TRecord : class
        {
            var rejected = 0;

            foreach (var row in outcome.Rows)
            {
                var record = map(row);

                if (record is null)
                {
                    Reject(schema, row.Number, Notice.Of(NoticeCodes.RowNotConvertible), row.Text(schema.KeyColumn));
                    rejected++;
                    continue;
                }

                target.Add(record);
            }

            return rejected;
        }

        private void Reject(SourceSchema schema, int rowNumber, Notice reason, string? key)
            => Rejections.Add(new SourceRejectionRow
            {
                VersionId = string.Empty,
                Source = schema.Source,
                RowNumber = rowNumber,
                Reason = NoticeStorage.Serialize(reason),
                Key = key,
            });

        private static void Attach<TRecord>(ModernizationDbContext db, string versionId, List<TRecord> records)
            where TRecord : class
        {
            foreach (var record in records)
            {
                var entry = db.Add(record);
                entry.Property(ModernizationDbContext.VersionId).CurrentValue = versionId;
            }
        }
    }
}
