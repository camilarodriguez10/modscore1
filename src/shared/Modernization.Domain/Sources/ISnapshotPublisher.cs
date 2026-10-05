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

    /// <summary>
    /// Qué columna de cada archivo cargó cada campo en la carga vigente. El mapping de
    /// la próxima carga parte de aquí: los exports de una organización repiten sus
    /// nombres de columna mes a mes. Vacío si no hay cargas o si la vigente es anterior
    /// al registro del mapping.
    /// </summary>
    Task<IReadOnlyList<SourceColumnMap>> GetCurrentColumnMapsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SourceColumnMap>>([]);
}

/// <summary>
/// Qué columna del archivo carga cada campo de una fuente. Lo decide quien sube los
/// archivos antes de publicar, en la pantalla de mapping, y queda registrado con la carga.
/// </summary>
/// <param name="FileName">Nombre canónico de la fuente, p. ej. <c>eai_master.csv</c>.</param>
/// <param name="Columns">Un campo por entrada, con la columna del archivo que lo carga.</param>
public sealed record SourceColumnMap(string FileName, IReadOnlyList<ColumnAssignment> Columns);

/// <summary>
/// Un campo de la base y la columna del archivo que lo carga.
/// </summary>
/// <param name="Column">Nombre canónico del campo, tal como lo declara el contrato.</param>
/// <param name="Header">Columna del archivo; null si el archivo no la trae.</param>
/// <param name="Manual">True si la eligió una persona; false si se reconoció por nombre o alias.</param>
public sealed record ColumnAssignment(string Column, string? Header, bool Manual);

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

    /// <summary>
    /// Qué columna del archivo carga cada campo, elegido antes de publicar: campo canónico
    /// a columna del archivo, o a null si el archivo no la trae. Un campo que no figura se
    /// reconoce por nombre o alias como siempre. Exige <see cref="AssignedFileName"/>: un
    /// mapping es de una fuente concreta.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? ColumnMap { get; init; }
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

    /// <summary>
    /// Por qué se rechazaron filas, agrupado por problema. Sin esto la carga solo dice
    /// cuántas quedaron fuera, y quien no puede compartir el archivo no tiene cómo
    /// saber qué corregir.
    /// </summary>
    public IReadOnlyList<RejectionReason> RejectionReasons { get; init; } = [];

    /// <summary>
    /// Valores de campos opcionales que no se pudieron leer y quedaron sin dato, agrupados
    /// igual que los rechazos. Esas filas sí se cargaron.
    /// </summary>
    public IReadOnlyList<RejectionReason> IgnoredValues { get; init; } = [];

    /// <summary>Qué columna cargó cada campo, cuando el archivo se aceptó.</summary>
    public SourceColumnMap? ColumnMap { get; init; }
}

/// <summary>
/// Un problema de las filas rechazadas de un archivo: el aviso de la primera fila
/// que lo tuvo —columna, tipo esperado y el valor recibido, acotado—, cuántas filas
/// lo tienen y los números de las primeras.
/// </summary>
public sealed record RejectionReason(Notice Reason, int Rows, IReadOnlyList<int> SampleRows);

/// <summary>
/// Topes de tamano de una carga, iguales para todos los adaptadores. Viven en el
/// dominio para que el borde HTTP, el publicador de archivos y el de base de datos
/// no puedan discrepar.
/// </summary>
public static class PublishLimits
{
    /// <summary>
    /// Maximo por archivo. Un export real de SCA pesa 73 MB; con el heap de un pod de 1 GiB
    /// se midieron 490 mil filas (98 MB) sin problema, y el tope de filas del lector acota el
    /// resto (ADR 0030). Un CSV mayor no es una carga, es un incidente.
    /// </summary>
    public const long MaxFileBytes = 128L * 1024 * 1024;

    /// <summary>Maximo por carga completa.</summary>
    public const long MaxTotalBytes = 256L * 1024 * 1024;
}
