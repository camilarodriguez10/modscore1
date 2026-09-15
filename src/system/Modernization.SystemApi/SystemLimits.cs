using Modernization.Sources.Files;

namespace Modernization.SystemApi;

/// <summary>Limites de recursos de la capa System.</summary>
public static class SystemLimits
{
    /// <summary>Cuerpo maximo de cualquier peticion salvo la carga.</summary>
    public const long DefaultMaxRequestBodyBytes = 1024 * 1024;

    /// <summary>La carga completa mas el margen de los encabezados multipart.</summary>
    public const long UploadMaxRequestBytes = UploadService.MaxTotalBytes + (1024 * 1024);
}
