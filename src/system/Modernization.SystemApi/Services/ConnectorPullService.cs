using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Security;
using Modernization.Sources.Connectors;
using Modernization.SystemApi.Contracts;

namespace Modernization.SystemApi.Services;

/// <summary>
/// Lee las fuentes desde sus bases y publica la carga, igual que una carga de archivos:
/// invalida el portafolio, lo recalcula en segundo plano y deja el evento de auditoría
/// (ADR 0037).
/// </summary>
public sealed class ConnectorPullService(
    DatabaseSourcePuller puller,
    ISnapshotPublisher? publisher,
    PortfolioService portfolio,
    PortfolioWarmup warmup,
    TimeProvider clock,
    ILoggerFactory loggers)
{
    /// <summary>True si hay fuentes configuradas y un adaptador que admite cargas.</summary>
    public bool IsAvailable => puller.Enabled && publisher is not null;

    public ConnectorStatusResponse Describe() => new(
        puller.Enabled,
        IsAvailable,
        puller.PullInterval is { } interval ? (int)interval.TotalMinutes : null,
        [.. puller.Connections.Select(c => new ConnectorConnectionResponse(c.Name, c.Provider))],
        [.. puller.Sources.Select(s => new ConnectorSourceResponse(s.Source, s.FileName, s.Connection, s.Kind))]);

    public async Task<IReadOnlyList<ConnectionTestResponse>> TestAsync(string user, CancellationToken cancellationToken = default)
    {
        if (!puller.Enabled)
        {
            return [];
        }

        var checks = await puller.TestAsync(cancellationToken).ConfigureAwait(false);

        SecurityAudit.ConnectionsTested(SecurityAudit.CreateLogger(loggers), user, checks.Count, checks.Count(c => c.Error is not null));

        return [.. checks.Select(c => new ConnectionTestResponse(c.Name, c.Provider, c.Error is null, c.Error, c.ElapsedMilliseconds))];
    }

    public async Task<PullResult> PullAsync(string user, string? label, CancellationToken cancellationToken = default)
    {
        var audit = SecurityAudit.CreateLogger(loggers);

        if (!IsAvailable)
        {
            return new PullResult(PublishOutcome.Rejected(Notice.Of(NoticeCodes.ConnectorDisabled)), []);
        }

        // La etiqueta se guarda en el manifiesto y queda en la auditoría, como la de una carga.
        if (!UploadLabel.TryNormalize(label, out var normalized, out _))
        {
            return new PullResult(PublishOutcome.Rejected(Notice.Of(NoticeCodes.UploadLabelInvalid, ("max", UploadLabel.MaxLength))), []);
        }

        var now = clock.GetUtcNow();
        var result = await puller.PullAsync(
            publisher!,
            normalized ?? FormattableString.Invariant($"Database pull {now:yyyy-MM-dd HH:mm}"),
            user,
            now,
            cancellationToken).ConfigureAwait(false);

        if (result.Outcome is { Accepted: true, Version: { } version })
        {
            portfolio.Invalidate();
            warmup.Request();

            SecurityAudit.SourcesPulled(audit, user, version.Id, result.Sources.Count, result.Outcome.ApplicationCount);
        }
        else
        {
            // Si fallaron fuentes, esas son los errores; el aviso que las resume no cuenta aparte.
            var failedSources = result.Sources.Count(s => s.Error is not null);
            SecurityAudit.SourcesPullRejected(audit, user, failedSources > 0 ? failedSources : result.Outcome.Errors.Count);
        }

        return result;
    }
}

/// <summary>
/// La lectura periódica (<c>Connectors:PullInterval</c>). Con varias réplicas, solo una debe
/// tenerla: cada lectura publica una versión.
/// </summary>
public sealed partial class ConnectorSchedule(
    ConnectorPullService pulls,
    DatabaseSourcePuller puller,
    TimeProvider clock,
    ILogger<ConnectorSchedule> logger) : BackgroundService
{
    /// <summary>Quién figura como autor de las versiones que publica la lectura periódica.</summary>
    public const string Author = "connector-schedule";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (puller.PullInterval is not { } interval || !pulls.IsAvailable)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var result = await pulls.PullAsync(Author, null, stoppingToken).ConfigureAwait(false);

                if (result.Outcome.Accepted)
                {
                    LogPublished(logger, result.Outcome.Version?.Id ?? "-", result.Outcome.ApplicationCount);
                }
                else
                {
                    LogRejected(logger, string.Join(", ", result.Outcome.Errors.Select(e => e.Code)));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // La próxima pasada lo reintenta; el proceso sigue atendiendo.
                LogFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(EventId = 5511, Level = LogLevel.Information, Message = "Lectura periodica de bases publicada: version {VersionId}, {ApplicationCount} aplicaciones.")]
    private static partial void LogPublished(ILogger logger, string versionId, int applicationCount);

    [LoggerMessage(EventId = 5512, Level = LogLevel.Warning, Message = "Lectura periodica de bases rechazada: {Codes}.")]
    private static partial void LogRejected(ILogger logger, string codes);

    [LoggerMessage(EventId = 5513, Level = LogLevel.Error, Message = "La lectura periodica de bases fallo; se reintenta en la siguiente pasada.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
