using System.Collections.Concurrent;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Rules;
using Modernization.Scoring.Engine;
using Modernization.Security;

namespace Modernization.SystemApi.Services;

/// <summary>
/// Carga el portafolio una sola vez, cachea los scores y aplica el alcance de quien
/// pregunta.
/// </summary>
/// <remarks>
/// El motor es puro y determinista, asi que recalcular es seguro pero innecesario:
/// para un mismo snapshot y un mismo ruleset el resultado no cambia.
/// <para>
/// <b>Consistencia.</b> El snapshot y su cache de scores viajan juntos en un unico
/// objeto de estado que se reemplaza de una vez, con un numero de generacion. Antes
/// eran dos campos sueltos: una peticion que calculaba sobre el snapshot anterior
/// podia escribir su score despues de que una carga limpiara el cache, y ese score
/// viejo quedaba servido como vigente.
/// </para>
/// <para>
/// <b>Reglas.</b> Desde la Fase 12 las reglas cambian sin redesplegar. Cada cache de
/// scores lleva la version de reglas con que se calculo y se descarta sola cuando la
/// vigente es otra; el snapshot, que no depende de las reglas, se conserva.
/// </para>
/// <para>
/// <b>Historia.</b> Una version publicada no cambia nunca, asi que sus puntos se
/// calculan una vez por version de reglas. Toda la serie se calcula con las reglas
/// vigentes: comparar una carga puntuada con v1.3 contra otra puntuada con v1.4
/// mezclaria un cambio de datos con un cambio de reglas (ADR 0018).
/// </para>
/// <para>
/// <b>Alcance.</b> Toda lectura exige un <see cref="PortfolioScope"/>. No hay
/// sobrecarga sin el: olvidarlo no compila (ADR 0012).
/// </para>
/// </remarks>
public sealed class PortfolioService(IApplicationSnapshotProvider provider, RulesService rules) : IDisposable
{
    /// <summary>Tope de versiones con puntos en memoria. Mas alla se calcula sin cachear.</summary>
    private const int MaxCachedVersions = 256;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<VersionPoint>>>> _history =
        new(StringComparer.Ordinal);

    private volatile PortfolioState? _state;
    private long _generation;

    public void Dispose() => _gate.Dispose();

    /// <summary>Las reglas con que se esta puntuando ahora.</summary>
    public ValueTask<RulesModel> GetRulesAsync(CancellationToken cancellationToken = default)
        => rules.GetCurrentAsync(cancellationToken);

