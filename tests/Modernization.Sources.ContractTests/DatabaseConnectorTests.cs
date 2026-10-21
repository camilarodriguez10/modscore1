using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Modernization.Domain;
using Modernization.Sources.Connectors;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// ADR 0037: las conexiones solo se abren con TLS verificado y secretos montados, las consultas
/// son de solo lectura por forma, y una lectura entra por el mismo publicador que los archivos.
/// </summary>
public sealed class SecureConnectionFactoryTests
{
    private static readonly SecureConnectionFactory Production = new(isDevelopment: false, connectTimeoutSeconds: 20);

    [Theory]
    [InlineData("Server=db.test;Database=cmt;User ID=reader;Encrypt=False")]
    [InlineData("Server=db.test;Database=cmt;User ID=reader;Encrypt=Optional")]
    [InlineData("Server=db.test;Database=cmt;User ID=reader;Encrypt=Mandatory;TrustServerCertificate=True")]
    public void SqlServerWithoutVerifiedTls_IsRefused(string connectionString)
    {
        var error = Assert.Throws<ConnectorException>(() => Production.Create("cmt", SqlServer(connectionString)));

        Assert.Equal(NoticeCodes.ConnectorTlsRequired, error.Notice.Code);
        Assert.Equal("cmt", error.Notice.Args["connection"]);
    }

    [Theory]
    [InlineData("Server=db.test;Database=cmt;User ID=reader")] // SqlClient cifra y valida por defecto.
    [InlineData("Server=db.test;Database=cmt;User ID=reader;Encrypt=Mandatory")]
    [InlineData("Server=db.test;Database=cmt;User ID=reader;Encrypt=Strict")]
    [InlineData("Server=db.test;Database=cmt;Integrated Security=True;Encrypt=Mandatory")]
    [InlineData("Server=db.test;Database=cmt;Authentication=Active Directory Managed Identity;Encrypt=Strict")]
    [InlineData("Server=db.test;Database=cmt;Authentication=Active Directory Default;Encrypt=Mandatory")]
    public void SqlServerWithVerifiedTls_IsReadOnlyAndNamed(string connectionString)
    {
        using var connection = Production.Create("cmt", SqlServer(connectionString));
        var built = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection.ConnectionString);

