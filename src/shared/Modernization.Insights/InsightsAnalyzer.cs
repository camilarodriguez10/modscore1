using System.Globalization;
using Modernization.Domain;
using Modernization.Domain.Sources;
using Modernization.Roadmap;

namespace Modernization.Insights;

/// <summary>
/// Responde las seis preguntas del portafolio con lo que ya hay: el score calculado,
/// el plan del roadmap y los datos de las fuentes.
/// </summary>
/// <remarks>
/// Es una función pura sobre esos tres insumos: mismo portafolio, mismo resultado.
/// No consulta nada ni decide por nadie; ordena y cuenta, y deja a la vista de qué
/// está hecho cada número para que quien decide pueda discutirlo.
/// </remarks>
public static class InsightsAnalyzer
{
    /// <summary>Kenna por debajo de esto cuenta como gestión de vulnerabilidades débil.</summary>
    private const decimal WeakKennaScore = 50m;

    /// <summary>Score operacional por debajo de esto cuenta como hueco de resiliencia.</summary>
    private const decimal LowOperationalScore = 60m;

    public static PortfolioInsights Analyze(
        IReadOnlyList<ApplicationSnapshot> applications,
        IReadOnlyList<ScoreBreakdown> scores,
        IReadOnlyList<ApplicationRoadmapPlan> plans,
        InsightsOptions options)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Sane();

        var scoreOf = scores.ToDictionary(s => s.EaiCode, StringComparer.Ordinal);
        var planOf = plans.ToDictionary(p => p.EaiCode, StringComparer.Ordinal);

