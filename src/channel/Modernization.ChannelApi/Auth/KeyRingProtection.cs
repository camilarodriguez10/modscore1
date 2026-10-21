using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Cifra en reposo las llaves de Data Protection con un certificado del gestor de secretos
/// (auditoría del 2026-10-21, ASVS 1.6.2).
/// </summary>
/// <remarks>
/// Las llaves cifran la cookie y el ticket de sesión. Persistidas en claro en un volumen, quien
/// lo lea puede forjar sesiones de cualquier persona. Por eso, fuera de Development, persistirlas
/// exige un certificado; sin él el proceso no arranca.
/// <list type="bullet">
/// <item><c>DataProtection:CertificateFile</c>: el certificado con su llave privada, como PFX
/// (<c>.pfx</c>/<c>.p12</c>, con <c>DataProtection:CertificatePasswordFile</c> si lleva
/// contraseña) o PEM (<c>.pem</c>/<c>.crt</c>, con la llave en el mismo archivo o en
/// <c>DataProtection:CertificateKeyFile</c>).</item>
/// <item><c>DataProtection:PreviousCertificateFiles</c>: los certificados anteriores, solo
/// para descifrar llaves viejas mientras dura una rotación.</item>
/// </list>
/// </remarks>
public static class KeyRingProtection
{
    public const string SectionName = "DataProtection";

    /// <summary>Tope de un archivo de certificado o de contraseña.</summary>
    private const long MaxFileBytes = 64 * 1024;

    /// <summary>
    /// Persiste las llaves en <c>DataProtection:KeysDirectory</c>, cifradas con el certificado.
    /// Sin directorio no hace nada: las llaves viven en memoria y mueren con el proceso.
    /// </summary>
    /// <exception cref="InvalidOperationException">Fuera de Development, se persisten sin certificado, o el certificado no sirve.</exception>
    public static void Configure(IDataProtectionBuilder dataProtection, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(SectionName);

        if (section["KeysDirectory"] is not { Length: > 0 } keysDirectory)
        {
            return;
        }

        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

        if (section["CertificateFile"] is not { Length: > 0 } certificateFile)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"'DataProtection:KeysDirectory' persiste las llaves de sesion (entorno '{environment.EnvironmentName}'), "
                    + "pero falta 'DataProtection:CertificateFile' para cifrarlas en reposo. Sin el, quien lea el volumen puede forjar sesiones.");
            }

            return;
        }

        var current = Load(certificateFile, section["CertificateKeyFile"], section["CertificatePasswordFile"]);
        var previous = (section.GetSection("PreviousCertificateFiles").Get<string[]>() ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Load(path, keyFile: null, passwordFile: null))
            .ToArray();

        dataProtection.ProtectKeysWithCertificate(current);
        dataProtection.UnprotectKeysWithAnyCertificate([current, .. previous]);
    }

    /// <summary>Carga un certificado con llave privada. Los mensajes no citan rutas ni contraseñas.</summary>
    internal static X509Certificate2 Load(string certificateFile, string? keyFile, string? passwordFile)
    {
        try
        {
            RequireReadable(certificateFile);

            var extension = Path.GetExtension(certificateFile);
            X509Certificate2 certificate;

            if (extension.Equals(".pem", StringComparison.OrdinalIgnoreCase) || extension.Equals(".crt", StringComparison.OrdinalIgnoreCase))
            {
                if (keyFile is { Length: > 0 })
                {
                    RequireReadable(keyFile);
                }

                certificate = X509Certificate2.CreateFromPemFile(certificateFile, keyFile is { Length: > 0 } ? keyFile : null);
            }
            else
            {
                string? password = null;

                if (passwordFile is { Length: > 0 })
                {
                    RequireReadable(passwordFile);
                    password = File.ReadAllText(passwordFile).Trim();
                }

                // En el contenedor la llave privada vive solo en memoria; en Windows (desarrollo)
                // los proveedores de llaves no admiten conjuntos efímeros para descifrar.
                certificate = X509CertificateLoader.LoadPkcs12FromFile(
                    certificateFile,
                    password,
                    OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
            }

            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException("The Data Protection certificate has no private key.");
            }

            return certificate;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The Data Protection certificate cannot be loaded ({ex.GetType().Name}). Check the file mounted from the secrets manager and its password.");
        }
    }

    private static void RequireReadable(string path)
    {
        var info = new FileInfo(path);

        if (!info.Exists || info.Length is 0 or > MaxFileBytes)
        {
            throw new IOException("A Data Protection certificate file is missing, empty or too large.");
        }
    }
}
