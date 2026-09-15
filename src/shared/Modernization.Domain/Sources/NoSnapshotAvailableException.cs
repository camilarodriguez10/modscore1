namespace Modernization.Domain.Sources;

/// <summary>
/// No hay ninguna version de datos todavia. No es un fallo del servicio: es el
/// estado inicial legitimo de un adaptador que se alimenta de cargas.
/// </summary>
/// <remarks>
/// Sin este tipo, el primer arranque del adaptador de archivos devuelve 500 en
/// todas las pantallas y el usuario no tiene forma de saber que lo unico que
/// falta es subir los archivos. Vive en el dominio, junto al puerto, porque el
/// adaptador de base de datos tiene exactamente el mismo estado inicial: una tabla
/// de snapshots vacia.
/// <para>
/// El mensaje va en ingles: termina en el <c>detail</c> de un 409 (ADR 0017).
/// </para>
/// </remarks>
public sealed class NoSnapshotAvailableException : Exception
{
    /// <summary>Mensaje comun a todos los adaptadores.</summary>
    public const string DefaultMessage =
        "No data upload has been published yet. Upload the source files to calculate the portfolio.";

    public NoSnapshotAvailableException(string message) : base(message) { }

    public NoSnapshotAvailableException(string message, Exception innerException)
        : base(message, innerException) { }

    public NoSnapshotAvailableException()
        : base(DefaultMessage) { }
}