        return new PortfolioInsights
        {
            Health = Health(applications, scores, options),
            Priorities = Priorities(applications, scoreOf, planOf, options),
            Complexity = Complexity(applications, scoreOf, options),
            Risk = Risk(applications, scoreOf, options),
            Obsolescence = Obsolescence(scores, options),
            Resilience = Resilience(applications, scoreOf, options),
            Options = options,
        };
    }

    // ------------------------------------------------- ¿qué tan sano está?

    private static PortfolioHealth Health(
        IReadOnlyList<ApplicationSnapshot> applications,
        IReadOnlyList<ScoreBreakdown> scores,
        InsightsOptions options)
    {
        if (scores.Count == 0)
        {
            return new PortfolioHealth(0, 0m, 0m, 0m, 0, []);
        }

        var ordered = scores.Select(s => s.Score).OrderBy(s => s).ToList();
        var median = ordered.Count % 2 == 1
            ? ordered[ordered.Count / 2]
            : (ordered[(ordered.Count / 2) - 1] + ordered[ordered.Count / 2]) / 2m;

        var coverage = scores.Average(CoverageOf);

        var critical = applications
            .Where(a => a.CriticalApplicationIndicator == true)
            .Count(a => ScoreOf(a, scores) is { } score && score < options.HealthyScore);

        var bands = ScoreBands.All
            .Select(band =>
            {
                var count = scores.Count(s => ScoreBands.Matches(s.Score, band.Code));
                return new BandShare(band.Code, band.Range, count, Share(count, scores.Count));
            })
            .ToList();

        return new PortfolioHealth(
            scores.Count,
            Round(scores.Average(s => s.Score)),
            Round(median),
            Round(coverage),
            critical,
            bands);
    }

    /// <summary>Parte del peso del modelo que tuvo dato. Un score alto sin cobertura dice poco.</summary>
    private static decimal CoverageOf(ScoreBreakdown score)
    {
        var total = score.Pillars.Sum(p => p.TotalWeight);

        return total == 0 ? 0m : score.Pillars.Sum(p => p.AvailableWeight) / total * 100m;
    }

    private static decimal? ScoreOf(ApplicationSnapshot application, IReadOnlyList<ScoreBreakdown> scores)
        => scores.FirstOrDefault(s => string.Equals(s.EaiCode, application.EaiCode, StringComparison.Ordinal))?.Score;

    // --------------------------------------------- ¿qué modernizar primero?

    private static List<ModernizationCandidate> Priorities(
        IReadOnlyList<ApplicationSnapshot> applications,
        Dictionary<string, ScoreBreakdown> scoreOf,
        Dictionary<string, ApplicationRoadmapPlan> planOf,
        InsightsOptions options)
    {
        var candidates = new List<ModernizationCandidate>();

        foreach (var application in applications)
        {
            if (!scoreOf.TryGetValue(application.EaiCode, out var score))
            {
                continue;
            }

            planOf.TryGetValue(application.EaiCode, out var plan);

            var projected = plan?.Projected?.Projected.Score;
            var uplift = projected is { } value ? Math.Max(0m, value - score.Score) : 0m;
            var weeks = plan?.Milestones.Sum(m => m.Weeks) ?? 0;

            if (uplift <= 0m || weeks <= 0)
            {
                // Sin plan no hay esfuerzo que comparar: la lista es de lo accionable.
                continue;
            }

            var evidence = new List<Notice>();
            var weight = 1m;

            if (application.CriticalApplicationIndicator == true)
            {
                weight += 1m;
                evidence.Add(Notice.Of(NoticeCodes.InsightsCriticalApplication));
            }

            if (application.UserCount is { } users && users >= options.SignificantUserBase)
            {
                weight += 0.5m;
                evidence.Add(Notice.Of(NoticeCodes.InsightsLargeUserBase, ("users", users)));
            }

            if (application.L1BusinessCapabilityCount is { } capabilities && capabilities >= 3)
            {
                weight += 0.25m;
                evidence.Add(Notice.Of(NoticeCodes.InsightsManyCapabilities, ("count", capabilities)));
            }

            evidence.Add(Notice.Of(
                NoticeCodes.InsightsUpliftFromRoadmap,
                ("uplift", Round(uplift)),
                ("weeks", weeks)));

            var impact = uplift * weight;

            candidates.Add(new ModernizationCandidate(
                application.EaiCode,
                application.ApplicationName,
                application.Lob,
                application.Country,
                Round(score.Score),
                projected is { } p ? Round(p) : null,
                Round(uplift),
                weeks,
                weight,
                Round(impact),
                Round(impact / weeks),
                application.Roadmap,
                evidence));
        }

        return [.. candidates
            .OrderByDescending(c => c.ValuePerWeek)
            .ThenByDescending(c => c.Impact)
            .ThenBy(c => c.EaiCode, StringComparer.Ordinal)
            .Take(options.TopCount)];
    }

    // ------------------------------------------- ¿dónde reducir complejidad?

    private static List<ComplexityCluster> Complexity(
        IReadOnlyList<ApplicationSnapshot> applications,
        Dictionary<string, ScoreBreakdown> scoreOf,
        InsightsOptions options)
    {
        var clusters = new List<ComplexityCluster>();

        // 1. Misma capacidad, mismo pais: el caso mas claro de duplicacion.
        foreach (var group in applications
            .Where(a => !string.IsNullOrWhiteSpace(a.PrimaryBusinessCapability) && !string.IsNullOrWhiteSpace(a.Country))
            .GroupBy(a => (Capability: a.PrimaryBusinessCapability!, Country: a.Country!))
            .Where(g => g.Count() >= options.DuplicateThreshold))
        {
            clusters.Add(Cluster(
                ComplexityKind.DuplicateCapabilityInCountry,
                group.Key.Capability + " · " + group.Key.Country,
                group,
                scoreOf,
                Notice.Of(
                    NoticeCodes.InsightsDuplicateCapability,
                    ("capability", group.Key.Capability),
                    ("country", group.Key.Country),
                    ("count", group.Count()))));
        }

        // 2. La misma capacidad resuelta con aplicaciones distintas en varios paises.
        foreach (var group in applications
            .Where(a => !string.IsNullOrWhiteSpace(a.PrimaryBusinessCapability))
            .GroupBy(a => a.PrimaryBusinessCapability!)
            .Where(g => g.Select(a => a.Country).Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() >= options.DuplicateThreshold))
        {
            var countries = group.Select(a => a.Country)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            clusters.Add(Cluster(
                ComplexityKind.CapabilityAcrossCountries,
                group.Key,
                group,
                scoreOf,
                Notice.Of(
                    NoticeCodes.InsightsCapabilityAcrossCountries,
                    ("capability", group.Key),
                    ("countries", countries),
                    ("count", group.Count()))));
        }

        // 3. Marcadas para eliminar y todavia activas: complejidad que ya se decidio sacar.
        var backlog = applications
            .Where(a => a.Disposition == ApplicationDisposition.Eliminate)
            .Where(a => a.ApplicationStatus is null
                || a.ApplicationStatus.Contains("Active", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (backlog.Count > 0)
        {
            clusters.Add(Cluster(
                ComplexityKind.DecommissionBacklog,
                nameof(ApplicationDisposition.Eliminate),
                backlog,
                scoreOf,
                Notice.Of(NoticeCodes.InsightsDecommissionBacklog, ("count", backlog.Count))));
        }

        // 4. Una capacidad resuelta en muchos lenguajes distintos: cada uno con su gente.
        foreach (var group in applications
            .Where(a => !string.IsNullOrWhiteSpace(a.PrimaryBusinessCapability) && !string.IsNullOrWhiteSpace(a.CodingLanguage))
            .GroupBy(a => a.PrimaryBusinessCapability!))
        {
            var languages = group.Select(a => a.CodingLanguage!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (languages.Count < options.DuplicateThreshold)
            {
                continue;
            }

            clusters.Add(Cluster(
                ComplexityKind.LanguageSpread,
                group.Key,
                group,
                scoreOf,
                Notice.Of(
                    NoticeCodes.InsightsLanguageSpread,
                    ("capability", group.Key),
                    ("languages", string.Join(", ", languages.Take(6))),
                    ("count", languages.Count))));
        }

        return [.. clusters
            .OrderByDescending(c => c.Applications)
            .ThenBy(c => c.Dimension, StringComparer.OrdinalIgnoreCase)
            .Take(options.TopCount)];
    }

    private static ComplexityCluster Cluster(
        ComplexityKind kind,
        string dimension,
        IEnumerable<ApplicationSnapshot> group,
        Dictionary<string, ScoreBreakdown> scoreOf,
        Notice evidence)
    {
        var applications = group.ToList();

        var scores = applications
            .Select(a => scoreOf.TryGetValue(a.EaiCode, out var score) ? score.Score : (decimal?)null)
            .Where(s => s is not null)
            .Select(s => s!.Value)
            .ToList();

        return new ComplexityCluster(
            kind,
            dimension,
            applications.Count,
            applications.Sum(a => a.UserCount ?? 0),
            scores.Count == 0 ? 0m : Round(scores.Average()),
            [.. applications.OrderBy(a => a.EaiCode, StringComparer.Ordinal).Select(a => a.EaiCode).Take(12)],
            evidence);
    }

    // --------------------------------------- ¿dónde se concentra el riesgo?

    private static List<RiskConcentration> Risk(
        IReadOnlyList<ApplicationSnapshot> applications,
        Dictionary<string, ScoreBreakdown> scoreOf,
        InsightsOptions options)
    {
        var groupings = new (string Name, Func<ApplicationSnapshot, string?> Of)[]
        {
            ("lob", a => a.Lob),
            ("country", a => a.Country),
            ("cio", a => a.Cio),
            ("capability", a => a.PrimaryBusinessCapability),
        };

        var concentrations = new List<RiskConcentration>();

        foreach (var (name, of) in groupings)
        {
            foreach (var group in applications
                .Where(a => !string.IsNullOrWhiteSpace(of(a)))
                .GroupBy(of!, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToList();

                var criticalFlaws = members.Sum(a => a.ScaFlaws?.Critical ?? 0);
                var highFlaws = members.Sum(a => a.ScaFlaws?.High ?? 0);
                var weakKenna = members.Count(a => a.KennaScore is { } k && k < WeakKennaScore);
                var obsolete = members.Sum(a =>
                    (a.ArchitecturalValue?.ObsoleteCount ?? 0) + (a.ArchitecturalValue?.NotApprovedForUseCount ?? 0));
                var exposed = members.Count(a => a.InternetFacing == true && a.CriticalApplicationIndicator == true);

                var scores = members
                    .Select(a => scoreOf.TryGetValue(a.EaiCode, out var score) ? score.Score : (decimal?)null)
                    .Where(s => s is not null)
                    .Select(s => s!.Value)
                    .ToList();

                // Indice para ordenar, no para comparar contra otro portafolio: un flaw
                // critico pesa mas que uno alto, y una aplicacion critica expuesta pesa
                // como varios flaws porque el riesgo no es solo el conteo.
                var index = (criticalFlaws * 3m) + (highFlaws * 1m) + (weakKenna * 5m) + (obsolete * 2m) + (exposed * 10m);

                if (index <= 0m)
                {
                    continue;
                }

                concentrations.Add(new RiskConcentration(
                    name,
                    group.Key!,
                    members.Count,
                    criticalFlaws,
                    highFlaws,
                    weakKenna,
                    obsolete,
                    exposed,
                    scores.Count == 0 ? 0m : Round(scores.Average()),
                    Round(index)));
            }
        }

        return [.. concentrations
            .OrderByDescending(c => c.RiskIndex)
            .ThenBy(c => c.Dimension, StringComparer.OrdinalIgnoreCase)
            .Take(options.TopCount)];
    }

    // ------------------------------------- ¿qué está causando la obsolescencia?

    private static List<ObsolescenceDriver> Obsolescence(
        IReadOnlyList<ScoreBreakdown> scores,
        InsightsOptions options)
    {
        var drivers = new Dictionary<string, (string Name, string Pillar, decimal Lost, decimal WithoutData, int Affected)>(
            StringComparer.Ordinal);

        foreach (var score in scores)
        {
            foreach (var metric in score.Metrics)
            {
                if (metric.Weight <= 0m)
                {
                    // Las metricas de peso 0 se muestran, pero no mueven el score.
                    continue;
                }

                drivers.TryGetValue(metric.MetricKey, out var current);
                current.Name = metric.Name;
                current.Pillar = metric.PillarKey;

                if (metric.MappedScore is { } mapped)
                {
                    var lost = metric.Weight * (100m - mapped) / 100m;

                    if (lost > 0m)
                    {
                        current.Lost += lost;
                        current.Affected++;
                    }
                }
                else
                {
                    current.WithoutData += metric.Weight;
                }

                drivers[metric.MetricKey] = current;
            }
        }

        var totalLost = drivers.Values.Sum(d => d.Lost);

        return [.. drivers
            .Where(d => d.Value.Lost > 0m || d.Value.WithoutData > 0m)
            .Select(d => new ObsolescenceDriver(
                d.Key,
                d.Value.Name,
                d.Value.Pillar,
                Round(d.Value.Lost),
                Round(d.Value.WithoutData),
                d.Value.Affected,
                totalLost == 0m ? 0m : Round(d.Value.Lost / totalLost * 100m)))
            .OrderByDescending(d => d.LostPoints)
            .ThenByDescending(d => d.WeightWithoutData)
            .Take(options.TopCount)];
    }

    // ------------------------------------------ ¿dónde falta resiliencia?

    private static ResilienceGaps Resilience(
        IReadOnlyList<ApplicationSnapshot> applications,
        Dictionary<string, ScoreBreakdown> scoreOf,
        InsightsOptions options)
    {
        var withGaps = new List<ResilienceGapApplication>();
        int withoutDr = 0, withoutMonitoring = 0, lowOperational = 0, withoutObjectives = 0;

        foreach (var application in applications)
        {
            var gaps = new List<Notice>();
            var critical = application.CriticalApplicationIndicator == true;

            if (application.DisasterRecoveryInScope == false)
            {
                gaps.Add(Notice.Of(NoticeCodes.InsightsGapDisasterRecovery));

                if (critical)
                {
                    withoutDr++;
                }
            }

            if (application.Monitoring?.AnyEnabled is null or false)
            {
                gaps.Add(Notice.Of(NoticeCodes.InsightsGapMonitoring));

                if (critical)
                {
                    withoutMonitoring++;
                }
            }

            if (application.OperationalScore is { } operational && operational < LowOperationalScore)
            {
                gaps.Add(Notice.Of(NoticeCodes.InsightsGapOperational, ("score", Round(operational))));
                lowOperational++;
            }

            if (string.IsNullOrWhiteSpace(application.RecoveryTimeObjectiveRange)
                || string.IsNullOrWhiteSpace(application.RecoveryPointObjectiveRange))
            {
                gaps.Add(Notice.Of(NoticeCodes.InsightsGapRecoveryObjectives));
                withoutObjectives++;
            }

            if (gaps.Count == 0)
            {
                continue;
            }

            var score = scoreOf.TryGetValue(application.EaiCode, out var breakdown) ? breakdown.Score : 0m;

            withGaps.Add(new ResilienceGapApplication(
                application.EaiCode,
                application.ApplicationName,
                application.Lob,
                application.Country,
                critical,
                Round(score),
                application.RecoveryTimeObjectiveRange,
                application.RecoveryPointObjectiveRange,
                gaps));
        }

        return new ResilienceGaps(
            withoutDr,
            withoutMonitoring,
            lowOperational,
            withoutObjectives,
            [.. withGaps
                .OrderByDescending(a => a.Critical)
                .ThenByDescending(a => a.Gaps.Count)
                .ThenBy(a => a.Score)
                .Take(options.TopCount)]);
    }

    // -----------------------------------------------------------------------

    private static decimal Share(int count, int total)
        => total == 0 ? 0m : Round(count / (decimal)total * 100m);

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Formato invariante para los argumentos numericos de los avisos.</summary>
    internal static string Text(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
