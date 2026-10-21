using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Data.SqlClient;
using Modernization.Domain;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace Modernization.Sources.Connectors;

/// <summary>Crea la conexión de una fuente. Las pruebas lo reemplazan por una base local.</summary>
public interface IConnectorConnectionFactory
{
    /// <summary>La conexión, sin abrir, ya verificada contra las reglas de seguridad.</summary>
    /// <exception cref="ConnectorException">La configuración no cumple una regla; el mensaje no lleva secretos.</exception>
    DbConnection Create(string name, ConnectionOptions options);

    /// <summary>La consulta mínima para probar una conexión.</summary>
    string PingQuery(ConnectionOptions options);

    /// <summary>
    /// La sentencia que deja de solo lectura la transacción recién abierta, o null si el motor
    /// no la tiene: entonces la garantía es la cuenta de solo lectura.
    /// </summary>
    string? ReadOnlyStatement(ConnectionOptions options);
}

/// <summary>
/// Un problema de configuración o de conexión: un aviso para la pantalla (ADR 0017) y un
/// mensaje en inglés para el log. Ninguno de los dos lleva secretos.
/// </summary>
public sealed class ConnectorException : Exception
{
    public ConnectorException(Notice notice, string message, Exception? innerException = null)
        : base(message, innerException)
        => Notice = notice;

    public ConnectorException()
        : this("The connector failed.")
    {
    }

    public ConnectorException(string message)
        : this(Notice.Of(NoticeCodes.ConnectorConnectFailed), message)
    {
    }

    public ConnectorException(string message, Exception innerException)
        : this(Notice.Of(NoticeCodes.ConnectorConnectFailed), message, innerException)
    {
    }

    /// <summary>El aviso que ve quien carga: código estable y datos sin secretos.</summary>
    public Notice Notice { get; }
}

/// <summary>
/// Las conexiones de verdad: SQL Server, PostgreSQL, Oracle y ODBC, con TLS verificado y los
/// secretos leídos de archivos montados (ADR 0037).
/// </summary>
/// <remarks>
/// Reglas que se aplican a toda conexión, salvo <see cref="ConnectionOptions.AllowInsecureInDevelopment"/>
/// en Development:
/// <list type="bullet">
/// <item>SQL Server: <c>Encrypt=Mandatory</c> o <c>Strict</c> y <c>TrustServerCertificate=False</c>;
/// sin métodos de autenticación que necesiten a una persona (interactivo, código de dispositivo).</item>
/// <item>PostgreSQL: <c>SSL Mode=VerifyFull</c>. <c>VerifyCA</c> no basta: no comprueba que el
/// certificado sea del servidor al que se conecta.</item>
/// <item>Oracle: protocolo TCPS (TLS) en el descriptor o en la forma <c>tcps://</c>, sin apagar
/// la verificación del nombre (<c>SSL_SERVER_DN_MATCH</c>).</item>
/// <item>ODBC: <see cref="ConnectionOptions.EncryptionEnforcedByDriver"/> declarado.</item>
/// </list>
/// </remarks>
public sealed class SecureConnectionFactory(bool isDevelopment, int connectTimeoutSeconds) : IConnectorConnectionFactory
{
    /// <summary>Tope del archivo de un secreto: una cadena de conexión no pesa más.</summary>
    private const int MaxSecretFileBytes = 16 * 1024;

    /// <summary>Las formas de apagar la verificación del nombre en un descriptor de Oracle, sin espacios.</summary>
    private static readonly string[] DnMatchDisabled = ["SSL_SERVER_DN_MATCH=NO", "SSL_SERVER_DN_MATCH=FALSE", "SSL_SERVER_DN_MATCH=OFF"];

    /// <summary>Nombre con el que la aplicación se presenta al servidor, para su auditoría.</summary>
    public const string ApplicationName = "ModernizationScore";

