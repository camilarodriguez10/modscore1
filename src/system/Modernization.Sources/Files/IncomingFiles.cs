using System.Security.Cryptography;

namespace Modernization.Sources.Files;

/// <summary>
/// Lo que los dos publicadores hacen igual con un archivo entrante antes de identificarlo: su
/// huella, para no cargar dos veces el mismo archivo, y el nombre con que se guarda cuando una
/// fuente llega en varios (ADR 0034).
/// </summary>
public static class IncomingFiles
{
    private const int BufferSize = 81_920;

    /// <summary>Copia un archivo y devuelve su SHA-256 en hexadecimal, en una sola pasada.</summary>
    public static async Task<string> CopyAndHashAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>El SHA-256 de un archivo en hexadecimal.</summary>
    public static async Task<string> HashAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// El nombre con que se guarda el archivo número <paramref name="part"/> de una fuente: el
    /// primero lleva el nombre canónico, así una carga de un archivo por fuente queda igual que
    /// siempre; los siguientes, <c>apim_onboarded_eais.part2.csv</c>.
    /// </summary>
    public static string PartFileName(string canonicalFileName, int part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalFileName);

        return part <= 1
            ? canonicalFileName
            : $"{Path.GetFileNameWithoutExtension(canonicalFileName)}.part{part}{Path.GetExtension(canonicalFileName)}";
    }

    /// <summary>True si es un nombre de archivo plano, sin carpetas: lo único que se lee de un manifiesto.</summary>
    public static bool IsPlainFileName(string? name)
        => !string.IsNullOrWhiteSpace(name)
            && string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal)
            && name.IndexOfAny(['/', '\\', ':']) < 0
            && name != "."
            && name != ".."
            && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
}
