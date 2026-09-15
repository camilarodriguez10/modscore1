import io, os
root = r"C:\Users\kmiro\Downloads\mod score"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    s = s.replace('\r\n', '\n')
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:110]!r}"
        s = s.replace(old, new, 1)
    if crlf:
        s = s.replace('\n', '\r\n')
    io.open(p, 'w', encoding='utf-8', newline='').write(s)
    print("ok", rel)

# ================================================================ CHANNEL

edit(r"src\channel\Modernization.ChannelApi\ViewModels\ScreenViewModels.cs", [
    ("public sealed record SessionView(string Name, string? Email, IReadOnlyList<string> Roles, bool IsAuthenticated);",
     """public sealed record SessionView(
    string Name,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsAuthenticated,
    SessionPermissionsView Permissions);

/// <summary>
/// Lo que la sesion permite, ya resuelto. La SPA lo usa para no ofrecer lo que el
/// servidor va a negar; la autorizacion real sigue en el servidor.
/// </summary>
public sealed record SessionPermissionsView(bool CanRead, bool FullPortfolio, bool CanUpload);"""),
])

edit(r"src\channel\Modernization.ChannelApi\Composition\IngestionComposer.cs", [
    ("using Microsoft.AspNetCore.Http;\n",
     "using System.Security.Claims;\nusing Microsoft.AspNetCore.Http;\nusing Modernization.ChannelApi.Auth;\n"),
    ("""public sealed class IngestionComposer(SystemApiClient system)
{
    /// <summary>Debe coincidir con el tope de la capa System.</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    private const long MaxTotalBytes = 256L * 1024 * 1024;

    public async Task<IngestionView> ComposeAsync(CancellationToken cancellationToken = default)
    {""",
     """public sealed class IngestionComposer(SystemApiClient system, ChannelPermissions permissions)
{
    public async Task<IngestionView> ComposeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {"""),
    ("""        var provider = capabilities.Provider;
        var uploadEnabled = capabilities.UploadEnabled;

        return new IngestionView(
            uploadEnabled,
            provider,
            uploadEnabled
                ? null
                : $"El adaptador activo es '{provider}', que sirve datos generados. "
                  + "La carga de archivos requiere el adaptador de archivos.",""",
     """        var provider = capabilities.Provider;

        // La pantalla no ofrece lo que el servidor va a negar: sin permiso, el
        // formulario no aparece y el motivo lo explica. La politica del endpoint
        // sigue siendo la que decide.
        var canUpload = permissions.CanUpload(user);
        var uploadEnabled = capabilities.UploadEnabled && canUpload;

        string? disabledReason = null;

        if (!capabilities.UploadEnabled)
        {
            disabledReason = $"El adaptador activo es '{provider}', que sirve datos generados. "
                + "La carga de archivos requiere el adaptador de archivos.";
        }
        else if (!canUpload)
        {
            disabledReason = "Tu usuario puede consultar las cargas pero no publicarlas. Publicar requiere "
                + "el rol 'ingestion.write' y alcance sobre todo el portafolio.";
        }

        return new IngestionView(
            uploadEnabled,
            provider,
            disabledReason,"""),
    ("""            WithDeltas(history),
            MaxFileBytes,
            MaxTotalBytes);""",
     """            WithDeltas(history),
            ChannelLimits.UploadMaxFileBytes,
            ChannelLimits.UploadMaxTotalBytes);"""),
])

# Los mensajes del motor ya no repiten la entrada del cliente.
edit(r"src\shared\Modernization.WhatIf\ScenarioLever.cs", [
    ("""            $"No existe la palanca '{key}'. Palancas validas: " +""",
     """            "Una de las palancas del escenario no existe. Palancas validas: " +"""),
])

edit(r"src\shared\Modernization.WhatIf\WhatIfEngine.cs", [
    ("""                $"El score objetivo debe estar entre 20 y 100; se recibio {targetScore}.");""",
     """                "El score objetivo debe estar entre 20 y 100.");"""),
])

# ================================================================ SYSTEM

