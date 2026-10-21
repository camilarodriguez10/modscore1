using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Modernization.ChannelApi.Auth;
using Modernization.SystemApi;

namespace Modernization.Api.Tests;

/// <summary>
/// Correcciones de la auditoría del 2026-10-21 (docs/security/audit-2026-10.md): las llaves de
/// sesión se cifran en reposo y una contraseña de los conectores solo llega desde un archivo
/// de secretos.
/// </summary>
public sealed class AuditHardeningTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-audit-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public AuditHardeningTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void PersistedSessionKeysWithoutACertificate_StopTheStartOutsideDevelopment()
    {
        var configuration = Configuration(("DataProtection:KeysDirectory", Path.Combine(_root, "keys")));

        var error = Assert.Throws<InvalidOperationException>(
            () => KeyRingProtection.Configure(new ServiceCollection().AddDataProtection(), configuration, new Environment(Environments.Production)));

        Assert.Contains("DataProtection:CertificateFile", error.Message, StringComparison.Ordinal);

        // En Development se admite, para un portátil.
        KeyRingProtection.Configure(new ServiceCollection().AddDataProtection(), configuration, new Environment(Environments.Development));
    }

    [Fact]
    public void PersistedSessionKeys_AreEncryptedWithTheCertificate_AndStillUnprotect()
    {
        var keys = Path.Combine(_root, "keys");
        var configuration = Configuration(
            ("DataProtection:KeysDirectory", keys),
            ("DataProtection:CertificateFile", WriteCertificate("dataprotection.pfx")));

        var protectedText = Protector(configuration).Protect("session ticket");

        var keyFile = File.ReadAllText(Assert.Single(Directory.GetFiles(keys, "*.xml")));
        Assert.DoesNotContain("unencrypted form", keyFile, StringComparison.Ordinal);
        Assert.Contains("encryptedSecret", keyFile, StringComparison.Ordinal);

        // Otra réplica, o el mismo proceso después de reiniciar, lee la sesión con el mismo certificado.
        Assert.Equal("session ticket", Protector(configuration).Unprotect(protectedText));
    }

    [Fact]
    public void AnUnreadableCertificate_NeverEchoesItsPath()
    {
        var missing = Path.Combine(_root, "vault-mount-secret", "dataprotection.pfx");
        var configuration = Configuration(
            ("DataProtection:KeysDirectory", Path.Combine(_root, "keys")),
            ("DataProtection:CertificateFile", missing));

        var error = Assert.Throws<InvalidOperationException>(
            () => KeyRingProtection.Configure(new ServiceCollection().AddDataProtection(), configuration, new Environment(Environments.Production)));

        Assert.DoesNotContain("vault-mount-secret", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Server=db;Database=cmt;User ID=reader;Password=hunter2;Encrypt=Strict")]
    [InlineData("Host=pg;Username=reader;PWD = hunter2;SSL Mode=VerifyFull")]
    public void APasswordInPlainConfiguration_StopsTheStartOutsideDevelopment(string connectionString)
    {
        var configuration = Configuration(("Connectors:Connections:cmt:ConnectionString", connectionString));

        var problem = Assert.Single(ConnectorSecretGuard.Validate(configuration, new Environment(Environments.Production)));

        Assert.Contains("Connectors:Connections:cmt:ConnectionString", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", problem, StringComparison.Ordinal);
        Assert.Empty(ConnectorSecretGuard.Validate(configuration, new Environment(Environments.Development)));
    }

    [Fact]
    public void APasswordFromTheSecretsDirectory_OrNoPasswordAtAll_IsAccepted()
    {
        var secrets = Path.Combine(_root, "secrets");
        Directory.CreateDirectory(secrets);
        File.WriteAllText(
            Path.Combine(secrets, "Connectors__Connections__cmt__ConnectionString"),
            "Server=db;Database=cmt;User ID=reader;Password=from-the-vault;Encrypt=Strict");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:Connections:sca:ConnectionString"] = "Host=pg;Username=reader;SSL Mode=VerifyFull",
                ["Connectors:Connections:sca:PasswordFile"] = "/mnt/secrets/sca-password",
            })
            .AddKeyPerFile(secrets, optional: false)
            .Build();

        Assert.Empty(ConnectorSecretGuard.Validate(configuration, new Environment(Environments.Production)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static IConfigurationRoot Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static IDataProtector Protector(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        KeyRingProtection.Configure(
            services.AddDataProtection().SetApplicationName("modernization-score-channel"),
            configuration,
            new Environment(Environments.Production));

        return services.BuildServiceProvider().GetDataProtector("audit-test");
    }

    /// <summary>Un certificado autofirmado con llave privada, exportado como PFX sin contraseña.</summary>
    private string WriteCertificate(string name)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=modscore-dataprotection-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx));
        return path;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Modernization.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
