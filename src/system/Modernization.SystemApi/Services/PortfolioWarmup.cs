using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modernization.Domain.Sources;

namespace Modernization.SystemApi.Services;

/// <summary>Cada cuanto se revisa que scores e historia esten calculados. Cero lo apaga.</summary>
public sealed record PortfolioWarmupOptions(TimeSpan Interval)
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(15);
}

/// <summary>
/// Calcula en segundo plano los scores y la historia con las reglas vigentes.
/// </summary>
/// <remarks>
/// Publicar una carga o unas reglas deja frios los caches, y la primera pantalla que
/// pedia la tendencia pagaba el recalculo de todas las cargas. Este servicio hace ese
/// trabajo fuera de las peticiones: al arrancar, cuando se le pide (tras publicar) y
/// periodicamente, que es como otra replica se entera de reglas publicadas en otro
/// proceso. Con todo en cache, una pasada cuesta milisegundos.
/// <para>
/// Nunca tumba el proceso: sin cargas no hay nada que calentar, y cualquier otra falla
/// se registra y se reintenta en la siguiente pasada.
/// </para>
/// </remarks>
public sealed partial class PortfolioWarmup(
    PortfolioService portfolio,
    TimeProvider clock,
    PortfolioWarmupOptions options,
    ILogger<PortfolioWarmup> logger) : BackgroundService
{
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Pide una pasada cuanto antes. Varias peticiones seguidas se funden en una.</summary>
    public void Request() => _requests.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Interval <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Interval, clock);
        Task<bool>? tick = null;
        Task<bool>? request = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            await WarmOnceAsync(stoppingToken).ConfigureAwait(false);

            // Las dos esperas se conservan entre vueltas: crear una nueva en cada vuelta
            // acumularia esperas pendientes mientras nadie publica.
            tick ??= timer.WaitForNextTickAsync(stoppingToken).AsTask();
            request ??= _requests.Reader.WaitToReadAsync(stoppingToken).AsTask();

            var completed = await Task.WhenAny(tick, request).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested || completed.IsCanceled)
            {
                return;
            }

            if (ReferenceEquals(completed, tick))
            {
                if (!await tick.ConfigureAwait(false))
                {
                    return;
                }

                tick = null;
            }
            else
            {
                _requests.Reader.TryRead(out _);
                request = null;
            }
        }
    }

    private async Task WarmOnceAsync(CancellationToken stoppingToken)
    {
        var started = clock.GetTimestamp();

        try
        {
            await portfolio.WarmAsync(stoppingToken).ConfigureAwait(false);

            var elapsedMs = clock.GetElapsedTime(started).TotalMilliseconds;
            LogWarmed(logger, elapsedMs);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El proceso se esta deteniendo.
        }
        catch (NoSnapshotAvailableException)
        {
            // Todavia no hay cargas: nada que calentar.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = 5401, Level = LogLevel.Debug, Message = "Scores e historia calientes en {ElapsedMs:0} ms.")]
    private static partial void LogWarmed(ILogger logger, double elapsedMs);

    [LoggerMessage(EventId = 5402, Level = LogLevel.Warning, Message = "No se pudo calentar el portafolio; se reintenta en la siguiente pasada.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
