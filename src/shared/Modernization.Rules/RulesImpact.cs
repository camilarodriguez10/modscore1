using Modernization.Domain;
using Modernization.Scoring.Engine;

namespace Modernization.Rules;

/// <summary>Lo esencial del impacto, que queda guardado con la version y en la auditoria.</summary>
public sealed record RulesImpactSummary(
    int Applications,
    int Up,
    int Down,
    int Unchanged,
    decimal? AverageBefore,
    decimal? AverageAfter,
    decimal? AverageDelta);

/// <summary>Cuantas aplicaciones hay en un tramo antes y despues del cambio.</summary>
public sealed record BandShift(string Band, int Before, int After);

/// <summary>Una aplicacion antes y despues. Null donde su dato no encaja en esas reglas.</summary>
public sealed record ApplicationShift(
    string EaiCode,
    string ApplicationName,
    decimal? Before,
    decimal? After,
    decimal? Delta,
    string? BandBefore,
    string? BandAfter);

/// <summary>
/// Que le hace un cambio de reglas al portafolio completo.
/// </summary>
public sealed record RulesImpact
{
    public required int Applications { get; init; }

    public required int Up { get; init; }

    public required int Down { get; init; }

    public required int Unchanged { get; init; }

    /// <summary>Puntuaban y dejan de hacerlo: su dato ya no cae en ninguna banda.</summary>
    public required int NoLongerScored { get; init; }

    /// <summary>No puntuaban y ahora si.</summary>
    public required int NewlyScored { get; init; }

    public decimal? AverageBefore { get; init; }

    public decimal? AverageAfter { get; init; }

    public decimal? AverageDelta { get; init; }

    public required IReadOnlyList<BandShift> Bands { get; init; }

    /// <summary>Las que mas se mueven, de mayor a menor.</summary>
    public required IReadOnlyList<ApplicationShift> Movers { get; init; }

    /// <summary>Score promedio al terminar cada plan, antes y despues.</summary>
    public decimal? ProjectedBefore { get; init; }

    public decimal? ProjectedAfter { get; init; }

    /// <summary>Alertas de coherencia en todo el portafolio, antes y despues.</summary>
    public required int AlertsBefore { get; init; }

    public required int AlertsAfter { get; init; }

    /// <summary>
    /// Todas las aplicaciones, antes y despues, en el orden de la carga. No viaja en la
    /// simulacion —son cientos de filas—: alimenta la exportacion del impacto.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ApplicationShift> Shifts { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public RulesImpactSummary Summary => new(Applications, Up, Down, Unchanged, AverageBefore, AverageAfter, AverageDelta);
}

/// <summary>
/// Lo que el impacto necesita de unas reglas sobre una carga: el score de cada
/// aplicacion y el resumen de sus planes.
/// </summary>
/// <remarks>
/// Del lado de las reglas vigentes no cambia mientras no cambien ni las reglas ni la
/// carga, asi que se calcula una vez y cada simulacion solo calcula el lado del
/// borrador. En la medicion de la revision era la mitad del costo de simular.
/// </remarks>
public sealed class RulesImpactBaseline
{
    private readonly decimal?[] _scores;

    private RulesImpactBaseline(
        IReadOnlyList<ApplicationSnapshot> applications,
        RulesModel model,
        DateOnly startDate,
        decimal?[] scores,
        decimal? projectedScore,
        int alerts)
    {
        Applications = applications;
        Model = model;
        StartDate = startDate;
        _scores = scores;
        ProjectedScore = projectedScore;
        Alerts = alerts;
    }

    public IReadOnlyList<ApplicationSnapshot> Applications { get; }

    public RulesModel Model { get; }

    public DateOnly StartDate { get; }

    /// <summary>Score promedio al terminar cada plan.</summary>
    public decimal? ProjectedScore { get; }

    /// <summary>Alertas de coherencia en todo el portafolio.</summary>
    public int Alerts { get; }

    /// <summary>
    /// True si sirve para estas reglas, esta carga y esta fecha. Compara instancias, no
    /// contenido: una carga nueva es otra lista aunque traiga las mismas aplicaciones.
    /// </summary>
    public bool IsFor(RulesModel model, IReadOnlyList<ApplicationSnapshot> applications, DateOnly startDate)
        => ReferenceEquals(Model, model) && ReferenceEquals(Applications, applications) && StartDate == startDate;

    internal decimal? ScoreAt(int index) => _scores[index];

    /// <summary>Calculo secuencial. Es el que usan las pruebas y quien no esta en un servidor.</summary>
    public static RulesImpactBaseline Compute(
        IReadOnlyList<ApplicationSnapshot> applications,
        RulesModel model,
        DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(model);

        var scores = new decimal?[applications.Count];

        for (var index = 0; index < applications.Count; index++)
        {
            scores[index] = RulesImpactSimulator.Score(model, applications[index]);
        }

        var plan = model.Analyzer.Summarize(applications, startDate);
        decimal? projected = plan.Groups.Count == 0 ? null : plan.Groups[0].AverageProjectedScore;

        return new RulesImpactBaseline(applications, model, startDate, scores, projected, plan.Alerts.Sum(a => a.Count));
    }