edit(r"src\system\Modernization.SystemApi\Services\IngestionService.cs", [
    ("using Microsoft.AspNetCore.Http;\n",
     "using Microsoft.AspNetCore.Http;\nusing Modernization.Security;\n"),
    ("""    public async Task<UploadService.UploadOutcome> PublishAsync(
        IFormFileCollection files,
        string? label,
        CancellationToken cancellationToken = default)""",
     """    public async Task<UploadService.UploadOutcome> PublishAsync(
        IFormFileCollection files,
        string? label,
        string uploadedBy,
        CancellationToken cancellationToken = default)"""),
    ("""        var rejected = new List<string>();
        var incoming = new List<UploadService.IncomingFile>(files.Count);""",
     """        // La etiqueta se guarda en el manifiesto, se muestra y queda en la
        // auditoria: se valida igual que en la capa Channel.
        if (!UploadLabel.TryNormalize(label, out var normalizedLabel, out var labelError))
        {
            return new UploadService.UploadOutcome(false, null, [], [labelError!], 0);
        }

        var rejected = new List<string>();
        var incoming = new List<UploadService.IncomingFile>(files.Count);"""),
    ("""            string.IsNullOrWhiteSpace(label) ? $"Carga {now:dd/MM/yyyy HH:mm}" : label.Trim(),
            "usuario autenticado",""",
     """            normalizedLabel ?? $"Carga {now:dd/MM/yyyy HH:mm}",
            uploadedBy,"""),
])

edit(r"src\system\Modernization.Sources\Files\UploadService.cs", [
    ("""public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null)
{""",
     """public sealed class UploadService(string rootDirectory, SourceSchemaCatalog? catalog = null) : IDisposable
{"""),
    ("""    private readonly SourceSchemaCatalog _catalog = catalog ?? SourceSchemaCatalog.Default;
""",
     """    private readonly SourceSchemaCatalog _catalog = catalog ?? SourceSchemaCatalog.Default;

    /// <summary>
    /// Una publicacion a la vez. Dos cargas simultaneas de 256 MB duplican el disco
    /// y la memoria en uso, y ninguna gana nada con la otra.
    /// </summary>
    private readonly SemaphoreSlim _publishing = new(1, 1);

    public void Dispose() => _publishing.Dispose();
"""),
    ("""        // Staging: nada toca el directorio de versiones hasta que todo valide.
        var staging = Path.Combine(rootDirectory, ".staging", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(staging);

        try
        {
""",
     """        if (!await _publishing.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new UploadOutcome(
                false,
                null,
                [],
                ["Hay otra carga publicandose en este momento. Intenta de nuevo cuando termine."],
                0);
        }

        // Staging: nada toca el directorio de versiones hasta que todo valide.
        var staging = Path.Combine(rootDirectory, ".staging", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            Directory.CreateDirectory(staging);

"""),
    ("""        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    // -----------------------------------------------------------------------
""",
     """        finally
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
"""),
])

edit(r"src\system\Modernization.Sources\Files\CsvSourceReader.cs", [
    ("""    public const int MaxRows = 500_000;
""",
     """    public const int MaxRows = 500_000;

    /// <summary>
    /// Tope de caracteres por campo. Ninguna columna de las fuentes se acerca: un
    /// campo mayor es un archivo corrupto, o uno fabricado para agotar memoria.
    /// </summary>
    public const int MaxFieldLength = 4096;
"""),
    ("""    public ReadOutcome Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var configuration""",
     """    public ReadOutcome Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            return ReadCore(reader);
        }
        catch (MaxFieldSizeException ex)
        {
            throw new SourceSchemaException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{_schema.FileName}' tiene un campo de mas de {MaxFieldLength:N0} caracteres (fila {ex.Context?.Parser?.Row}). ")
                + "Ninguna columna de las fuentes necesita tanto: el archivo esta corrupto o no es el export esperado.",
                ex);
        }
        catch (CsvHelperException ex)
        {
            // Sin el mensaje de CsvHelper: incluye el contenido crudo de la fila.
            throw new SourceSchemaException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{_schema.FileName}' no se pudo leer como CSV (fila {ex.Context?.Parser?.Row})."),
                ex);
        }
    }

    private ReadOutcome ReadCore(TextReader reader)
    {
        var configuration"""),
    ("""            HasHeaderRecord = true,
        };""",
     """            HasHeaderRecord = true,
            MaxFieldSize = MaxFieldLength,
        };"""),
])

print("done")
