using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Modernization.Domain;
using Modernization.Domain.Rules;
using Modernization.Rules;

namespace Modernization.SystemApi.Services;

/// <summary>Resultado de validar y simular un borrador.</summary>
public sealed record RulesSimulationOutcome(
    bool Valid,
    bool Stale,
    string ActiveVersion,
    IReadOnlyList<Notice> Errors,
    IReadOnlyList<Notice> Changes,
    RulesImpact? Impact);

/// <summary>Resultado de publicar: la version nueva, o por que no se publico.</summary>
public sealed record RulesPublishOutcome(
    bool Published,
    RulesVersionInfo? Version,
    IReadOnlyList<Notice> Errors,
    IReadOnlyList<Notice> Changes,
    RulesImpact? Impact)
{
    public static RulesPublishOutcome Rejected(IReadOnlyList<Notice> errors, IReadOnlyList<Notice>? changes = null)
        => new(false, null, errors, changes ?? [], null);
}

/// <summary>El almacen de reglas no respondio. Las reglas vigentes siguen puntuando.</summary>
public sealed class RulesStoreUnavailableException : Exception
{
    public RulesStoreUnavailableException() : base("El almacen de reglas no esta disponible.") { }

    public RulesStoreUnavailableException(string message) : base(message) { }

    public RulesStoreUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Las reglas vigentes de este proceso y su publicacion.
/// </summary>
/// <remarks>
/// <b>Sin redesplegar.</b> Las reglas se leen del almacen, no del ensamblado: publicar
/// reemplaza el modelo vigente en este proceso de inmediato, y el resto de las
/// replicas lo toma en la siguiente comprobacion (cada <c>Rules:RefreshSeconds</c>).
/// <para>
/// <b>Concurrencia optimista.</b> Un borrador se prepara sobre una version. Si al
/// publicar la vigente ya es otra, se rechaza en lugar de pisar el cambio ajeno; y el
/// almacen repite la comprobacion al escribir, para la carrera entre replicas.
/// </para>
/// <para>
/// <b>Nada se edita.</b> Publicar crea una version nueva; volver atras es publicar
/// otra vez lo que habia. Por eso una version leida se puede recordar: su contenido
/// no cambia nunca, y listar no vuelve a validar ni a construir motores.
/// </para>
/// <para>
/// <b>Almacen caido.</b> Si el almacen no responde se sigue puntuando con las reglas
/// que habia y se registra; publicar se rechaza con un aviso. Un almacen caido no
/// puede dejar sin scores al portafolio.
/// </para>
/// </remarks>
public sealed partial class RulesService : IDisposable
{
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(15);

    /// <summary>Modelos de versiones anteriores en memoria, para verlas y compararlas.</summary>
    private const int MaxCachedModels = 16;

    /// <summary>Tope de metadatos recordados. Una version pesa unos pocos KB de avisos.</summary>
    private const int MaxCachedInfos = 1024;

    /// <summary>Metadatos por version; null si la version guardada no se puede leer.</summary>
    private readonly ConcurrentDictionary<string, RulesVersionInfo?> _infos = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, RulesModel> _models = new(StringComparer.Ordinal);

    private readonly IRulesStore _store;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _refreshInterval;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private readonly SemaphoreSlim _publishing = new(1, 1);

    private volatile RulesModel _current = RulesModel.BuiltIn;
    private long _checkedAtTicks;

    public RulesService(
        IRulesStore store,
        TimeProvider? clock = null,
        TimeSpan? refreshInterval = null,
        ILogger<RulesService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _clock = clock ?? TimeProvider.System;
        _refreshInterval = refreshInterval ?? DefaultRefreshInterval;
        _logger = logger ?? NullLogger<RulesService>.Instance;
    }

    /// <summary>Donde se guardan las versiones: memoria, archivos o base de datos.</summary>
    public string StoreKind => _store.Kind;

    public void Dispose()
    {
        _refreshing.Dispose();
        _publishing.Dispose();
    }

    /// <summary>
    /// Las reglas vigentes. Consulta el almacen como mucho una vez por intervalo; entre
    /// consultas es una lectura de campo.
    /// </summary>
    public async ValueTask<RulesModel> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStale())
        {
            return _current;
        }

