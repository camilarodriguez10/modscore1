namespace Modernization.Sources.Connectors;

/// <summary>
/// Las fuentes que se leen directamente de las bases de la organización, sin archivos
/// (sección <c>Connectors</c> de la configuración de la capa System, ADR 0037).
/// </summary>
/// <remarks>
/// Todo es configuración del despliegue: ninguna pantalla define una conexión ni una consulta.
/// Los secretos no van en appsettings sino en archivos montados desde el gestor de secretos:
/// una clave por archivo en <c>Secrets:Directory</c>
/// (<c>Connectors__Connections__{nombre}__ConnectionString</c>), o un archivo propio en
/// <see cref="ConnectionOptions.ConnectionStringFile"/> o <see cref="ConnectionOptions.PasswordFile"/>.
/// La guía para configurarlo está en <c>docs/database-connections.md</c>.
/// </remarks>
public sealed class ConnectorOptions
{
    public const string SectionName = "Connectors";

    /// <summary>Apagado por defecto: sin esto, solo se carga por archivo.</summary>
    public bool Enabled { get; init; }

    /// <summary>Segundos para abrir una conexión.</summary>
    public int ConnectTimeoutSeconds { get; init; } = 30;

    /// <summary>Segundos para que una consulta termine.</summary>
    public int CommandTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Cada cuánto se leen las fuentes y se publica una carga; null lo deja manual. Mínimo
    /// <see cref="MinPullInterval"/>. Con varias réplicas, solo una debe tenerlo.
    /// </summary>
    public TimeSpan? PullInterval { get; init; }

    /// <summary>El intervalo más corto que se acepta: cada lectura publica una versión.</summary>
    public static TimeSpan MinPullInterval { get; } = TimeSpan.FromMinutes(15);

    /// <summary>Las conexiones, por nombre: varias fuentes pueden compartir una.</summary>
    public Dictionary<string, ConnectionOptions> Connections { get; init; } = [];

    /// <summary>
    /// Las fuentes, por la raíz del nombre canónico de su contrato: <c>eai_master</c>,
    /// <c>architectural_value_by_eai</c>, <c>product_strategy</c>, <c>tech_debt</c>,
    /// <c>veracode_sca_open_flaws</c>, <c>oss_score_by_eai</c>, <c>monitoreo</c>,
    /// <c>apim_onboarded_eais</c>. Una fuente que no figura no se lee.
    /// </summary>
    public Dictionary<string, SourceOptions> Sources { get; init; } = [];
}

/// <summary>Una conexión a una base de datos de origen.</summary>
public sealed class ConnectionOptions
{
    /// <summary><c>SqlServer</c>, <c>PostgreSql</c>, <c>Oracle</c> u <c>Odbc</c>.</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// La cadena de conexión —servidor, base, modo de autenticación, TLS—. Sin contraseña si
    /// queda en appsettings: el secreto va en <see cref="PasswordFile"/>, o la cadena entera
    /// en <see cref="ConnectionStringFile"/> o en un archivo de <c>Secrets:Directory</c>.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>Archivo con la cadena completa, montado desde el gestor de secretos.</summary>
    public string? ConnectionStringFile { get; init; }

    /// <summary>Archivo con solo la contraseña; se agrega a la cadena al conectar.</summary>
    public string? PasswordFile { get; init; }

    /// <summary>
    /// Solo ODBC: el driver no expone si cifra y verifica el certificado, así que quien
    /// configura lo declara explícitamente después de comprobarlo en el DSN o el driver.
    /// </summary>
    public bool EncryptionEnforcedByDriver { get; init; }

    /// <summary>
    /// Solo en Development: permite conectar sin TLS verificado, por ejemplo a un contenedor
    /// local. Fuera de Development se ignora.
    /// </summary>
    public bool AllowInsecureInDevelopment { get; init; }
}

/// <summary>De dónde se lee una fuente: una vista o tabla, o una consulta.</summary>
public sealed class SourceOptions
{
    /// <summary>El nombre de la conexión en <see cref="ConnectorOptions.Connections"/>.</summary>
    public string Connection { get; init; } = string.Empty;

    /// <summary>Una vista o tabla, con su esquema si hace falta: <c>CMT.V_TECH_DEBT_APP_SCORE</c>.</summary>
    public string? Table { get; init; }

    /// <summary>
    /// Una consulta <c>SELECT</c> (o <c>WITH … SELECT</c>), para renombrar o filtrar columnas. Una
    /// sola sentencia y de solo lectura; lo recomendado es una vista y <see cref="Table"/>.
    /// </summary>
    public string? Query { get; init; }
}