    /// <summary>
    /// Mismo resultado que <see cref="Compute"/>, con los scores en paralelo y el resumen
    /// de planes a la vez: son independientes entre si.
    /// </summary>
    public static async Task<RulesImpactBaseline> ComputeAsync(
        IReadOnlyList<ApplicationSnapshot> applications,
        RulesModel model,
        DateOnly startDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(model);

        var planTask = Task.Run(() => model.Analyzer.Summarize(applications, startDate), cancellationToken);
        var scores = RulesImpactSimulator.ScoreAll(model, applications, cancellationToken);
        var plan = await planTask.ConfigureAwait(false);
        decimal? projected = plan.Groups.Count == 0 ? null : plan.Groups[0].AverageProjectedScore;

        return new RulesImpactBaseline(applications, model, startDate, scores, projected, plan.Alerts.Sum(a => a.Count));
    }
}

/// <summary>
/// Simula un cambio de reglas sobre todo el portafolio antes de publicarlo.
/// </summary>
/// <remarks>
/// Es lo que hace que editar reglas desde el portal no sea peligroso: antes de
/// publicar se ve cuantas aplicaciones suben, cuantas bajan, cuanto se mueve el
/// promedio y cuales se mueven mas (plan secc. 8bis.3). Se calcula con los mismos
/// motores que calcularan despues, no con una aproximacion.
/// </remarks>
public static class RulesImpactSimulator
{
    public const int MaxMovers = 10;

    public static RulesImpact Simulate(
        IReadOnlyList<ApplicationSnapshot> applications,
        RulesModel before,
        RulesModel after,
        DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(after);

        var baseline = RulesImpactBaseline.Compute(applications, before, startDate);
        var candidate = RulesImpactBaseline.Compute(applications, after, startDate);

        return Build(baseline, candidate);
    }

    /// <summary>
    /// Simula contra un lado vigente ya calculado. Solo calcula el borrador, en paralelo.
    /// </summary>
    public static async Task<RulesImpact> SimulateAsync(
        RulesImpactBaseline baseline,
        RulesModel after,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(after);

        var candidate = await RulesImpactBaseline
            .ComputeAsync(baseline.Applications, after, baseline.StartDate, cancellationToken)
            .ConfigureAwait(false);

        return Build(baseline, candidate);
    }

    internal static decimal?[] ScoreAll(
        RulesModel model,
        IReadOnlyList<ApplicationSnapshot> applications,
        CancellationToken cancellationToken)
    {
        var scores = new decimal?[applications.Count];

        Parallel.For(
            0,
            applications.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            index => scores[index] = Score(model, applications[index]));

        return scores;
    }

    internal static decimal? Score(RulesModel model, ApplicationSnapshot application)
    {
        try
        {
            return model.Engine.Calculate(application).EffectiveScore;
        }
        catch (RulesetException)
        {
            // Su dato no cae en ninguna banda de estas reglas: no tiene score, y el
            // impacto lo cuenta aparte en lugar de esconderlo.
            return null;
        }
    }

    private static RulesImpact Build(RulesImpactBaseline before, RulesImpactBaseline after)
    {
        var applications = before.Applications;
        var shifts = new List<ApplicationShift>(applications.Count);

        for (var index = 0; index < applications.Count; index++)
        {
            var application = applications[index];
            var old = before.ScoreAt(index);
            var now = after.ScoreAt(index);

            shifts.Add(new ApplicationShift(
                application.EaiCode,
                application.ApplicationName,
                old,
                now,
                old is { } b && now is { } a ? Round(a - b) : null,
                old is { } oldScore ? ScoreBands.Of(oldScore) : null,
                now is { } newScore ? ScoreBands.Of(newScore) : null));
        }

        var averageBefore = Average(shifts.Where(s => s.Before is not null).Select(s => s.Before!.Value));
        var averageAfter = Average(shifts.Where(s => s.After is not null).Select(s => s.After!.Value));

        return new RulesImpact
        {
            Applications = applications.Count,
            Up = shifts.Count(s => s.Delta > 0),
            Down = shifts.Count(s => s.Delta < 0),
            Unchanged = shifts.Count(s => s.Delta == 0),
            NoLongerScored = shifts.Count(s => s.Before is not null && s.After is null),
            NewlyScored = shifts.Count(s => s.Before is null && s.After is not null),
            AverageBefore = averageBefore,
            AverageAfter = averageAfter,
            AverageDelta = averageBefore is { } b2 && averageAfter is { } a2 ? Round(a2 - b2) : null,
            Bands = [.. ScoreBands.All.Select(band => new BandShift(
                band.Code,
                shifts.Count(s => string.Equals(s.BandBefore, band.Code, StringComparison.Ordinal)),
                shifts.Count(s => string.Equals(s.BandAfter, band.Code, StringComparison.Ordinal))))],
            Movers = [.. shifts
                .Where(s => s.Delta is not (null or 0m) || (s.Before is null) != (s.After is null))
                // Perder o ganar el score pesa mas que cualquier delta.
                .OrderByDescending(s => s.Delta is { } delta ? Math.Abs(delta) : decimal.MaxValue)
                .ThenBy(s => s.EaiCode, StringComparer.Ordinal)
                .Take(MaxMovers)],
            ProjectedBefore = before.ProjectedScore,
            ProjectedAfter = after.ProjectedScore,
            AlertsBefore = before.Alerts,
            AlertsAfter = after.Alerts,
            Shifts = shifts,
        };
    }

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : Round(list.Average());
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
