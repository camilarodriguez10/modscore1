using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Modernization.Sources.Dummy;

/// <summary>
/// El mismo portafolio dummy en trimestres anteriores: las mismas aplicaciones, con los
/// valores que tenían antes, para comparar el score, el Currency Score y el riesgo en el
/// tiempo.
/// </summary>
/// <remarks>
/// <para>
/// Cada aplicación sigue una trayectoria determinista por su código EAI. Las que están
/// en un roadmap de inversión suelen <b>modernizarse</b>: hacia atrás tenían peor
/// Currency Score, peor Kenna, más componentes obsoletos, más deuda técnica y a veces sin
/// monitoreo. Las marcadas para salir del portafolio suelen <b>envejecer</b>: hacia atrás
/// estaban mejor. El resto apenas se mueve.
/// </para>
/// <para>
/// Los flaws SCA no se inventan: se leen de las fechas que ya traen. Un flaw encontrado
/// después de la fecha de la carga anterior no existía todavía, y uno resuelto después
/// seguía abierto.
/// </para>
/// <para>
/// El EAI de referencia del BRD no cambia: su 80,70 (79,67 con las bandas del BRD) es el mismo
/// en cada trimestre.
/// </para>
/// </remarks>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Generador determinista de datos de prueba: la reproducibilidad por semilla es el requisito.")]
public static class DummyTimeline
{
    private enum Trajectory
    {
        Stable,
        Modernizing,
        Aging,
    }

    /// <summary>El portafolio tal como estaba <paramref name="quarters"/> trimestres antes.</summary>
    public static DummyPortfolio Rewind(DummyPortfolio current, int quarters)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentOutOfRangeException.ThrowIfNegative(quarters);

        if (quarters == 0)
        {
            return current;
        }

        var at = current.CapturedAt.AddMonths(-3 * quarters);
        var plans = current.Eai.ToDictionary(e => e.EaiCode, Trace, StringComparer.Ordinal);
        var byAppId = current.Eai
            .Where(e => e.AppId is not null)
            .GroupBy(e => e.AppId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => plans[g.First().EaiCode], StringComparer.Ordinal);

        Plan? PlanOf(string eaiCode) => plans.TryGetValue(eaiCode, out var plan) ? plan : null;