        return await RefreshAsync(force: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Las versiones publicadas, de la mas reciente a la de fabrica.</summary>
    public async Task<IReadOnlyList<RulesVersionInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StoredRulesVersion> stored;

        try
        {
            stored = await _store.ListAsync(RulesLimits.MaxListedVersions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            // Sin almacen se muestra lo que se sabe: la vigente y la de fabrica.
            LogStoreUnavailable(_logger, ex, _store.Kind);
            var current = _current;

            return current.Info.IsBuiltIn ? [current.Info] : [current.Info, RulesModel.BuiltIn.Info];
        }

        var versions = new List<RulesVersionInfo>(stored.Count + 1);

        foreach (var version in stored)
        {
            if (InfoOf(version) is { } info)
            {
                versions.Add(info);
            }
        }

        if (versions.Count < RulesLimits.MaxListedVersions)
        {
            versions.Add(RulesModel.BuiltIn.Info);
        }

        return versions;
    }

    /// <summary>Una version concreta, publicada o de fabrica. Null si no existe o no se puede leer.</summary>
    /// <exception cref="RulesStoreUnavailableException">El almacen no respondio.</exception>
    public async Task<RulesModel?> GetVersionAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.Equals(id, RulesModel.BuiltIn.Id, StringComparison.Ordinal))
        {
            return RulesModel.BuiltIn;
        }

        if (!RulesVersionId.IsWellFormed(id))
        {
            return null;
        }

        var current = _current;

        if (string.Equals(id, current.Id, StringComparison.Ordinal))
        {
            return current;
        }

        if (_models.TryGetValue(id, out var cached))
        {
            return cached;
        }

        StoredRulesVersion? stored;

        try
        {
            stored = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsStoreFailure(ex))
        {
            LogStoreUnavailable(_logger, ex, _store.Kind);
            throw new RulesStoreUnavailableException("El almacen de reglas no respondio.", ex);
        }

