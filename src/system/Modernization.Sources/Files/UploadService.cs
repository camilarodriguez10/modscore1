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
/// </list>
/// </remarks>
public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null)
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

            var accepted = new Dictionary<string, SourceSchema>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var outcome = await StageAsync(file, staging, accepted, cancellationToken).ConfigureAwait(false);
                outcomes.Add(outcome);

                if (outcome.Error is not null)
                {
                    errors.Add(outcome.Error.With("file", outcome.ClientFileName));
                }
            }

            // El maestro EAI es la espina dorsal: sin el no hay portafolio.
            if (!accepted.ContainsKey("eai_master.csv"))
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
                var version = await CommitAsync(staging, label, uploadedBy, capturedAt, outcomes, cancellationToken)
                    .ConfigureAwait(false);

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

    // -----------------------------------------------------------------------

    /// <summary>
    /// Escribe un archivo en staging tras identificarlo y validarlo.
    /// </summary>
    /// <remarks>
    /// El archivo aterriza primero con un <b>nombre generado</b>: hasta leer el
    /// encabezado no se sabe que fuente es, y el nombre del cliente no participa ni
    /// en la decision ni en la ruta. Solo cuando el contrato queda identificado, el
    /// archivo se mueve a su nombre canonico.
    /// </remarks>
    private async Task<PublishedFileOutcome> StageAsync(
        IncomingSourceFile file,
        string staging,
        Dictionary<string, SourceSchema> accepted,
        CancellationToken cancellationToken)
    {
        var clientName = Path.GetFileName(file.ClientFileName);

        if (file.Length > MaxFileBytes)
        {
            return new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0,
                Notice.Of(NoticeCodes.FileTooLarge, ("size", Megabytes(file.Length)), ("max", Megabytes(MaxFileBytes))));
        }

        var scratch = Path.Combine(
            staging,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".part");

        await using (var input = file.OpenRead())
        await using (var output = System.IO.File.Create(scratch))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        var match = _matcher.MatchFile(scratch, clientName, file.AssignedFileName);

        if (match.Schema is null)
        {
            System.IO.File.Delete(scratch);

            return new PublishedFileOutcome(clientName, null, "-", file.Length, 0, 0, match.Problem)
            {
                DetectedColumns = match.DetectedColumns,
                Candidates = match.Candidates,
            };
        }

        var schema = match.Schema;

        if (!accepted.TryAdd(schema.FileName, schema))
        {
            System.IO.File.Delete(scratch);

            return new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0,
                Notice.Of(NoticeCodes.FileDuplicate, ("source", schema.Source)))
            {
                DetectedColumns = match.DetectedColumns,
            };
        }

        // El destino es el nombre canonico del contrato, dentro de staging.
        var destination = Path.Combine(staging, schema.FileName);
        System.IO.File.Move(scratch, destination, overwrite: true);

        try
        {
            using var reader = new StreamReader(destination);
            var outcome = new CsvSourceReader(schema).Read(reader);

            return new PublishedFileOutcome(
                clientName,
                schema.FileName,
                schema.Source,
                file.Length,
                outcome.Rows.Count,
                outcome.Rejections.Count,
                null)
            {
                DetectedColumns = match.DetectedColumns,
            };
        }
        catch (SourceSchemaException ex)
        {
            accepted.Remove(schema.FileName);
            System.IO.File.Delete(destination);

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
            Files = [.. outcomes.Where(o => o.CanonicalFileName is not null).Select(o => o.CanonicalFileName!)],
            UploadedBy = uploadedBy,
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

    private static string Megabytes(long bytes)
        => (bytes / 1024d / 1024d).ToString("0.#", CultureInfo.InvariantCulture);
}
