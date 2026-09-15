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

    [System.Text.Json.Serialization.JsonIgnore]
    public RulesImpactSummary Summary => new(Applications, Up, Down, Unchanged, AverageBefore, AverageAfter, AverageDelta);
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
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var shifts = new List<ApplicationShift>(applications.Count);

        foreach (var application in applications)
        {
            var old = Score(before, application);
            var now = Score(after, application);

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

        var planBefore = before.Analyzer.Summarize(applications, startDate);
        var planAfter = after.Analyzer.Summarize(applications, startDate);

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
            ProjectedBefore = planBefore.Groups.Count == 0 ? null : planBefore.Groups[0].AverageProjectedScore,
            ProjectedAfter = planAfter.Groups.Count == 0 ? null : planAfter.Groups[0].AverageProjectedScore,
            AlertsBefore = planBefore.Alerts.Sum(a => a.Count),
            AlertsAfter = planAfter.Alerts.Sum(a => a.Count),
        };
    }

    private static decimal? Score(RulesModel model, ApplicationSnapshot application)
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

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : Round(list.Average());
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
