namespace Modernization.Domain.Sources;

/// <summary>
/// Publica una carga nueva. El adaptador de archivos escribe un directorio y lo
/// mueve de forma atomica; el de base de datos escribe una transaccion. Aguas
/// arriba nadie distingue los dos casos.
/// </summary>
/// <remarks>
/// Es el puerto gemelo de <see cref="IApplicationSnapshotProvider"/>: uno lee el
/// portafolio, el otro lo publica. Separarlos permite que la capa System exponga
/// la misma pantalla de cargas sin saber donde terminan los datos.
/// </remarks>
public interface ISnapshotPublisher
{
    /// <summary>Adaptador que publica: File o Database.</summary>
    string Provider { get; }

    /// <summary>
    /// Valida y publica una carga. Devuelve el resultado en lugar de lanzar: el
    /// usuario necesita ver que se acepto y que no, archivo por archivo.
    /// </summary>
    Task<PublishOutcome> PublishAsync(
        IReadOnlyList<IncomingSourceFile> files,
        string label,
        string uploadedBy,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Un archivo entrante. El nombre es el que envio el cliente y desde la Fase 10 ni
/// siquiera sirve para identificar la fuente —eso lo decide el encabezado—: jamas
/// se usa como ruta (ADR 0010).
/// </summary>
public sealed record IncomingSourceFile(string ClientFileName, long Length, Func<Stream> OpenRead)
{
    /// <summary>
    /// Fuente asignada a mano cuando el encabezado no distingue. Es un nombre
    /// canonico del catalogo, que el publicador valida contra la lista blanca: un
    /// valor inventado se rechaza en lugar de convertirse en una ruta.
    /// </summary>
    public string? AssignedFileName { get; init; }
}

/// <summary>Resultado de publicar una carga.</summary>
public sealed record PublishOutcome(
    bool Accepted,
    SnapshotVersion? Version,
    IReadOnlyList<PublishedFileOutcome> Files,
    IReadOnlyList<Notice> Errors,
    int ApplicationCount)
{
    public static PublishOutcome Rejected(params Notice[] errors)
        => new(false, null, [], errors, 0);

    public static PublishOutcome Rejected(IReadOnlyList<PublishedFileOutcome> files, IReadOnlyList<Notice> errors)
        => new(false, null, files, errors, 0);
}

/// <summary>Veredicto de un archivo dentro de la carga.</summary>
public sealed record PublishedFileOutcome(
    string ClientFileName,
    string? CanonicalFileName,
    string Source,
    long Bytes,
    int RowCount,
    int RejectedCount,
    Notice? Error)
{
    /// <summary>
    /// El encabezado que se leyo, cuando no se pudo identificar la fuente. Sin esto
    /// el usuario solo sabe que su archivo no sirve; con esto ve exactamente lo que
    /// el sistema leyo y puede asignarlo a mano.
    /// </summary>
    public IReadOnlyList<string> DetectedColumns { get; init; } = [];

    /// <summary>Fuentes a las que se puede asignar este archivo.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = [];
}

/// <summary>
/// Topes de tamano de una carga, iguales para todos los adaptadores. Viven en el
/// dominio para que el borde HTTP, el publicador de archivos y el de base de datos
/// no puedan discrepar.
/// </summary>
public static class PublishLimits
{
    /// <summary>Maximo por archivo. Un CSV mayor no es una carga, es un incidente.</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>Maximo por carga completa.</summary>
    public const long MaxTotalBytes = 256L * 1024 * 1024;
}