        return stored is null ? null : ModelOf(stored);
    }

    /// <summary>
    /// Valida un borrador contra las reglas vigentes y simula su impacto. Si la
    /// version base ya no es la vigente se simula igual, contra la vigente, y se dice.
    /// </summary>
    public async Task<RulesSimulationOutcome> SimulateAsync(
        string basedOn,
        RulesDraft draft,
        IReadOnlyList<ApplicationSnapshot> applications,
        DateOnly startDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(applications);

        var current = await RefreshAsync(force: true, cancellationToken).ConfigureAwait(false);
        var stale = !string.Equals(basedOn, current.Id, StringComparison.Ordinal);
        var edit = RulesEditor.Apply(current, draft);

        if (!edit.IsValid)
        {
            return new RulesSimulationOutcome(false, stale, current.Id, edit.Errors, [], null);
        }

        var impact = RulesImpactSimulator.Simulate(applications, current, edit.Candidate!, startDate);

        return new RulesSimulationOutcome(true, stale, current.Id, [], edit.Changes, impact);
    }

    /// <summary>
    /// Publica un borrador como version nueva. La validacion y la simulacion se
    /// repiten aqui: el impacto que queda registrado es el que calculo el servidor, no
    /// uno que envio el navegador.
    /// </summary>
    public async Task<RulesPublishOutcome> PublishAsync(
        string basedOn,
        string? reason,
        RulesDraft draft,
        string publishedBy,
        IReadOnlyList<ApplicationSnapshot> applications,
        DateOnly startDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(applications);

        // Una publicacion a la vez en este proceso. La segunda no espera: se le dice.
        if (!await _publishing.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return RulesPublishOutcome.Rejected([Notice.Of(NoticeCodes.RulesBusy)]);
        }

        try
        {
            var current = await RefreshAsync(force: true, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(basedOn, current.Id, StringComparison.Ordinal))
            {
                return RulesPublishOutcome.Rejected([Notice.Of(NoticeCodes.RulesStale, ("active", current.Id))]);
            }

            if (!RulesEditor.TryNormalizeReason(reason, out var normalizedReason))
            {
                return RulesPublishOutcome.Rejected([Notice.Of(
                    NoticeCodes.RulesReasonInvalid,
                    ("min", RulesLimits.MinReasonLength),
                    ("max", RulesLimits.MaxReasonLength))]);
            }

            var edit = RulesEditor.Apply(current, draft);

            if (!edit.IsValid)
            {
                return RulesPublishOutcome.Rejected(edit.Errors);
            }

            if (edit.Changes.Count == 0)
            {
                return RulesPublishOutcome.Rejected([Notice.Of(NoticeCodes.RulesNoChanges)]);
            }

            var impact = RulesImpactSimulator.Simulate(applications, current, edit.Candidate!, startDate);
            var now = _clock.GetUtcNow();

            var published = edit.Candidate!.WithInfo(new RulesVersionInfo
            {
                Id = RulesVersionId.Next(current.Id),
                BasedOn = current.Id,
                PublishedAt = now,
                PublishedBy = publishedBy,
                Reason = normalizedReason,
                Changes = edit.Changes,
                Impact = impact.Summary,
            });

            try
            {
                await _store
                    .PublishAsync(published.ToStored(), current.Info.IsBuiltIn ? null : current.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RulesStoreConflictException)
            {
                var winner = await RefreshAsync(force: true, cancellationToken).ConfigureAwait(false);
                return RulesPublishOutcome.Rejected([Notice.Of(NoticeCodes.RulesStale, ("active", winner.Id))]);
            }
            catch (Exception ex) when (IsStoreFailure(ex))
            {
                // No se sabe si quedo escrita: la proxima comprobacion lo dira. Lo que
                // no se hace es activarla en este proceso sin haberla guardado.
                LogStoreUnavailable(_logger, ex, _store.Kind);
                return RulesPublishOutcome.Rejected([Notice.Of(NoticeCodes.RulesStoreUnavailable)]);
            }

            _current = published;
            Remember(published);
            Interlocked.Exchange(ref _checkedAtTicks, now.UtcTicks);

            LogActivated(_logger, published.Id);

            return new RulesPublishOutcome(true, published.Info, [], edit.Changes, impact);
        }
        finally
        {
            _publishing.Release();
        }
    }

    // -----------------------------------------------------------------------

    private bool IsStale()
        => _clock.GetUtcNow().UtcTicks - Interlocked.Read(ref _checkedAtTicks) >= _refreshInterval.Ticks;

    private async Task<RulesModel> RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        await _refreshing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!force && !IsStale())
            {
                return _current;
            }

            StoredRulesVersion? latest;

            try
            {
                latest = await _store.GetLatestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsStoreFailure(ex))
            {
                // Se sigue puntuando con las que habia. La marca de comprobacion avanza
                // igual, para no insistir contra un almacen caido en cada peticion.
                LogStoreUnavailable(_logger, ex, _store.Kind);
                Interlocked.Exchange(ref _checkedAtTicks, _clock.GetUtcNow().UtcTicks);
                return _current;
            }

            var current = _current;

            // Un almacen vacio deja las reglas de fabrica. Uno que dejo de estar vacio
            // no vuelve atras solo: si la ultima version no se puede leer, se sigue con
            // la que habia y se registra, en lugar de puntuar con reglas equivocadas.
            if (latest is not null
                && !string.Equals(latest.Id, current.Id, StringComparison.Ordinal)
                && ModelOf(latest) is { } model)
            {
                _current = model;
                LogActivated(_logger, latest.Id);
            }

            Interlocked.Exchange(ref _checkedAtTicks, _clock.GetUtcNow().UtcTicks);

            return _current;
        }
        finally
        {
            _refreshing.Release();
        }
    }

    /// <summary>Construye y recuerda el modelo de una version guardada. Null si no es valida.</summary>
    private RulesModel? ModelOf(StoredRulesVersion stored)
    {
        if (_models.TryGetValue(stored.Id, out var cached))
        {
            return cached;
        }

        if (_infos.TryGetValue(stored.Id, out var known) && known is null)
        {
            return null;
        }

        try
        {
            var model = RulesModel.FromStored(stored);
            Remember(model);
            return model;
        }
        catch (RulesException ex)
        {
            LogUnreadableVersion(_logger, ex, stored.Id);
            RememberInfo(stored.Id, null);
            return null;
        }
    }

    /// <summary>
    /// Los metadatos de una version. La primera vez se valida completa, igual que al
    /// activarla: una version que no se puede leer no aparece en la lista.
    /// </summary>
    private RulesVersionInfo? InfoOf(StoredRulesVersion stored)
        => _infos.TryGetValue(stored.Id, out var known) ? known : ModelOf(stored)?.Info;

    private void Remember(RulesModel model)
    {
        if (_models.Count >= MaxCachedModels)
        {
            foreach (var key in _models.Keys)
            {
                if (!string.Equals(key, _current.Id, StringComparison.Ordinal))
                {
                    _models.TryRemove(key, out _);
                    break;
                }
            }
        }

        _models[model.Id] = model;
        RememberInfo(model.Id, model.Info);
    }

    private void RememberInfo(string id, RulesVersionInfo? info)
    {
        if (_infos.Count < MaxCachedInfos)
        {
            _infos[id] = info;
        }
    }

    /// <summary>
    /// Fallas del almacen que no son del borrador ni de la concurrencia: disco, permisos,
    /// base de datos, tiempo de espera. Un error de programacion no entra aqui.
    /// </summary>
    private static bool IsStoreFailure(Exception exception)
        => exception is IOException or UnauthorizedAccessException or TimeoutException or DbException
            or InvalidOperationException;

    [LoggerMessage(EventId = 5303, Level = LogLevel.Error, Message = "El almacen de reglas {StoreKind} no responde; se mantienen las reglas vigentes.")]
    private static partial void LogStoreUnavailable(ILogger logger, Exception exception, string storeKind);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "Reglas vigentes: {VersionId}")]
    private static partial void LogActivated(ILogger logger, string versionId);

    [LoggerMessage(EventId = 5302, Level = LogLevel.Error, Message = "La version de reglas {VersionId} no se puede leer; se mantienen las vigentes.")]
    private static partial void LogUnreadableVersion(ILogger logger, Exception exception, string versionId);
}