        return new DummyPortfolio
        {
            Seed = current.Seed,
            CapturedAt = at,
            Eai = [.. current.Eai.Select(e => RewindEai(e, plans[e.EaiCode], quarters))],
            ArchitecturalValue = [.. current.ArchitecturalValue.Select(a => RewindArchitecture(a, PlanOf(a.EaiCode), quarters))],
            ProductStrategy = [.. current.ProductStrategy.Select(p => RewindStrategy(p, PlanOf(p.EaiCode), quarters))],
            TechDebt = [.. current.TechDebt.Select(t => RewindTechDebt(t, byAppId.GetValueOrDefault(t.AppId), quarters, at))],
            ScaFlaws = [.. current.ScaFlaws.Select(f => RewindFlaw(f, at)).OfType<ScaFlawRecord>()],
            OssScore = [.. current.OssScore.Select(o => RewindOss(o, PlanOf(o.EaiCode), quarters, at))],
            Monitoring = [.. current.Monitoring.Select(m => RewindMonitoring(m, PlanOf(m.EaiCode), quarters))],
            ApimOnboarding = [.. current.ApimOnboarding.Where(a => PlanOf(a.EaiCode) is not { } plan || !plan.OnboardedLate(quarters))],
        };
    }

    // ------------------------------------------------------------ trayectoria

    /// <summary>
    /// Qué hace la aplicación en el tiempo y a qué ritmo. Cada paso es la <b>mejora por
    /// trimestre</b> en el sentido natural de la métrica: más Currency, OSS y score
    /// operacional es mejor; menos Kenna es mejor. Ir al pasado es deshacer esa mejora.
    /// </summary>
    private sealed record Plan(
        Trajectory Trajectory,
        decimal CurrencyStep,
        int KennaStep,
        decimal OperationalStep,
        decimal OssStep,
        int MonitoringSince,
        int ApimSince)
    {
        public bool IsReference { get; init; }

        /// <summary>Hace más de <c>MonitoringSince</c> trimestres no tenía monitoreo.</summary>
        public bool MonitoredLate(int quarters) => Trajectory == Trajectory.Modernizing && quarters >= MonitoringSince;

        public bool OnboardedLate(int quarters) => Trajectory == Trajectory.Modernizing && quarters >= ApimSince;
    }

    private static Plan Trace(EaiRecord application)
    {
        if (application.EaiCode == DummyPortfolio.ReferenceEaiCode)
        {
            return new Plan(Trajectory.Stable, 0m, 0, 0m, 0m, int.MaxValue, int.MaxValue) { IsReference = true };
        }

        var random = new Random(Seed(application.EaiCode));
        var roll = random.NextDouble();

        var trajectory = application.ApplicationDisposition switch
        {
            "Invest" => roll < 0.65 ? Trajectory.Modernizing : roll < 0.85 ? Trajectory.Stable : Trajectory.Aging,
            "Tolerate" => roll < 0.2 ? Trajectory.Modernizing : roll < 0.45 ? Trajectory.Stable : Trajectory.Aging,
            "Eliminate" => roll < 0.8 ? Trajectory.Aging : Trajectory.Stable,
            _ => Trajectory.Stable,
        };

        return trajectory switch
        {
            // Mejora cada trimestre: hacia atrás estaba peor.
            Trajectory.Modernizing => new Plan(
                trajectory,
                CurrencyStep: 4m + random.Next(0, 6),
                KennaStep: 20 + random.Next(0, 41),
                OperationalStep: 1m + random.Next(0, 4),
                OssStep: 2m + random.Next(0, 4),
                MonitoringSince: 2 + random.Next(0, 2),
                ApimSince: random.NextDouble() < 0.3 ? 3 : int.MaxValue),

            // Empeora cada trimestre: hacia atrás estaba mejor.
            Trajectory.Aging => new Plan(
                trajectory,
                CurrencyStep: -(2m + random.Next(0, 4)),
                KennaStep: -(10 + random.Next(0, 31)),
                OperationalStep: -(1m + random.Next(0, 3)),
                OssStep: -(1m + random.Next(0, 3)),
                MonitoringSince: int.MaxValue,
                ApimSince: int.MaxValue),

            _ => new Plan(
                trajectory,
                CurrencyStep: random.Next(-1, 2),
                KennaStep: random.Next(-5, 6),
                OperationalStep: 0m,
                OssStep: 0m,
                MonitoringSince: int.MaxValue,
                ApimSince: int.MaxValue),
        };
    }

    // ------------------------------------------------------------ por fuente

    private static EaiRecord RewindEai(EaiRecord application, Plan plan, int quarters)
    {
        if (plan.IsReference)
        {
            return application;
        }

        return application with
        {
            // Un 0 o un nulo es falta de dato, y sigue siéndolo.
            RawTechScore = application.RawTechScore is { } currency and > 0m
                ? Math.Clamp(currency - (plan.CurrencyStep * quarters), 1m, 100m)
                : application.RawTechScore,
            KennaScore = application.KennaScore is { } kenna and > 0
                ? Math.Clamp(kenna + (plan.KennaStep * quarters), 1, 1000)
                : application.KennaScore,
            OperationalScore = application.OperationalScore is { } operational and > 0m
                ? Math.Clamp(operational - (plan.OperationalStep * quarters), 1m, 100m)
                : application.OperationalScore,
        };
    }

    private static ArchitecturalValueRecord RewindArchitecture(ArchitecturalValueRecord record, Plan? plan, int quarters)
    {
        if (plan is null || plan.IsReference || record.ModernCount is not { } modern)
        {
            return record;
        }

        var heritage = record.HeritageCount ?? 0;
        var obsolete = record.ObsoleteCount ?? 0;

        switch (plan.Trajectory)
        {
            case Trajectory.Modernizing:
                // Antes, parte de lo moderno era heredado u obsoleto.
                var moved = Math.Min(modern, 2 * quarters);
                return record with
                {
                    ModernCount = modern - moved,
                    HeritageCount = heritage + moved - (moved / 3),
                    ObsoleteCount = obsolete + (moved / 3),
                };

            case Trajectory.Aging:
                // Antes, parte de lo obsoleto seguía vigente.
                var aged = Math.Min(obsolete, quarters);
                return record with
                {
                    ObsoleteCount = obsolete - aged,
                    HeritageCount = heritage + aged,
                };

            default:
                return record;
        }
    }

    private static ProductStrategyRecord RewindStrategy(ProductStrategyRecord record, Plan? plan, int quarters)
    {
        if (plan is not { Trajectory: Trajectory.Modernizing } || plan.IsReference || record.InvestCount is not { } invest)
        {
            return record;
        }

        var moved = Math.Min(invest, quarters);

        return record with
        {
            InvestCount = invest - moved,
            TolerateCount = (record.TolerateCount ?? 0) + moved,
        };
    }

    private static TechDebtRecord RewindTechDebt(TechDebtRecord record, Plan? plan, int quarters, DateTimeOffset at)
    {
        var step = plan?.Trajectory switch
        {
            Trajectory.Modernizing => 0.25m * quarters,
            Trajectory.Aging => -0.25m * quarters,
            _ => 0m,
        };

        var denial = Clamp(record.CombinedDenialScore, step);
        var accepted = Clamp(record.CombinedAcceptedScore, step);

        return record with
        {
            CombinedDenialScore = denial,
            CombinedAcceptedScore = accepted,
            CombinedAppScore = denial is { } d && accepted is { } a ? Quantize((d + a) / 2m) : record.CombinedAppScore,
            LastUpdatedDate = record.LastUpdatedDate is { } updated && updated > at ? at.AddDays(-7) : record.LastUpdatedDate,
        };

        static decimal? Clamp(decimal? value, decimal step)
            => value is { } v ? Quantize(Math.Clamp(v + step, 0m, 5m)) : null;
    }

    /// <summary>Un flaw encontrado después no existía; uno resuelto después seguía abierto.</summary>
    private static ScaFlawRecord? RewindFlaw(ScaFlawRecord flaw, DateTimeOffset at)
    {
        if (flaw.FirstFoundDate is { } found && found > at)
        {
            return null;
        }

        var reopened = flaw.ResolvedDate is { } resolved && resolved > at;

        return flaw with
        {
            FindingStatus = reopened ? "Open" : flaw.FindingStatus,
            ResolvedDate = reopened ? null : flaw.ResolvedDate,
            LastFoundDate = flaw.LastFoundDate is { } last && last > at ? at : flaw.LastFoundDate,
            FindingAge = flaw.FirstFoundDate is { } first ? (int)(at - first).TotalDays : flaw.FindingAge,
        };
    }

    private static OssScoreRecord RewindOss(OssScoreRecord record, Plan? plan, int quarters, DateTimeOffset at)
        => record with
        {
            OssScore = plan is { IsReference: false } && record.OssScore is { } oss and > 0m
                ? Math.Clamp(oss - (plan.OssStep * quarters), 1m, 100m)
                : record.OssScore,
            UpdatedAt = at,
        };

    private static MonitoringRecord RewindMonitoring(MonitoringRecord record, Plan? plan, int quarters)
        => plan is not null && plan.MonitoredLate(quarters)
            ? record with { Agent = false, Apm = false, Monitoring = false, Synthetics = false }
            : record;

    /// <summary>Tech debt en cuartos, como lo publica la fuente.</summary>
    private static decimal Quantize(decimal value) => Math.Round(value * 4m, MidpointRounding.AwayFromZero) / 4m;

    /// <summary>FNV-1a sobre el código EAI: estable entre procesos.</summary>
    private static int Seed(string eaiCode)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var b in Encoding.UTF8.GetBytes("timeline:" + eaiCode))
            {
                hash = (hash ^ b) * 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
