using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Sources.Files;
using Oracle.ManagedDataAccess.Client;

namespace Modernization.Sources.Connectors;

/// <summary>Una conexión configurada, sin su cadena ni sus secretos.</summary>
public sealed record ConnectorConnectionDescription(string Name, string Provider);

/// <summary>Una fuente configurada: de qué conexión sale y si es una vista o una consulta.</summary>
public sealed record ConnectorSourceDescription(string Source, string FileName, string Connection, string Kind);

/// <summary>El resultado de probar una conexión.</summary>
public sealed record ConnectionCheck(string Name, string Provider, Notice? Error, long ElapsedMilliseconds);

/// <summary>Lo que dejó la lectura de una fuente.</summary>
public sealed record SourcePullOutcome(
    string Source,
    string FileName,
    string Connection,
    int Rows,
    long Bytes,
    long ElapsedMilliseconds,
    Notice? Error);

/// <summary>Una lectura completa: la publicación, y el detalle de cada fuente.</summary>
public sealed record PullResult(PublishOutcome Outcome, IReadOnlyList<SourcePullOutcome> Sources);

/// <summary>
/// Lee las fuentes configuradas desde sus bases y las publica como una carga (ADR 0037).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Cada fuente se lee en una transacción que nunca se confirma, de solo lectura donde
/// el motor lo permite, con una cuenta que solo debe tener <c>SELECT</c>.</item>
/// <item>El resultado se escribe como CSV en un directorio temporal privado y entra por el
/// mismo publicador que una carga de archivos: misma validación, mismo mapping recordado,
/// misma publicación atómica.</item>
/// <item>Si una fuente falla, no se publica nada: una versión sin esa fuente se leería como
/// que la fuente dejó de reportar.</item>
/// <item>Una lectura a la vez por proceso.</item>
/// </list>
/// </remarks>
public sealed partial class DatabaseSourcePuller(
    ConnectorOptions options,
    IConnectorConnectionFactory factory,
    SourceSchemaCatalog catalog,
    ILogger<DatabaseSourcePuller> logger) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IReadOnlyList<ResolvedSource> _sources = ConnectorConfiguration.Resolve(options, catalog);

    /// <summary>True si la sección está encendida y hay al menos una fuente que leer.</summary>
    public bool Enabled => options.Enabled && _sources.Count > 0;

    public TimeSpan? PullInterval => options.PullInterval;

    public IReadOnlyList<ConnectorConnectionDescription> Connections
        => [.. options.Connections
            .Where(c => _sources.Any(s => s.ConnectionName == c.Key))
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ConnectorConnectionDescription(c.Key, ConnectorProviders.Normalize(c.Value.Provider) ?? c.Value.Provider))];

    public IReadOnlyList<ConnectorSourceDescription> Sources
        => [.. _sources.Select(s => new ConnectorSourceDescription(
            s.Schema.Source,
            s.Schema.FileName,
            s.ConnectionName,
            string.IsNullOrWhiteSpace(s.Options.Table) ? "query" : "table"))];

    /// <summary>Abre cada conexión y ejecuta la consulta mínima, sin leer datos.</summary>
    public async Task<IReadOnlyList<ConnectionCheck>> TestAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<ConnectionCheck>();

        foreach (var (name, connection) in options.Connections
            .Where(c => _sources.Any(s => s.ConnectionName == c.Key))
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
        {
            var watch = Stopwatch.StartNew();
            var provider = ConnectorProviders.Normalize(connection.Provider) ?? connection.Provider;
            Notice? error = null;

            try
            {
                await RunReadOnlyAsync(name, connection, factory.PingQuery(connection), async (command, ct) =>
                {
                    await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (Classify(ex, name, null, opened: false) is { } notice)
            {
                error = notice;
            }

            checks.Add(new ConnectionCheck(name, provider, error, watch.ElapsedMilliseconds));
        }

        return checks;
    }

    /// <summary>
    /// Lee todas las fuentes y, si todas se leyeron, las publica como una carga.
    /// </summary>
    public async Task<PullResult> PullAsync(
        ISnapshotPublisher publisher,
        string label,
        string requestedBy,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        if (!Enabled)
        {
            return new PullResult(PublishOutcome.Rejected(Notice.Of(NoticeCodes.ConnectorDisabled)), []);
        }

        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new PullResult(PublishOutcome.Rejected(Notice.Of(NoticeCodes.ConnectorBusy)), []);
        }

        var directory = CreatePrivateDirectory();

        try
        {
            var outcomes = new List<SourcePullOutcome>(_sources.Count);
            var files = new List<IncomingSourceFile>(_sources.Count);
            long total = 0;

            foreach (var source in _sources)
            {
                var path = Path.Combine(directory, source.Schema.FileName);
                var outcome = await ExtractAsync(source, path, Math.Min(PublishLimits.MaxFileBytes, PublishLimits.MaxTotalBytes - total), cancellationToken)
                    .ConfigureAwait(false);

                outcomes.Add(outcome);

                if (outcome.Error is null)
                {
                    total += outcome.Bytes;
                    files.Add(new IncomingSourceFile(source.Schema.FileName, outcome.Bytes, () => File.OpenRead(path))
                    {
                        AssignedFileName = source.Schema.FileName,
                    });
                }
            }

            var failed = outcomes.Count(o => o.Error is not null);

            if (failed > 0)
            {
                return new PullResult(
                    PublishOutcome.Rejected(Notice.Of(NoticeCodes.ConnectorPullFailed, ("count", failed), ("total", outcomes.Count))),
                    outcomes);
            }

            var published = await publisher.PublishAsync(files, label, requestedBy, capturedAt, cancellationToken).ConfigureAwait(false);

            return new PullResult(published, outcomes);
        }
        finally
        {
            DeleteQuietly(directory);
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<SourcePullOutcome> ExtractAsync(ResolvedSource source, string path, long maxBytes, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var opened = false;

        try
        {
            var statement = SourceQueries.StatementFor(source.Key, source.Options);
            CsvExtractResult? result = null;

            await RunReadOnlyAsync(source.ConnectionName, source.Connection, statement, async (command, ct) =>
            {
                opened = true;

                var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false);

                await using (reader.ConfigureAwait(false))
                {
                    result = await CsvExtract.WriteAsync(source.Key, reader, path, maxBytes, ct).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);

            LogSourceRead(logger, source.Key, source.ConnectionName, result!.Rows, watch.ElapsedMilliseconds);

            return new SourcePullOutcome(
                source.Schema.Source,
                source.Schema.FileName,
                source.ConnectionName,
                result.Rows,
                result.Bytes,
                watch.ElapsedMilliseconds,
                null);
        }
        catch (Exception ex) when (Classify(ex, source.ConnectionName, source.Key, opened) is { } notice)
        {
            return new SourcePullOutcome(
                source.Schema.Source,
                source.Schema.FileName,
                source.ConnectionName,
                0,
                0,
                watch.ElapsedMilliseconds,
                notice);
        }
    }

    /// <summary>
    /// Abre la conexión, empieza una transacción de solo lectura, ejecuta y la deshace: lo
    /// que se lee nunca se confirma, aunque la cuenta tuviera más permisos de los debidos.
    /// </summary>
    [SuppressMessage("Security", "CA2100", Justification = "Las sentencias salen de la configuración del despliegue o son constantes, y SourceQueries las valida; ningún valor llega de una petición.")]
    private async Task RunReadOnlyAsync(
        string name,
        ConnectionOptions connection,
        string statement,
        Func<DbCommand, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        var database = factory.Create(name, connection);

        await using (database.ConfigureAwait(false))
        {
            await database.OpenAsync(cancellationToken).ConfigureAwait(false);

            var transaction = await database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

            await using (transaction.ConfigureAwait(false))
            {
                if (factory.ReadOnlyStatement(connection) is { } readOnly)
                {
                    var setup = database.CreateCommand();

                    await using (setup.ConfigureAwait(false))
                    {
                        setup.Transaction = transaction;
                        setup.CommandText = readOnly;
                        await setup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                var command = database.CreateCommand();

                await using (command.ConfigureAwait(false))
                {
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    command.CommandTimeout = options.CommandTimeoutSeconds;

                    await run(command, cancellationToken).ConfigureAwait(false);
                }

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Un fallo como aviso para la pantalla, registrado en el log con lo que sirve para
    /// diagnosticar. Null para la cancelación de quien pidió la lectura, que sigue su curso.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Un driver puede lanzar tipos propios (de Azure, de Kerberos): ninguno debe tumbar la lectura sin registrarse.")]
    private Notice? Classify(Exception ex, string connection, string? source, bool opened)
    {
        switch (ex)
        {
            case ConnectorException connector:
                LogConnectorRejected(logger, connection, source ?? "-", connector.Notice.Code, connector.Message);
                return connector.Notice;

            case OperationCanceledException when ex is not TaskCanceledException { InnerException: TimeoutException }:
                // Cancelación de la petición o del apagado: no es un fallo de la fuente.
                return null;
        }

        var timeout = IsTimeout(ex);
        LogConnectorFailed(logger, ex, connection, source ?? "-", ex.GetType().Name);

        if (timeout)
        {
            return Notice.Of(NoticeCodes.ConnectorTimeout, ("connection", connection), ("source", source ?? string.Empty));
        }

        return opened && source is not null
            ? Notice.Of(NoticeCodes.ConnectorQueryFailed, ("connection", connection), ("source", source))
            : Notice.Of(NoticeCodes.ConnectorConnectFailed, ("connection", connection));
    }

    private static bool IsTimeout(Exception ex)
        => ex is TimeoutException
            || ex.InnerException is TimeoutException
            || (ex is SqlException sql && sql.Number == -2)
            || (ex is OracleException oracle && oracle.Number is 1013 or 12170);

    /// <summary>
    /// Un directorio temporal que solo el proceso puede leer: los CSV son el portafolio de la
    /// organización mientras dura la publicación.
    /// </summary>
    private static string CreatePrivateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "modscore-pull-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    private void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogCleanupFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = 5501, Level = LogLevel.Information, Message = "Fuente {Source} leida de {Connection}: {Rows} filas en {ElapsedMs} ms.")]
    private static partial void LogSourceRead(ILogger logger, string source, string connection, int rows, long elapsedMs);

    [LoggerMessage(EventId = 5502, Level = LogLevel.Warning, Message = "Conexion {Connection} rechazada para {Source}: {Code}. {Detail}")]
    private static partial void LogConnectorRejected(ILogger logger, string connection, string source, string code, string detail);

    [LoggerMessage(EventId = 5503, Level = LogLevel.Warning, Message = "Conexion {Connection} fallo para {Source} ({ExceptionType}).")]
    private static partial void LogConnectorFailed(ILogger logger, Exception exception, string connection, string source, string exceptionType);

    [LoggerMessage(EventId = 5504, Level = LogLevel.Warning, Message = "No se pudo borrar el directorio temporal de una lectura de bases.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
