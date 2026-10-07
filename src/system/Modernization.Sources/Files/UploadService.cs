using System.Globalization;
using Modernization.Domain.Sources;
using Modernization.Domain;

namespace Modernization.Sources.Files;

/// <summary>
/// Recibe una carga de archivos, la valida y la publica como una version nueva.
/// </summary>
/// <remarks>
/// La carga es el punto mas peligroso de la aplicacion, asi que el flujo es
/// deliberadamente estricto:
/// <list type="number">
/// <item>El nombre que llega del cliente <b>nunca</b> se usa como ruta ni, desde
/// la Fase 10, para identificar la fuente: eso lo decide el encabezado del
/// archivo. Lo que se escribe es el nombre canonico del contrato.</item>
/// <item>Todo se escribe primero en un directorio temporal. Solo si el conjunto
/// completo valida y ensambla, se mueve a su version definitiva.</item>
/// <item>Una carga que no produce ninguna aplicacion se rechaza: reemplazar el
/// portafolio por uno vacio es peor que no cargar nada.</item>
/// <item>Desde el 2026-10-07 (ADR 0034) una fuente puede llegar en varios archivos —uno por
/// región, por ejemplo—, que se cargan juntos; dos archivos idénticos se rechazan, porque
/// sumarían dos veces lo mismo. Un libro de Excel (.xlsx) se convierte a CSV apenas llega, y
/// desde ahí los dos formatos siguen el mismo camino.</item>
/// </list>
/// </remarks>
public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null, int retainVersions = 0)
    : ISnapshotPublisher, IDisposable
{
    /// <summary>Maximo por archivo. Los topes son del dominio: todos los adaptadores comparten los mismos.</summary>
    public const long MaxFileBytes = PublishLimits.MaxFileBytes;

    /// <summary>Maximo por carga completa.</summary>
    public const long MaxTotalBytes = PublishLimits.MaxTotalBytes;

    /// <summary>Adaptador que publica.</summary>
    public string Provider => FileSnapshotProvider.ProviderName;

    private readonly SourceSchemaCatalog _catalog = catalog ?? SourceSchemaCatalog.Default;

    /// <summary>Identifica cada archivo por su encabezado, no por su nombre.</summary>
    private readonly SourceFileMatcher _matcher = new(catalog);

    /// <summary>
    /// Una publicacion a la vez. Dos cargas simultaneas de 256 MB duplican el disco
    /// y la memoria en uso, y ninguna gana nada con la otra.
    /// </summary>
    private readonly SemaphoreSlim _publishing = new(1, 1);

    public void Dispose() => _publishing.Dispose();

    /// <summary>
    /// Valida y publica una carga. Devuelve el resultado en lugar de lanzar: el
    /// usuario necesita ver que se acepto y que no, archivo por archivo.
    /// </summary>
    public async Task<PublishOutcome> PublishAsync(
        IReadOnlyList<IncomingSourceFile> files,
        string label,
        string uploadedBy,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var errors = new List<Notice>();
        var outcomes = new List<PublishedFileOutcome>(files.Count);

        if (files.Count == 0)
        {
            return new PublishOutcome(false, null, [], [Notice.Of(NoticeCodes.UploadNoFiles)], 0);
        }

        var totalBytes = files.Sum(f => f.Length);
        if (totalBytes > MaxTotalBytes)
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadTooLarge, ("size", Megabytes(totalBytes)), ("max", Megabytes(MaxTotalBytes)))],
                0);
        }

        if (!await _publishing.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadBusy)],
                0);
        }

        // Staging: nada toca el directorio de versiones hasta que todo valide.
        var staging = Path.Combine(rootDirectory, ".staging", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            Directory.CreateDirectory(staging);

            // Primero cada archivo por separado: se guarda, se identifica y se resuelve su
            // mapping. Después cada fuente con todos sus archivos, que se leen juntos.
            var verdicts = new PublishedFileOutcome?[files.Count];
            var staged = new List<StagedFile>(files.Count);
            var fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
            var partsPerSource = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var index = 0; index < files.Count; index++)
            {
                var (failure, file) = await StageAsync(files[index], staging, fingerprints, partsPerSource, cancellationToken)
                    .ConfigureAwait(false);

                if (failure is not null)
                {
                    verdicts[index] = failure;
                }
                else
                {
                    staged.Add(file! with { Index = index });
                }
            }

            foreach (var source in staged.GroupBy(s => s.Schema.FileName, StringComparer.Ordinal))
            {
                ReadSource([.. source], verdicts);
            }

            outcomes.AddRange(verdicts.Select(v => v!));

            foreach (var outcome in outcomes.Where(o => o.Error is not null))
            {
                errors.Add(outcome.Error!.With("file", outcome.ClientFileName));
            }

            // El maestro EAI es la espina dorsal: sin el no hay portafolio.
            if (!outcomes.Any(o => o.Error is null && o.CanonicalFileName == "eai_master.csv"))
            {
                // El maestro EAI es el inventario: sin el no hay portafolio que puntuar.
                errors.Add(Notice.Of(NoticeCodes.UploadMissingMaster, ("master", "eai_master.csv")));
            }

            if (errors.Count > 0)
            {
                return new PublishOutcome(false, null, outcomes, errors, 0);
            }

            try
            {
                var version = await CommitAsync(staging, label, uploadedBy, capturedAt, outcomes, staged, cancellationToken)
                    .ConfigureAwait(false);

                // La retencion se aplica despues de publicar: si la carga no llega a
                // existir, tampoco se borra ninguna anterior.
                PruneVersions();

                return new PublishOutcome(true, version, outcomes, [], version.ApplicationCount);
            }
            catch (SourceSchemaException ex)
            {
                // Un conjunto que no ensambla es un problema de datos, no del
                // servicio: se reporta con su motivo y el portafolio no cambia.
                return new PublishOutcome(false, null, outcomes, [ex.Notice ?? Notice.Of(NoticeCodes.UploadEmpty)], 0);
            }
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            _publishing.Release();
        }
    }

    /// <summary>
    /// Borra los directorios de staging que dejo una carga interrumpida, por
    /// ejemplo un reinicio a mitad de publicar. Devuelve cuantos borro.
    /// </summary>
    public int CleanupStaleStaging(DateTimeOffset now, TimeSpan olderThan)
    {
        var root = Path.Combine(rootDirectory, ".staging");

        if (!Directory.Exists(root))
        {
            return 0;
        }

        var removed = 0;

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (now.UtcDateTime - Directory.GetLastWriteTimeUtc(directory) > olderThan)
            {
                Directory.Delete(directory, recursive: true);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Retencion declarativa: conserva las N cargas mas recientes, igual que
    /// <c>Database:RetainVersions</c> en el adaptador de base. Con 0 no borra nada.
    /// Devuelve cuantas borro.
    /// </summary>
    /// <remarks>
    /// Solo borra directorios con manifiesto. El staging, el almacen de reglas y cualquier
    /// otra cosa que comparta el volumen quedan fuera, pase lo que pase con su nombre.
    /// </remarks>
    public int PruneVersions()
    {
        if (retainVersions <= 0 || !Directory.Exists(rootDirectory))
        {
            return 0;
        }

        var expired = Directory.EnumerateDirectories(rootDirectory)
            .Select(directory => (Directory: directory, Manifest: UploadManifest.TryRead(directory)))
            .Where(candidate => candidate.Manifest is not null)
            .OrderByDescending(candidate => candidate.Manifest!.CapturedAt)
            .ThenByDescending(candidate => candidate.Manifest!.Id, StringComparer.Ordinal)
            .Skip(retainVersions)
            .ToList();

        foreach (var (directory, _) in expired)
        {
            Directory.Delete(directory, recursive: true);
        }

        return expired.Count;
    }

    // -----------------------------------------------------------------------

    /// <summary>Un archivo guardado en staging, identificado y con su mapping resuelto.</summary>
    private sealed record StagedFile(
        int Index,
        string ClientName,
        long Bytes,
        SourceSchema Schema,
        string StoredAs,
        string StoredPath,
        SourceFileMatch Match,
        ColumnResolution Resolution);

    /// <summary>
    /// Escribe un archivo en staging tras identificarlo y resolver su mapping. Las filas se
    /// leen después, con los demás archivos de la misma fuente.
    /// </summary>
    /// <remarks>
    /// El archivo aterriza primero con un <b>nombre generado</b>: hasta leer el
    /// encabezado no se sabe que fuente es, y el nombre del cliente no participa ni
    /// en la decision ni en la ruta. Solo cuando el contrato queda identificado, el
    /// archivo se mueve a su nombre canonico, o al de su parte si la fuente ya tenía uno.
    /// </remarks>
    private async Task<(PublishedFileOutcome? Failure, StagedFile? Staged)> StageAsync(
        IncomingSourceFile file,
        string staging,
        Dictionary<string, string> fingerprints,
        Dictionary<string, int> partsPerSource,
        CancellationToken cancellationToken)
    {
        var clientName = Path.GetFileName(file.ClientFileName);

        if (file.Length > MaxFileBytes)
        {
            return (new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(NoticeCodes.FileTooLarge, ("size", Megabytes(file.Length)), ("max", Megabytes(MaxFileBytes)))), null);
        }

        // Un mapping es de una fuente concreta: sin la fuente asignada no hay contra qué
        // contrato validarlo.
        if (file.ColumnMap is not null && string.IsNullOrWhiteSpace(file.AssignedFileName))
        {
            return (new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(NoticeCodes.MappingNeedsSource)), null);
        }

        var scratch = Path.Combine(
            staging,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".part");

        string fingerprint;

        await using (var input = file.OpenRead())
        await using (var output = System.IO.File.Create(scratch))
        {
            fingerprint = await IncomingFiles.CopyAndHashAsync(input, output, cancellationToken).ConfigureAwait(false);
        }

        // Dos archivos de una fuente se suman: el mismo archivo dos veces duplicaría sus filas.
        if (!fingerprints.TryAdd(fingerprint, clientName))
        {
            System.IO.File.Delete(scratch);

            return (new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(NoticeCodes.FileIdentical, ("other", fingerprints[fingerprint]))), null);
        }

        // Un libro de Excel se convierte antes de mirarlo: desde aquí es un CSV más.
        if (XlsxSourceConverter.Detect(scratch) != IncomingFileKind.Text)
        {
            var converted = scratch + ".csv";

            try
            {
                XlsxSourceConverter.Convert(scratch, converted);
            }
            catch (SourceSchemaException ex)
            {
                System.IO.File.Delete(converted);

                return (new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                    ex.Notice ?? Notice.Of(NoticeCodes.XlsxUnreadable)), null);
            }
            finally
            {
                System.IO.File.Delete(scratch);
            }

            scratch = converted;
        }

        var match = _matcher.MatchFile(scratch, clientName, file.AssignedFileName);

        if (match.Schema is null)
        {
            System.IO.File.Delete(scratch);

            return (new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0, match.Problem)
            {
                DetectedColumns = match.DetectedColumns,
                Candidates = match.Candidates,
            }, null);
        }

        var schema = match.Schema;
        var part = partsPerSource.GetValueOrDefault(schema.FileName) + 1;
        var storedAs = IncomingFiles.PartFileName(schema.FileName, part);

        // El destino es el nombre canonico del contrato, o el de su parte, dentro de staging.
        var destination = Path.Combine(staging, storedAs);
        ColumnResolution resolution;

        try
        {
            resolution = ResolveColumns(scratch, schema, file.ColumnMap);

            if (!resolution.IsValid && file.ColumnMap is not null)
            {
                System.IO.File.Delete(scratch);

                var problem = resolution.Problems.Count == 1
                    ? resolution.Problems[0]
                    : resolution.Problems[0].With("more", resolution.Problems.Count - 1);

                return (new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0, problem)
                {
                    DetectedColumns = match.DetectedColumns,
                }, null);
            }

            if (file.ColumnMap is not null)
            {
                // Con un mapping, el archivo se guarda con el encabezado reescrito: dice por
                // si mismo que columna cargo cada campo, y releerlo no depende del mapping.
                using (var output = new StreamWriter(destination, append: false, new System.Text.UTF8Encoding(false)))
                {
                    ColumnMapping.Rewrite(new StreamReader(scratch), output, resolution);
                }

                System.IO.File.Delete(scratch);
            }
            else
            {
                System.IO.File.Move(scratch, destination, overwrite: true);
            }
        }
        catch (SourceSchemaException ex)
        {
            System.IO.File.Delete(scratch);
            System.IO.File.Delete(destination);

            return (new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)))
            {
                DetectedColumns = match.DetectedColumns,
            }, null);
        }

        partsPerSource[schema.FileName] = part;

        return (null, new StagedFile(0, clientName, file.Length, schema, storedAs, destination, match, resolution));
    }

    /// <summary>
    /// Lee juntos los archivos de una fuente y deja el veredicto de cada uno: sus filas, sus
    /// rechazos y por qué, con los números de fila de su propio archivo.
    /// </summary>
    private void ReadSource(IReadOnlyList<StagedFile> parts, PublishedFileOutcome?[] verdicts)
    {
        var schema = parts[0].Schema;
        var read = new CsvSourceReader(schema).ReadParts(
            [.. parts.Select(p => new CsvSourceReader.SourcePart(() => new StreamReader(p.StoredPath), p.ClientName))]);

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var result = read.Parts[i];

            verdicts[part.Index] = result.Error is { } error
                ? new PublishedFileOutcome(part.ClientName, schema.FileName, schema.Source, part.Bytes, 0, 0, error)
                {
                    DetectedColumns = part.Match.DetectedColumns,
                    Candidates = _catalog.ExpectedFileNames,
                }
                : new PublishedFileOutcome(
                    part.ClientName,
                    schema.FileName,
                    schema.Source,
                    part.Bytes,
                    result.Outcome!.Rows.Count,
                    result.Outcome.Rejections.Count,
                    null)
                {
                    DetectedColumns = part.Match.DetectedColumns,
                    RejectionReasons = result.Outcome.Reasons,
                    IgnoredValues = result.Outcome.IgnoredValues,
                    ColumnMap = part.Resolution.ToColumnMap(),
                };
        }
    }

    /// <summary>
    /// Ensambla desde staging y, solo si produce un portafolio con aplicaciones,
    /// lo mueve a su version definitiva.
    /// </summary>
    private async Task<SnapshotVersion> CommitAsync(
        string staging,
        string label,
        string uploadedBy,
        DateTimeOffset capturedAt,
        IReadOnlyList<PublishedFileOutcome> outcomes,
        IReadOnlyList<StagedFile> staged,
        CancellationToken cancellationToken)
    {
        var id = UploadManifest.NewId(capturedAt);
        var probe = new SnapshotVersion
        {
            Id = id,
            CapturedAt = capturedAt,
            Label = label,
            Provider = FileSnapshotProvider.ProviderName,
            ApplicationCount = 0,
            IsCurrent = true,
        };

        // Se ensambla contra staging: si el conjunto no produce portafolio, la
        // version definitiva nunca llega a existir.
        var probeManifest = new UploadManifest
        {
            Id = Path.GetFileName(staging),
            Label = label,
            CapturedAt = capturedAt,
            ApplicationCount = 0,
            Files = [.. staged.Select(s => s.StoredAs)],
            UploadedBy = uploadedBy,
            ColumnMaps = [.. outcomes.Where(o => o.ColumnMap is not null).Select(o => o.ColumnMap!)],
            Parts = [.. staged.Select(s => new UploadPart(s.Schema.FileName, s.StoredAs, s.ClientName))],
        };
        probeManifest.Write(staging);

        var probeProvider = new FileSnapshotProvider(Path.GetDirectoryName(staging)!, _catalog);
        var snapshot = await probeProvider.GetAllAsync(probeManifest.Id, cancellationToken).ConfigureAwait(false);

        if (snapshot.Applications.Count == 0)
        {
            // Reemplazar el portafolio por uno vacio seria peor que no cargar nada.
            throw new SourceSchemaException(Notice.Of(NoticeCodes.UploadEmpty));
        }

        var destination = Path.Combine(rootDirectory, id);

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(rootDirectory);
        Directory.Move(staging, destination);

        var manifest = probeManifest with { Id = id, ApplicationCount = snapshot.Applications.Count };
        manifest.Write(destination);

        return probe with { ApplicationCount = snapshot.Applications.Count };
    }

    /// <summary>
    /// El mapping de la carga vigente: el manifiesto de la versión más reciente. Una
    /// versión anterior al registro del mapping no trae ninguno.
    /// </summary>
    public Task<IReadOnlyList<SourceColumnMap>> GetCurrentColumnMapsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(rootDirectory))
        {
            return Task.FromResult<IReadOnlyList<SourceColumnMap>>([]);
        }

        var current = Directory.EnumerateDirectories(rootDirectory)
            .Select(UploadManifest.TryRead)
            .Where(manifest => manifest is not null)
            .OrderByDescending(manifest => manifest!.CapturedAt)
            .FirstOrDefault();

        return Task.FromResult(current?.ColumnMaps ?? []);
    }

    /// <summary>
    /// Qué columna del archivo carga cada campo. Sin mapping es lo mismo que decide el
    /// lector por nombre o alias, y queda registrado igual: cada carga dice de qué columna
    /// salió cada valor.
    /// </summary>
    private static ColumnResolution ResolveColumns(string path, SourceSchema schema, IReadOnlyDictionary<string, string?>? map)
    {
        using var reader = new StreamReader(path);
        var headers = CsvSourceReader.ReadHeader(reader);

        return ColumnMapping.Resolve(schema, headers, map);
    }

    private static string Megabytes(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture);
}