    /// <summary>
    /// Descarta el portafolio cacheado. Se llama tras publicar una carga: los
    /// scores calculados corresponden a datos que ya no son los vigentes.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _state = null;
    }

    /// <summary>Versiones disponibles, de la mas reciente a la mas antigua.</summary>
    public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
        => provider.GetVersionsAsync(cancellationToken);

    /// <summary>El portafolio vigente, recortado al alcance.</summary>
    public async Task<SnapshotSet> GetSnapshotAsync(PortfolioScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);

        return scope.IsFull
            ? state.Snapshot
            : state.Snapshot with { Applications = [.. state.Snapshot.Applications.Where(a => Allows(scope, a))] };
    }

    /// <summary>
    /// El portafolio de una carga concreta, recortado al alcance. Con la version
    /// vacia es la vigente.
    /// </summary>
    /// <remarks>
    /// El identificador de version llega del cliente y en el adaptador de archivos
    /// es un directorio, asi que quien llama debe haberlo validado contra la lista
    /// de versiones publicadas antes de pasarlo.
    /// </remarks>
    public async Task<SnapshotSet> GetSnapshotAsync(
        string? versionId,
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (string.IsNullOrWhiteSpace(versionId))
        {
            return await GetSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        // Una version historica no se cachea: se consulta al mirar el linaje de una
        // carga vieja, no en cada pantalla.
        var snapshot = await provider.GetAllAsync(versionId, cancellationToken).ConfigureAwait(false);

        return scope.IsFull
            ? snapshot
            : snapshot with { Applications = [.. snapshot.Applications.Where(a => Allows(scope, a))] };
    }

    /// <summary>
    /// El portafolio que pasa el filtro, dentro del alcance.
    /// </summary>
    /// <remarks>
    /// Filtrar por tramo exige puntuar, asi que el filtro completo vive aqui y no
    /// repartido entre capas: si la pagina se filtrara en un sitio y los agregados
    /// en otro, ambos numeros dejarian de hablar del mismo conjunto. Una aplicacion
    /// cuyo dato no encaja en el ruleset no tiene tramo, y por eso desaparece
    /// cuando se filtra por uno.
    /// </remarks>
    public async Task<IReadOnlyList<ApplicationSnapshot>> FilterAsync(
        PortfolioScope scope,
        ApplicationFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(filter);

        var snapshot = await GetSnapshotAsync(scope, cancellationToken).ConfigureAwait(false);
        var matched = snapshot.Applications.Where(filter.MatchesAttributes).ToList();

        if (!filter.NeedsScores || matched.Count == 0)
        {
            return matched;
        }

        var scores = await GetAllScoresAsync(scope, cancellationToken).ConfigureAwait(false);
        var byEai = scores.ToDictionary(s => s.EaiCode, s => s.EffectiveScore, StringComparer.Ordinal);

        return [.. matched.Where(a =>
            byEai.TryGetValue(a.EaiCode, out var score) && ScoreBands.Matches(score, filter.Band))];
    }

    /// <summary>
    /// Una aplicacion del portafolio vigente. Fuera del alcance es indistinguible de
    /// inexistente: un 404 no confirma que la aplicacion exista.
    /// </summary>
    public async Task<ApplicationSnapshot?> FindAsync(
        string eaiCode,
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        var application = state.Snapshot.Find(eaiCode);

        return application is not null && Allows(scope, application) ? application : null;
    }

    /// <summary>
    /// Score de una aplicacion. Devuelve null si el EAI no existe o esta fuera de
    /// alcance; la excepcion del motor se propaga cuando el dato no encaja en el
    /// ruleset, porque eso es un problema de datos que hay que ver, no esconder.
    /// </summary>
    public async Task<ScoreBreakdown?> GetScoreAsync(
        string eaiCode,
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        var application = state.Snapshot.Find(eaiCode);

        if (application is null || !Allows(scope, application))
        {
            return null;
        }

        var model = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var cache = state.ScoresFor(model);

        return cache.Scores.GetOrAdd(application.EaiCode, _ => model.Engine.Calculate(application));
    }

    /// <summary>
    /// Scores del portafolio dentro del alcance. Las aplicaciones cuyo dato no encaja
    /// en el ruleset se omiten en lugar de tumbar la respuesta completa: un problema
    /// en una fila no puede dejar sin dashboard a las otras 249.
    /// </summary>
    public async Task<IReadOnlyList<ScoreBreakdown>> GetAllScoresAsync(
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        var model = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var cache = state.ScoresFor(model);
        var results = new List<ScoreBreakdown>(state.Snapshot.Applications.Count);

        foreach (var application in state.Snapshot.Applications)
        {
            if (!Allows(scope, application))
            {
                continue;
            }

            try
            {
                results.Add(cache.Scores.GetOrAdd(application.EaiCode, _ => model.Engine.Calculate(application)));
            }
            catch (RulesetException)
            {
                // Queda fuera del agregado. El detalle individual si expone el error.
            }
        }

        return results;
    }

    /// <summary>
    /// Evolucion del score de una aplicacion a lo largo de las cargas. Incluye el
    /// peso disponible de cada version: un salto de score con el mismo peso es
    /// merito real, y con mas peso puede ser solo mas evidencia.
    /// </summary>
    public async Task<IReadOnlyList<ScoreHistoryPoint>> GetHistoryAsync(
        string eaiCode,
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var model = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var versions = await OrderedVersionsAsync(model, cancellationToken).ConfigureAwait(false);
        var points = new List<ScoreHistoryPoint>(versions.Count);

        foreach (var version in versions)
        {
            var computed = await PointsOfAsync(model, version, cancellationToken).ConfigureAwait(false);
            var match = computed.FirstOrDefault(p => string.Equals(p.EaiCode, eaiCode, StringComparison.Ordinal));

            // El alcance se evalua con los atributos de cada version: si la
            // aplicacion cambio de LOB, sus puntos anteriores siguen al LOB de entonces.
            var visible = match is not null && scope.Allows(match.Lob, match.Country, match.Cio);

            points.Add(new ScoreHistoryPoint
            {
                VersionId = version.Id,
                Label = version.Label,
                CapturedAt = version.CapturedAt,
                Score = visible ? match!.Score : null,
                AvailableWeight = visible ? match!.Weight : null,
            });
        }

        return points;
    }

    /// <summary>Score promedio del portafolio dentro del alcance, en cada carga.</summary>
    public async Task<IReadOnlyList<ScoreHistoryPoint>> GetPortfolioHistoryAsync(
        PortfolioScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var model = await rules.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var versions = await OrderedVersionsAsync(model, cancellationToken).ConfigureAwait(false);
        var points = new List<ScoreHistoryPoint>(versions.Count);

        foreach (var version in versions)
        {
            var computed = await PointsOfAsync(model, version, cancellationToken).ConfigureAwait(false);
            var visible = computed
                .Where(p => p.Score is not null && scope.Allows(p.Lob, p.Country, p.Cio))
                .ToList();

            points.Add(new ScoreHistoryPoint
            {
                VersionId = version.Id,
                Label = version.Label,
                CapturedAt = version.CapturedAt,
                Score = visible.Count == 0
                    ? null
                    : Math.Round(visible.Average(p => p.Score!.Value), 2, MidpointRounding.AwayFromZero),
                AvailableWeight = visible.Count == 0
                    ? null
                    : Math.Round(visible.Average(p => p.Weight!.Value), 1, MidpointRounding.AwayFromZero),
            });
        }

        return points;
    }

    // -----------------------------------------------------------------------

    private static bool Allows(PortfolioScope scope, ApplicationSnapshot application)
        => scope.Allows(application.Lob, application.Country, application.Cio);

    /// <summary>La clave de cache de una carga puntuada con unas reglas concretas.</summary>
    private static string HistoryKey(RulesModel model, string versionId) => model.Id + "/" + versionId;

    private async Task<PortfolioState> GetStateAsync(CancellationToken cancellationToken)
    {
        var state = _state;
        if (state is not null)
        {
            return state;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            while (true)
            {
                state = _state;
                if (state is not null)
                {
                    return state;
                }

                var generation = Interlocked.Read(ref _generation);
                var loaded = new PortfolioState(
                    await provider.GetAllAsync(cancellationToken).ConfigureAwait(false));

                // Una carga publicada mientras se leia deja esta lectura obsoleta: se
                // descarta y se vuelve a leer, en lugar de fijarla como vigente.
                if (Interlocked.Read(ref _generation) == generation)
                {
                    _state = loaded;
                    return loaded;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<SnapshotVersion>> OrderedVersionsAsync(RulesModel model, CancellationToken cancellationToken)
    {
        var versions = await provider.GetVersionsAsync(cancellationToken).ConfigureAwait(false);

        // Una version retirada (runbook de carga defectuosa) sale tambien del cache, y
        // con ella los puntos calculados con reglas que ya no son las vigentes.
        if (!_history.IsEmpty)
        {
            var live = versions.Select(v => HistoryKey(model, v.Id)).ToHashSet(StringComparer.Ordinal);

            foreach (var key in _history.Keys)
            {
                if (!live.Contains(key))
                {
                    _history.TryRemove(key, out _);
                }
            }
        }

        return [.. versions.OrderBy(v => v.CapturedAt)];
    }

    private Task<IReadOnlyList<VersionPoint>> PointsOfAsync(
        RulesModel model,
        SnapshotVersion version,
        CancellationToken cancellationToken)
    {
        var key = HistoryKey(model, version.Id);

        if (_history.Count >= MaxCachedVersions && !_history.ContainsKey(key))
        {
            return ComputePointsAsync(model, version.Id);
        }

        var lazy = _history.GetOrAdd(
            key,
            _ => new Lazy<Task<IReadOnlyList<VersionPoint>>>(
                () => ComputePointsAsync(model, version.Id),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return AwaitCachedAsync(key, lazy, cancellationToken);
    }

    /// <summary>
    /// Espera un calculo compartido. Si fallo, sale del cache para que la proxima
    /// consulta lo reintente en lugar de heredar el error para siempre.
    /// </summary>
    private async Task<IReadOnlyList<VersionPoint>> AwaitCachedAsync(
        string key,
        Lazy<Task<IReadOnlyList<VersionPoint>>> lazy,
        CancellationToken cancellationToken)
    {
        var task = lazy.Value;

        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                _history.TryRemove(KeyValuePair.Create(key, lazy));
            }
        }
    }

    private async Task<IReadOnlyList<VersionPoint>> ComputePointsAsync(RulesModel model, string versionId)
    {
        // Sin el token del primer llamador: su cancelacion no puede envenenar un
        // calculo que comparten todos los que piden la misma version.
        var snapshot = await provider.GetAllAsync(versionId, CancellationToken.None).ConfigureAwait(false);
        var points = new List<VersionPoint>(snapshot.Applications.Count);

        foreach (var application in snapshot.Applications)
        {
            decimal? score = null;
            decimal? weight = null;

            try
            {
                var breakdown = model.Engine.Calculate(application);
                score = breakdown.EffectiveScore;
                weight = breakdown.Denominator;
            }
            catch (RulesetException)
            {
                // Un dato que no encaja en el ruleset deja el punto vacio en vez de
                // romper la serie completa.
            }

            points.Add(new VersionPoint(
                application.EaiCode,
                application.Lob,
                application.Country,
                application.Cio,
                score,
                weight));
        }

        return points;
    }

    /// <summary>Lo minimo de una aplicacion en una version para dibujar su serie.</summary>
    private sealed record VersionPoint(
        string EaiCode,
        string? Lob,
        string? Country,
        string? Cio,
        decimal? Score,
        decimal? Weight);

    /// <summary>Los scores de un snapshot calculados con unas reglas concretas.</summary>
    private sealed class ScoreCache(string rulesId)
    {
        public string RulesId { get; } = rulesId;

        public ConcurrentDictionary<string, ScoreBreakdown> Scores { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Snapshot y scores de una misma generacion: se reemplazan juntos.</summary>
    private sealed class PortfolioState(SnapshotSet snapshot)
    {
        private ScoreCache? _scores;

        public SnapshotSet Snapshot { get; } = snapshot;

        /// <summary>
        /// El cache de las reglas pedidas. Si las vigentes cambiaron, se reemplaza: un
        /// score de la version anterior nunca se sirve como si fuera de la nueva.
        /// </summary>
        public ScoreCache ScoresFor(RulesModel model)
        {
            while (true)
            {
                var cache = _scores;

                if (cache is not null && string.Equals(cache.RulesId, model.Id, StringComparison.Ordinal))
                {
                    return cache;
                }

                var fresh = new ScoreCache(model.Id);

                if (ReferenceEquals(Interlocked.CompareExchange(ref _scores, fresh, cache), cache))
                {
                    return fresh;
                }
            }
        }
    }
}
