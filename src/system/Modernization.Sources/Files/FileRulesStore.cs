using System.Globalization;
using System.Text.Json;
using Modernization.Domain.Rules;

namespace Modernization.Sources.Files;

/// <summary>
/// Versiones de reglas en disco: un directorio por version, con un unico archivo.
/// </summary>
/// <remarks>
/// Mismo patron que las cargas. Se escribe primero en un directorio temporal y se
/// mueve de una vez al nombre de la version, asi que nunca queda una version a
/// medias; y como el movimiento falla si el destino ya existe, dos publicaciones que
/// calcularon el mismo identificador no pueden pisarse. El identificador se valida
/// por lista blanca antes de tocar cualquier ruta.
/// <para>
/// Por defecto vive dentro del directorio de cargas, en <c>.rules</c>: el proveedor de
/// archivos ignora los directorios sin manifiesto, y en despliegue las reglas quedan
/// en el mismo volumen persistente que los datos.
/// </para>
/// </remarks>
public sealed class FileRulesStore(string rootDirectory) : IRulesStore, IDisposable
{
    public const string DefaultDirectoryName = ".rules";

    private const string DocumentName = "version.json";

    private const string StagingPrefix = ".staging-";

    /// <summary>
    /// Una publicacion escribe su staging en milisegundos. Uno mas viejo que esto lo dejo
    /// un proceso interrumpido, y no pertenece a una publicacion en curso de otra replica.
    /// </summary>
    public static readonly TimeSpan StaleStagingAge = TimeSpan.FromMinutes(10);

    /// <summary>Una version pesa unas decenas de KB. Un archivo de megas no lo escribio este codigo.</summary>
    private const long MaxDocumentBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly SemaphoreSlim _publishing = new(1, 1);

    public string Kind => "File";

    public void Dispose() => _publishing.Dispose();

    public async Task<StoredRulesVersion?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        foreach (var id in Ids())
        {
            var version = await ReadAsync(id, cancellationToken).ConfigureAwait(false);

            if (version is not null)
            {
                return version;
            }
        }

        return null;
    }

    public Task<StoredRulesVersion?> GetAsync(string id, CancellationToken cancellationToken = default)
        => RulesVersionId.IsWellFormed(id)
            ? ReadAsync(id, cancellationToken)
            : Task.FromResult<StoredRulesVersion?>(null);

    public async Task<IReadOnlyList<StoredRulesVersion>> ListAsync(int max, CancellationToken cancellationToken = default)
    {
        var versions = new List<StoredRulesVersion>();

        foreach (var id in Ids())
        {
            if (versions.Count >= max)
            {
                break;
            }

            if (await ReadAsync(id, cancellationToken).ConfigureAwait(false) is { } version)
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    public async Task PublishAsync(
        StoredRulesVersion version,
        string? expectedLatestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!RulesVersionId.IsWellFormed(version.Id))
        {
            throw new ArgumentException("El identificador de version no tiene la forma vMayor.Menor.", nameof(version));
        }

        await _publishing.WaitAsync(cancellationToken).ConfigureAwait(false);

        CleanupStaleStaging(DateTimeOffset.UtcNow);

        // El nombre temporal no tiene forma de version: si un reinicio lo deja atras,
        // ninguna lectura lo confunde con una, y la proxima publicacion lo limpia.
        var staging = Path.Combine(
            rootDirectory,
            StagingPrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            if (!string.Equals(Ids().FirstOrDefault(), expectedLatestId, StringComparison.Ordinal))
            {
                throw new RulesStoreConflictException();
            }

            Directory.CreateDirectory(staging);

            await using (var stream = new FileStream(
                Path.Combine(staging, DocumentName),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, version, Json, cancellationToken).ConfigureAwait(false);
            }

            var destination = Path.Combine(rootDirectory, version.Id);

            if (Directory.Exists(destination))
            {
                throw new RulesStoreConflictException();
            }

            try
            {
                Directory.Move(staging, destination);
            }
            catch (IOException ex)
            {
                // Otro proceso sobre el mismo volumen gano la carrera por el nombre.
                throw new RulesStoreConflictException("La version ya existe.", ex);
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
    /// Borra los directorios de staging que dejo una publicacion interrumpida. Devuelve
    /// cuantos borro. Nunca toca un directorio con forma de version.
    /// </summary>
    public int CleanupStaleStaging(DateTimeOffset now)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        var removed = 0;

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory, StagingPrefix + "*"))
        {
            try
            {
                if (now - Directory.GetLastWriteTimeUtc(directory) < StaleStagingAge)
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
                removed++;
            }
            catch (IOException)
            {
                // Otra replica lo esta borrando o usando: queda para la proxima.
            }
            catch (UnauthorizedAccessException)
            {
                // Sin permiso sobre ese directorio: no bloquea la publicacion.
            }
        }

        return removed;
    }

    // -----------------------------------------------------------------------

    /// <summary>Los identificadores publicados, del mas reciente al mas antiguo.</summary>
    private List<string> Ids()
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateDirectories(rootDirectory)
            .Select(Path.GetFileName)
            .Where(RulesVersionId.IsWellFormed)
            .Select(name => name!)
            .OrderByDescending(name => name, Comparer<string>.Create(RulesVersionId.Compare))];
    }

    private async Task<StoredRulesVersion?> ReadAsync(string id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(rootDirectory, id, DocumentName);
        var file = new FileInfo(path);

        if (!file.Exists || file.Length > MaxDocumentBytes)
        {
            return null;
        }

        await using var stream = file.OpenRead();

        try
        {
            var version = await JsonSerializer.DeserializeAsync<StoredRulesVersion>(stream, Json, cancellationToken)
                .ConfigureAwait(false);

            // El contenido tiene que decir lo mismo que el directorio: una copia movida
            // a mano bajo otro nombre no se sirve como si fuera esa version.
            return version is not null && string.Equals(version.Id, id, StringComparison.Ordinal) ? version : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