        Assert.Equal(Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly, built.ApplicationIntent);
        Assert.Equal(SecureConnectionFactory.ApplicationName, built.ApplicationName);
        Assert.Equal(20, built.ConnectTimeout);
    }

    [Theory]
    [InlineData("Active Directory Interactive")]
    [InlineData("Active Directory Device Code Flow")]
    public void SqlServerSignInsThatNeedAPerson_AreRefused(string method)
    {
        var error = Assert.Throws<ConnectorException>(
            () => Production.Create("cmt", SqlServer($"Server=db.test;Database=cmt;Authentication={method};Encrypt=Strict")));

        Assert.Equal(NoticeCodes.ConnectorAuthenticationInteractive, error.Notice.Code);
    }

    [Theory]
    [InlineData("Host=pg.test;Database=sca;Username=reader")]
    [InlineData("Host=pg.test;Database=sca;Username=reader;SSL Mode=Prefer")]
    [InlineData("Host=pg.test;Database=sca;Username=reader;SSL Mode=Require")]
    public void PostgreSqlWithoutCertificateVerification_IsRefused(string connectionString)
    {
        var error = Assert.Throws<ConnectorException>(() => Production.Create("sca", Options(ConnectorProviders.PostgreSql, connectionString)));

        Assert.Equal(NoticeCodes.ConnectorTlsRequired, error.Notice.Code);
        Assert.Equal(ConnectorProviders.PostgreSql, error.Notice.Args["provider"]);
    }

    [Fact]
    public void PostgreSqlVerifyFull_TakesThePasswordFromItsFile()
    {
        using var secrets = new TempDirectory();
        var passwordFile = secrets.Write("sca-password", "s3cr3t-from-the-vault\n");

        using var connection = Production.Create("sca", new ConnectionOptions
        {
            Provider = "postgres",
            ConnectionString = "Host=pg.test;Database=sca;Username=reader;SSL Mode=VerifyFull",
            PasswordFile = passwordFile,
        });

        var built = new Npgsql.NpgsqlConnectionStringBuilder(connection.ConnectionString);

        Assert.Equal("s3cr3t-from-the-vault", built.Password);
        Assert.Equal(Npgsql.SslMode.VerifyFull, built.SslMode);
    }

    [Theory]
    [InlineData("User Id=reader;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=ora.test)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=CMT)))")]
    [InlineData("User Id=reader;Data Source=ora.test:1521/CMT")]
    public void OracleWithoutTcps_IsRefused(string connectionString)
    {
        var error = Assert.Throws<ConnectorException>(() => Production.Create("ora", Options(ConnectorProviders.Oracle, connectionString)));

        Assert.Equal(NoticeCodes.ConnectorTlsRequired, error.Notice.Code);
    }

    [Theory]
    [InlineData("User Id=reader;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL = TCPS)(HOST=ora.test)(PORT=2484))(CONNECT_DATA=(SERVICE_NAME=CMT)))")]
    [InlineData("User Id=reader;Data Source=tcps://ora.test:2484/CMT")]
    public void OracleOverTcps_IsAccepted(string connectionString)
    {
        using var connection = Production.Create("ora", Options(ConnectorProviders.Oracle, connectionString));

        Assert.Contains("TCPS", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Las formas de conexión que documenta docs/database-connections.md: cada driver las acepta.</summary>
    [Theory]
    [InlineData(ConnectorProviders.SqlServer, "Server=tcp:cmt.database.windows.net,1433;Database=cmt;Authentication=Active Directory Service Principal;User ID=00000000-0000-0000-0000-000000000000;Encrypt=Strict", true)]
    [InlineData(ConnectorProviders.SqlServer, "Server=tcp:cmt.database.windows.net,1433;Database=cmt;Authentication=Active Directory Workload Identity;Encrypt=Strict", false)]
    [InlineData(ConnectorProviders.SqlServer, "Server=sql01.corp.example;Database=cmt;Integrated Security=SSPI;Encrypt=Mandatory;HostNameInCertificate=sql01.corp.example", false)]
    [InlineData(ConnectorProviders.PostgreSql, "Host=pg.corp.example;Database=sca;Username=modscore_reader;SSL Mode=VerifyFull;Root Certificate=/mnt/pg/ca.crt;SSL Certificate=/mnt/pg/client.crt;SSL Key=/mnt/pg/client.key", false)]
    [InlineData(ConnectorProviders.PostgreSql, "Host=pg.corp.example;Database=sca;Username=modscore_reader@CORP.EXAMPLE;Kerberos Service Name=postgres;SSL Mode=VerifyFull", false)]
    [InlineData(ConnectorProviders.Oracle, "User Id=modscore_reader;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=ora.corp.example)(PORT=2484))(CONNECT_DATA=(SERVICE_NAME=CMT))(SECURITY=(SSL_SERVER_DN_MATCH=yes)))", true)]
    [InlineData(ConnectorProviders.Oracle, "User Id=/;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=adb.example.oraclecloud.com)(PORT=1522))(CONNECT_DATA=(SERVICE_NAME=cmt_high.adb.oraclecloud.com)));Wallet_Location=/mnt/oracle-wallet", false)]
    public void TheDocumentedConnectionForms_AreAcceptedByTheirDrivers(string provider, string connectionString, bool withPasswordFile)
    {
        using var secrets = new TempDirectory();

        using var connection = Production.Create("documented", new ConnectionOptions
        {
            Provider = provider,
            ConnectionString = connectionString,
            PasswordFile = withPasswordFile ? secrets.Write("password", "from-the-vault") : null,
        });

        Assert.NotNull(connection);

        if (withPasswordFile)
        {
            Assert.Contains("from-the-vault", connection.ConnectionString, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OdbcOnlyOpensWhenTheEncryptionWasDeclared()
    {
        var undeclared = Assert.Throws<ConnectorException>(() => Production.Create("dsn", Options(ConnectorProviders.Odbc, "DSN=TechDebt")));
        Assert.Equal(NoticeCodes.ConnectorTlsRequired, undeclared.Notice.Code);

        using var connection = Production.Create("dsn", new ConnectionOptions
        {
            Provider = ConnectorProviders.Odbc,
            ConnectionString = "DSN=TechDebt",
            EncryptionEnforcedByDriver = true,
        });

        Assert.Contains("DSN=TechDebt", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsecureConnections_AreOnlyAllowedInDevelopment()
    {
        var options = new ConnectionOptions
        {
            Provider = ConnectorProviders.PostgreSql,
            ConnectionString = "Host=localhost;Database=sca;Username=reader",
            AllowInsecureInDevelopment = true,
        };

        using var local = new SecureConnectionFactory(isDevelopment: true, 5).Create("local", options);
        Assert.NotNull(local);

        var deployed = Assert.Throws<ConnectorException>(() => Production.Create("local", options));
        Assert.Equal(NoticeCodes.ConnectorTlsRequired, deployed.Notice.Code);
    }

    [Fact]
    public void AnUnknownEngine_IsRefusedByName()
    {
        var error = Assert.Throws<ConnectorException>(() => Production.Create("x", Options("MySql", "Server=x")));

        Assert.Equal(NoticeCodes.ConnectorProviderUnknown, error.Notice.Code);
        Assert.Equal("MySql", error.Notice.Args["provider"]);
    }

    [Fact]
    public void AMissingSecretFile_NeverNamesItsPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "vault-mount-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), "cmt");

        var error = Assert.Throws<ConnectorException>(() => Production.Create("cmt", new ConnectionOptions
        {
            Provider = ConnectorProviders.SqlServer,
            ConnectionStringFile = path,
        }));

        Assert.Equal(NoticeCodes.ConnectorSecretMissing, error.Notice.Code);
        Assert.DoesNotContain("vault-mount", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(error.Notice.Args.Values, value => value.Contains("vault-mount", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInvalidConnectionString_NeverEchoesItsValues()
    {
        var error = Assert.Throws<ConnectorException>(
            () => Production.Create("cmt", SqlServer("Server=db.test;Password=hunter2-secret;Encrypt=Mandatory;Bogus Keyword=1")));

        Assert.Equal(NoticeCodes.ConnectorConnectionStringInvalid, error.Notice.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void ReadOnlyTransactions_AreUsedWhereTheEngineHasThem()
    {
        Assert.Equal("SET TRANSACTION READ ONLY", Production.ReadOnlyStatement(Options(ConnectorProviders.PostgreSql, "x")));
        Assert.Equal("SET TRANSACTION READ ONLY", Production.ReadOnlyStatement(Options(ConnectorProviders.Oracle, "x")));
        Assert.Null(Production.ReadOnlyStatement(Options(ConnectorProviders.SqlServer, "x")));
        Assert.Equal("SELECT 1 FROM DUAL", Production.PingQuery(Options(ConnectorProviders.Oracle, "x")));
    }

    private static ConnectionOptions SqlServer(string connectionString) => Options(ConnectorProviders.SqlServer, connectionString);

    private static ConnectionOptions Options(string provider, string connectionString)
        => new() { Provider = provider, ConnectionString = connectionString };
}

public sealed class SourceQueriesTests
{
    [Theory]
    [InlineData("V_TECH_DEBT", "SELECT * FROM V_TECH_DEBT")]
    [InlineData("CMT.V_TECH_DEBT", "SELECT * FROM CMT.V_TECH_DEBT")]
    [InlineData(" reporting.dbo.tech_debt ", "SELECT * FROM reporting.dbo.tech_debt")]
    public void ATable_IsReadWhole(string table, string expected)
        => Assert.Equal(expected, SourceQueries.StatementFor("tech_debt", new SourceOptions { Connection = "c", Table = table }));

    [Theory]
    [InlineData("V_TECH_DEBT; DROP TABLE X")]
    [InlineData("V_TECH_DEBT --")]
    [InlineData("[dbo].[V]")]
    [InlineData("a.b.c.d")]
    [InlineData("V_TECH DEBT")]
    public void ATableThatIsNotAPlainName_IsRefused(string table)
    {
        var error = Assert.Throws<ConnectorException>(
            () => SourceQueries.StatementFor("tech_debt", new SourceOptions { Connection = "c", Table = table }));

        Assert.Equal(NoticeCodes.ConnectorQueryInvalid, error.Notice.Code);
    }

    [Theory]
    [InlineData("SELECT APP_ID AS EAI_Code, COMBINED_APP_SCORE FROM CMT.TECH_DEBT", "SELECT APP_ID AS EAI_Code, COMBINED_APP_SCORE FROM CMT.TECH_DEBT")]
    [InlineData("  select * from t;  ", "select * from t")]
    [InlineData("WITH latest AS (SELECT * FROM t) SELECT * FROM latest", "WITH latest AS (SELECT * FROM t) SELECT * FROM latest")]
    public void AReadOnlyQuery_IsRunAsWritten(string query, string expected)
        => Assert.Equal(expected, SourceQueries.StatementFor("tech_debt", new SourceOptions { Connection = "c", Query = query }));

    [Theory]
    [InlineData("DELETE FROM t")]
    [InlineData("UPDATE t SET x = 1")]
    [InlineData("SELECT * FROM t; DELETE FROM t")]
    [InlineData("SELECT * INTO copy FROM t")]
    [InlineData("SELECT * FROM t FOR UPDATE")]
    [InlineData("SELECT * FROM t -- comment")]
    [InlineData("SELECT * FROM t /* comment */")]
    [InlineData("EXEC sp_who")]
    public void AnythingButASingleSelect_IsRefused(string query)
    {
        var error = Assert.Throws<ConnectorException>(
            () => SourceQueries.StatementFor("tech_debt", new SourceOptions { Connection = "c", Query = query }));

        Assert.Equal(NoticeCodes.ConnectorQueryInvalid, error.Notice.Code);
    }

    [Fact]
    public void TableAndQueryTogether_OrNeither_AreRefused()
    {
        Assert.Throws<ConnectorException>(() => SourceQueries.StatementFor("t", new SourceOptions { Connection = "c" }));
        Assert.Throws<ConnectorException>(() => SourceQueries.StatementFor("t", new SourceOptions { Connection = "c", Table = "T", Query = "SELECT 1" }));
    }
}

public sealed class ConnectorConfigurationTests
{
    [Fact]
    public void ADisabledSection_IsNeverChecked()
        => Assert.Empty(ConnectorConfiguration.Validate(new ConnectorOptions(), SourceSchemaCatalog.Default));

    [Fact]
    public void EveryProblem_IsReportedAtOnce()
    {
        var options = new ConnectorOptions
        {
            Enabled = true,
            PullInterval = TimeSpan.FromMinutes(1),
            Connections = new()
            {
                ["cmt"] = new ConnectionOptions { Provider = "MySql" },
            },
            Sources = new()
            {
                ["tech_debt"] = new SourceOptions { Connection = "cmt", Table = "V_TECH_DEBT" },
                ["tech_debt.csv"] = new SourceOptions { Connection = "cmt", Table = "V_TECH_DEBT" },
                ["payroll"] = new SourceOptions { Connection = "cmt", Table = "PAYROLL" },
                ["eai_master"] = new SourceOptions { Connection = "missing", Query = "DELETE FROM t" },
            },
        };

        var problems = ConnectorConfiguration.Validate(options, SourceSchemaCatalog.Default);

        Assert.Contains(problems, p => p.Contains("PullInterval", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("'MySql'", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("needs ConnectionString", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("payroll is not a source", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("'missing'", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("single read-only SELECT", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("same source twice: tech_debt", StringComparison.Ordinal));
    }

    [Fact]
    public void SourcesResolve_InCatalogOrder_WhateverTheCase()
    {
        var options = new ConnectorOptions
        {
            Enabled = true,
            Connections = new() { ["CMT"] = new ConnectionOptions { Provider = "SqlServer", ConnectionString = "x" } },
            Sources = new()
            {
                ["TECH_DEBT"] = new SourceOptions { Connection = "cmt", Table = "T" },
                ["eai_master.csv"] = new SourceOptions { Connection = "cmt", Table = "M" },
            },
        };

        var resolved = ConnectorConfiguration.Resolve(options, SourceSchemaCatalog.Default);

        Assert.Equal(["eai_master", "tech_debt"], resolved.Select(s => s.Key));
        Assert.All(resolved, s => Assert.Equal("CMT", s.ConnectionName));
    }
}

public sealed class CsvExtractTests
{
    [Theory]
    [InlineData(1.234, "1.2340")]
    [InlineData(97.67, "97.67")]
    [InlineData(650.00, "650")]
    public void Decimals_AreInvariant_AndNeverLookLikeThousands(double value, string expected)
        => Assert.Equal(expected, CsvExtract.Format("s", "c", (decimal)value));

    [Fact]
    public void ValuesAreWrittenAsAnExportWould()
    {
        Assert.Equal(string.Empty, CsvExtract.Format("s", "c", DBNull.Value));
        Assert.Equal("true", CsvExtract.Format("s", "c", true));
        Assert.Equal("14381", CsvExtract.Format("s", "c", 14381L));
        Assert.Equal("2026-10-21", CsvExtract.Format("s", "c", new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.Equal("2026-10-21T08:30:00", CsvExtract.Format("s", "c", new DateTime(2026, 10, 21, 8, 30, 0, DateTimeKind.Unspecified)));
        Assert.Equal("2026-10-21T13:30:00Z", CsvExtract.Format("s", "c", new DateTimeOffset(2026, 10, 21, 8, 30, 0, TimeSpan.FromHours(-5))));
        Assert.Equal("2026-10-21", CsvExtract.Format("s", "c", new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public void ABinaryColumn_IsRefusedByName()
    {
        var error = Assert.Throws<ConnectorException>(() => CsvExtract.Format("tech_debt", "BLOB", new byte[] { 1, 2 }));

        Assert.Equal(NoticeCodes.ConnectorColumnUnsupported, error.Notice.Code);
        Assert.Equal("BLOB", error.Notice.Args["column"]);
    }
}

/// <summary>
/// La lectura completa contra una base de verdad (SQLite) y el publicador de archivos: lo que
/// sale de las bases se valida y se publica igual que una carga.
/// </summary>
public sealed class DatabaseSourcePullerTests : IDisposable
{
    private readonly TempDirectory _work = new();
    private readonly string _database;
    private readonly string _versions;

    public DatabaseSourcePullerTests()
    {
        _database = Path.Combine(_work.Path, "sources.db");
        _versions = Path.Combine(_work.Path, "versions");
        Directory.CreateDirectory(_versions);

        SeedDatabase(_database, Path.Combine(_work.Path, "export"));
    }

    [Fact]
    public async Task EverySourceRead_IsPublishedAsOneUpload()
    {
        using var puller = Puller(AllSources());
        using var publisher = new UploadService(_versions);

        var result = await puller.PullAsync(publisher, "Database pull", "tests", DateTimeOffset.UtcNow);

        Assert.True(result.Outcome.Accepted, string.Join(" · ", result.Outcome.Errors));
        Assert.Equal(SourceSchemaCatalog.Default.Files.Count, result.Sources.Count);
        Assert.All(result.Sources, s => Assert.Null(s.Error));
        Assert.Equal(25, result.Outcome.ApplicationCount);
        Assert.Equal(result.Sources.Single(s => s.FileName == "eai_master.csv").Rows, result.Outcome.ApplicationCount);
    }

    [Fact]
    public async Task OneSourceThatCannotBeRead_PublishesNothing()
    {
        var sources = AllSources();
        sources["tech_debt"] = new SourceOptions { Connection = "local", Table = "NOT_A_TABLE" };

        using var puller = Puller(sources);
        using var publisher = new UploadService(_versions);

        var result = await puller.PullAsync(publisher, "Database pull", "tests", DateTimeOffset.UtcNow);

        Assert.False(result.Outcome.Accepted);
        Assert.Equal(NoticeCodes.ConnectorPullFailed, Assert.Single(result.Outcome.Errors).Code);
        Assert.Equal(NoticeCodes.ConnectorQueryFailed, result.Sources.Single(s => s.FileName == "tech_debt.csv").Error?.Code);
        Assert.DoesNotContain(Directory.EnumerateDirectories(_versions), d => File.Exists(Path.Combine(d, "manifest.json")));
    }

    [Fact]
    public async Task ATestOpensEachConnectionWithoutReadingData()
    {
        using var puller = Puller(AllSources());

        var check = Assert.Single(await puller.TestAsync());

        Assert.Equal("local", check.Name);
        Assert.Null(check.Error);
    }

    [Fact]
    public async Task ADisabledSection_NeverTouchesThePublisher()
    {
        using var puller = new DatabaseSourcePuller(
            new ConnectorOptions(),
            new SqliteConnections(_database),
            SourceSchemaCatalog.Default,
            NullLogger<DatabaseSourcePuller>.Instance);
        using var publisher = new UploadService(_versions);

        var result = await puller.PullAsync(publisher, "x", "tests", DateTimeOffset.UtcNow);

        Assert.Equal(NoticeCodes.ConnectorDisabled, Assert.Single(result.Outcome.Errors).Code);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _work.Dispose();
    }

    private DatabaseSourcePuller Puller(Dictionary<string, SourceOptions> sources) => new(
        new ConnectorOptions
        {
            Enabled = true,
            Connections = new() { ["local"] = new ConnectionOptions { Provider = "SqlServer", ConnectionString = "unused" } },
            Sources = sources,
        },
        new SqliteConnections(_database),
        SourceSchemaCatalog.Default,
        NullLogger<DatabaseSourcePuller>.Instance);

    private static Dictionary<string, SourceOptions> AllSources()
        => SourceSchemaCatalog.Default.Files.ToDictionary(
            f => ConnectorConfiguration.KeyOf(f.FileName),
            f => new SourceOptions { Connection = "local", Table = TableOf(f.FileName) });

    private static string TableOf(string fileName) => "SRC_" + ConnectorConfiguration.KeyOf(fileName).ToUpperInvariant();

    /// <summary>
    /// Una tabla por fuente con lo que exportaría el portafolio de ejemplo: los mismos encabezados
    /// que un archivo, todo como texto, como lo dejaría una vista de staging.
    /// </summary>
    private static void SeedDatabase(string database, string exportDirectory)
    {
        var exported = CsvSourceExporter.Export(DummyPortfolio.Generate(applicationCount: 25), exportDirectory);

        using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        connection.Open();

        foreach (var file in exported)
        {
            var lines = ReadCsv(Path.Combine(exportDirectory, file.FileName));
            var header = lines[0];
            var table = TableOf(file.FileName);

            using (var create = connection.CreateCommand())
            {
#pragma warning disable CA2100 // Nombres de columna del export de ejemplo, entre comillas; ningún dato externo.
                create.CommandText = $"CREATE TABLE {table} ({string.Join(", ", header.Select(h => $"\"{h.Replace("\"", "\"\"", StringComparison.Ordinal)}\" TEXT"))})";
#pragma warning restore CA2100
                create.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();

            foreach (var row in lines.Skip(1))
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
#pragma warning disable CA2100 // Solo parámetros: los valores nunca se concatenan.
                insert.CommandText = $"INSERT INTO {table} VALUES ({string.Join(", ", header.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture)))})";
#pragma warning restore CA2100

                for (var i = 0; i < header.Count; i++)
                {
                    insert.Parameters.AddWithValue("$p" + i.ToString(CultureInfo.InvariantCulture), i < row.Count && row[i].Length > 0 ? row[i] : DBNull.Value);
                }

                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private static List<List<string>> ReadCsv(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvHelper.CsvParser(reader, CultureInfo.InvariantCulture);
        var rows = new List<List<string>>();

        while (csv.Read())
        {
            rows.Add([.. csv.Record ?? []]);
        }

        return rows;
    }

    /// <summary>La base local de las pruebas en lugar de un servidor: misma lectura, mismo publicador.</summary>
    private sealed class SqliteConnections(string database) : IConnectorConnectionFactory
    {
        public DbConnection Create(string name, ConnectionOptions options) => new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");

        public string PingQuery(ConnectionOptions options) => "SELECT 1";

        public string? ReadOnlyStatement(ConnectionOptions options) => "PRAGMA query_only = ON";
    }
}

/// <summary>Un directorio temporal propio de cada prueba.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "modscore-tests-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string name, string content)
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, content);
        return file;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