    public DbConnection Create(string name, ConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var provider = ConnectorProviders.Normalize(options.Provider)
            ?? throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorProviderUnknown, ("connection", name), ("provider", options.Provider)),
                $"The connection '{name}' has the provider '{options.Provider}'. Valid providers: {string.Join(", ", ConnectorProviders.All)}.");

        var connectionString = ReadConnectionString(name, options);
        var password = options.PasswordFile is { Length: > 0 } file ? ReadSecret(name, file) : null;
        var insecureAllowed = isDevelopment && options.AllowInsecureInDevelopment;

        return provider switch
        {
            ConnectorProviders.SqlServer => SqlServer(name, connectionString, password, insecureAllowed),
            ConnectorProviders.PostgreSql => PostgreSql(name, connectionString, password, insecureAllowed),
            ConnectorProviders.Oracle => Oracle(name, connectionString, password, insecureAllowed),
            _ => Odbc(name, connectionString, password, insecureAllowed || options.EncryptionEnforcedByDriver),
        };
    }

    public string PingQuery(ConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ConnectorProviders.Normalize(options.Provider) == ConnectorProviders.Oracle ? "SELECT 1 FROM DUAL" : "SELECT 1";
    }

    public string? ReadOnlyStatement(ConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // SQL Server no tiene transacciones de solo lectura; ODBC depende del motor de atrás.
        return ConnectorProviders.Normalize(options.Provider) is ConnectorProviders.PostgreSql or ConnectorProviders.Oracle
            ? "SET TRANSACTION READ ONLY"
            : null;
    }

    private SqlConnection SqlServer(string name, string connectionString, string? password, bool insecureAllowed)
    {
        var builder = Parse(name, () => new SqlConnectionStringBuilder(connectionString));

        if (!insecureAllowed)
        {
            if (builder.Encrypt != SqlConnectionEncryptOption.Mandatory && builder.Encrypt != SqlConnectionEncryptOption.Strict)
            {
                throw TlsRequired(name, ConnectorProviders.SqlServer, "Encrypt=Mandatory or Encrypt=Strict");
            }

            if (builder.TrustServerCertificate)
            {
                throw TlsRequired(name, ConnectorProviders.SqlServer, "TrustServerCertificate=False, so the server certificate is validated");
            }
        }

        if (builder.Authentication is SqlAuthenticationMethod.ActiveDirectoryInteractive or SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
        {
            throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorAuthenticationInteractive, ("connection", name)),
                $"The connection '{name}' uses an authentication method that needs a person to sign in. Use a SQL login, Windows integrated security or a Microsoft Entra managed identity or service principal.");
        }

        if (password is not null)
        {
            builder.Password = password;
        }

        builder.ConnectTimeout = connectTimeoutSeconds;
        builder.ApplicationName = ApplicationName;
        builder.ApplicationIntent = ApplicationIntent.ReadOnly;

        return new SqlConnection(builder.ConnectionString);
    }

    private NpgsqlConnection PostgreSql(string name, string connectionString, string? password, bool insecureAllowed)
    {
        var builder = Parse(name, () => new NpgsqlConnectionStringBuilder(connectionString));

        if (!insecureAllowed && builder.SslMode is not SslMode.VerifyFull)
        {
            throw TlsRequired(name, ConnectorProviders.PostgreSql, "SSL Mode=VerifyFull, which also checks the server name");
        }

        if (password is not null)
        {
            builder.Password = password;
        }

        builder.Timeout = connectTimeoutSeconds;
        builder.ApplicationName = ApplicationName;

        return new NpgsqlConnection(builder.ConnectionString);
    }

    private OracleConnection Oracle(string name, string connectionString, string? password, bool insecureAllowed)
    {
        var builder = Parse(name, () => new OracleConnectionStringBuilder(connectionString));
        var dataSource = (builder.DataSource ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

        if (!insecureAllowed
            && !dataSource.Contains("PROTOCOL=TCPS", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("tcps://", StringComparison.OrdinalIgnoreCase))
        {
            throw TlsRequired(name, ConnectorProviders.Oracle, "the TCPS protocol in the Data Source (PROTOCOL=TCPS or tcps://host:port/service)");
        }

        // Con TCPS pero sin comprobar el nombre del servidor, cualquier certificado de la misma
        // CA serviría para hacerse pasar por él.
        if (!insecureAllowed
            && DnMatchDisabled.Any(value => dataSource.Contains(value, StringComparison.OrdinalIgnoreCase)))
        {
            throw TlsRequired(name, ConnectorProviders.Oracle, "SSL_SERVER_DN_MATCH=yes (or leave it out), so the server name is checked");
        }

        if (password is not null)
        {
            builder.Password = password;
        }

        builder.ConnectionTimeout = connectTimeoutSeconds;

        return new OracleConnection(builder.ConnectionString);
    }

    private static OdbcConnection Odbc(string name, string connectionString, string? password, bool encryptionDeclared)
    {
        if (!encryptionDeclared)
        {
            throw TlsRequired(
                name,
                ConnectorProviders.Odbc,
                "EncryptionEnforcedByDriver=true, once the DSN or driver is confirmed to encrypt and validate the server certificate");
        }

        var builder = Parse(name, () => new OdbcConnectionStringBuilder(connectionString));

        if (password is not null)
        {
            builder["PWD"] = password;
        }

        return new OdbcConnection(builder.ConnectionString);
    }

    private static string ReadConnectionString(string name, ConnectionOptions options)
    {
        if (options.ConnectionStringFile is { Length: > 0 } file)
        {
            return ReadSecret(name, file);
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorSecretMissing, ("connection", name)),
                $"The connection '{name}' has neither ConnectionString nor ConnectionStringFile.");
        }

        return options.ConnectionString;
    }

    /// <summary>
    /// Lee un secreto de un archivo montado. Ni la ruta ni el contenido salen en un mensaje: la
    /// ruta dice dónde está montado el gestor de secretos.
    /// </summary>
    private static string ReadSecret(string name, string path)
    {
        var missing = Notice.Of(NoticeCodes.ConnectorSecretMissing, ("connection", name));

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists || info.Length is 0 or > MaxSecretFileBytes)
            {
                throw new ConnectorException(
                    missing,
                    $"A secret file of the connection '{name}' is missing, empty or larger than {MaxSecretFileBytes} bytes.");
            }

            return File.ReadAllText(path).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new ConnectorException(missing, $"A secret file of the connection '{name}' cannot be read.", ex);
        }
    }

    private static TBuilder Parse<TBuilder>(string name, Func<TBuilder> create)
    {
        try
        {
            return create();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            // Sin la excepción del driver: su mensaje puede citar un valor de la cadena.
            throw new ConnectorException(
                Notice.Of(NoticeCodes.ConnectorConnectionStringInvalid, ("connection", name)),
                $"The connection string of '{name}' is not valid for its provider ({ex.GetType().Name}).");
        }
    }

    private static ConnectorException TlsRequired(string name, string provider, string requirement)
        => new(
            Notice.Of(NoticeCodes.ConnectorTlsRequired, ("connection", name), ("provider", provider)),
            $"The connection '{name}' must use verified TLS: set {requirement}.");
}

/// <summary>Los motores admitidos, con el nombre que se escribe en la configuración.</summary>
public static class ConnectorProviders
{
    public const string SqlServer = "SqlServer";
    public const string PostgreSql = "PostgreSql";
    public const string Oracle = "Oracle";
    public const string Odbc = "Odbc";

    public static IReadOnlyList<string> All { get; } = [SqlServer, PostgreSql, Oracle, Odbc];

    /// <summary>El nombre canónico, o null si no es uno admitido.</summary>
    public static string? Normalize(string? provider) => (provider ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "SQLSERVER" or "MSSQL" => SqlServer,
        "POSTGRESQL" or "POSTGRES" => PostgreSql,
        "ORACLE" => Oracle,
        "ODBC" => Odbc,
        _ => null,
    };
}
