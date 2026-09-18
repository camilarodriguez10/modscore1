using System.Globalization;
using Modernization.ChannelApi.Clients;
using Modernization.Domain;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Composition;

/// <summary>
/// Arma los modelos de vista de cada pantalla a partir de los recursos canonicos
/// de la capa System.
/// </summary>
/// <remarks>
/// Aqui no se calcula ningun score: se ordena, se agrupa y se etiqueta lo que la
/// capa System ya calculo. La linea es estricta a proposito — si una regla de
/// negocio se colara aqui, habria dos fuentes de verdad (ADR 0001).
/// </remarks>
public sealed class ScreenComposer(SystemApiClient system)
{
    public async Task<PortfolioView> ComposePortfolioAsync(
        ApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        // Fan-out: la pagina visible, el conjunto filtrado completo y los scores.
        //
        // Se piden las dos cosas a proposito: las filas de la tabla son la pagina,
        // pero los agregados deben cubrir todo el conjunto filtrado. Calcularlos
        // sobre la pagina daria un promedio distinto en cada scroll.
        var pageTask = system.GetApplicationsAsync(query, cancellationToken);
        var filteredTask = system.GetAllApplicationsAsync(query, cancellationToken);
        var scoresTask = system.GetAllScoresAsync(cancellationToken);

        await Task.WhenAll(pageTask, filteredTask, scoresTask).ConfigureAwait(false);

        var applications = await pageTask.ConfigureAwait(false);
        var filtered = await filteredTask.ConfigureAwait(false);
        var scores = await scoresTask.ConfigureAwait(false);
        var scoreByEai = scores.ToDictionary(s => s.EaiCode, StringComparer.Ordinal);

        var rows = applications.Items
            .Select(app =>
            {
                scoreByEai.TryGetValue(app.EaiCode, out var score);
                var value = score?.EffectiveScore ?? 0m;

                return new PortfolioRowView(
                    app.EaiCode,
                    app.ApplicationName,
                    app.Lob,
                    app.Country,
                    app.Cio,
                    app.Cre,
                    app.CriticalApplication,
                    app.Disposition,
                    app.Roadmap,
                    value,
                    BandOfScore(value),
                    score?.Denominator ?? 0m);
            })
            .ToList();

        // Los agregados respetan el filtro activo: si el usuario mira solo Chile,
        // el promedio que ve es el de Chile.
        var filteredValues = filtered
            .Select(a => scoreByEai.TryGetValue(a.EaiCode, out var v) ? v.EffectiveScore : (decimal?)null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        return new PortfolioView(
            filtered.Count,
            filteredValues.Count == 0 ? 0m : Round(filteredValues.Average()),
            Median(filteredValues),
            BuildDistribution(filteredValues),

            // Cada agregado es tambien un filtro: la clave que sale aqui es la que
            // la pantalla envia de vuelta al hacer clic, sin traducir nada.
            GroupBy(scores, filtered, a => a.Lob),
            GroupBy(scores, filtered, a => a.Country),
            GroupBy(scores, filtered, a => a.Cio),
            GroupBy(scores, filtered, a => a.Disposition),
            GroupBy(scores, filtered, a => a.Roadmap),

            // Los cuatro del dashboard ejecutivo: tambien son las opciones de su filtro.
            GroupBy(scores, filtered, a => a.Cre),
            GroupBy(scores, filtered, a => a.Region),
            GroupBy(scores, filtered, a => a.ApplicationStatus),
            GroupBy(scores, filtered, a => a.PrimaryBusinessCapability),
            rows,
            applications.Page,
            applications.PageSize,
            applications.TotalCount);
    }

    public async Task<ScoreOverviewView?> ComposeOverviewAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
    {
        var applicationTask = system.GetApplicationAsync(eaiCode, cancellationToken);
        var scoreTask = system.GetScoreAsync(eaiCode, cancellationToken);

        await Task.WhenAll(applicationTask, scoreTask).ConfigureAwait(false);

        var application = await applicationTask.ConfigureAwait(false);
        var score = await scoreTask.ConfigureAwait(false);

        if (application is null || score is null)
        {
            return null;
        }

        var byKey = score.Metrics.ToLookup(m => m.PillarKey, StringComparer.Ordinal);

        var pillars = score.Pillars.Select(p =>
        {
            var metrics = byKey[p.Key].ToList();

            return new PillarCardView(
                p.Key,
                p.Name,
                p.Score,
                p.TotalWeight,
                p.AvailableWeight,
                p.Numerator,
                metrics.Count(m => m.IsIncluded && !m.IsInformational),
                metrics.Count(m => !m.IsIncluded),
                [.. metrics
                    .Where(m => !m.IsInformational)
                    .Select(m => new PillarMetricView(
                        m.MetricKey, m.Name, m.Display, m.MappedScore, m.Weight, m.WeightedScore, m.IsIncluded))]);
        }).ToList();

        return new ScoreOverviewView(
            HeaderOf(application),
            score.EffectiveScore,
            BandOfScore(score.EffectiveScore),
            score.RulesetVersion,
            score.Numerator,
            score.Denominator,
            pillars,
            score.Override is null
                ? null
                : new OverrideView(score.Override.Value, score.Override.Reason, score.Override.AppliedBy, score.Override.AppliedAt),
            score.CalculatedAt);
    }

    /// <summary>
    /// Pantalla "como se calculo". Compone la formula con los numeros reales
    /// sustituidos y el aporte acumulado de cada metrica.
    /// </summary>
    public async Task<ExplainView?> ComposeExplainAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
    {
        var applicationTask = system.GetApplicationAsync(eaiCode, cancellationToken);
        var scoreTask = system.GetScoreAsync(eaiCode, cancellationToken);
        var rulesetTask = system.GetRulesetAsync(cancellationToken);

        await Task.WhenAll(applicationTask, scoreTask, rulesetTask).ConfigureAwait(false);

        var application = await applicationTask.ConfigureAwait(false);
        var score = await scoreTask.ConfigureAwait(false);
        var ruleset = await rulesetTask.ConfigureAwait(false);

        if (application is null || score is null)
        {
            return null;
        }

        var scored = score.Metrics.Where(m => m.IsIncluded && !m.IsInformational).ToList();

        // Waterfall: mayor aporte primero, con el acumulado corriendo.
        decimal cumulative = 0m;
        var waterfall = scored
            .OrderByDescending(m => m.WeightedScore ?? 0m)
            .ThenBy(m => m.MetricKey, StringComparer.Ordinal)
            .Select(m =>
            {
                var weighted = m.WeightedScore ?? 0m;
                cumulative += weighted;

                // Cuantos puntos del score final aporta esta metrica.
                var points = score.Denominator == 0
                    ? 0m
                    : Round(weighted / score.Denominator * ruleset.Range);

                return new WaterfallStepView(m.MetricKey, m.Name, m.PillarKey, weighted, Round(cumulative), points);
            })
            .ToList();

        return new ExplainView(
            HeaderOf(application),
            BuildFormula(score, ruleset),
            waterfall,
            [.. scored.Select(ToExplainMetric)],
            [.. score.Metrics.Where(m => !m.IsIncluded).Select(m => new ExcludedMetricView(
                m.MetricKey, m.Name, m.PillarKey, m.Weight, m.Exclusion ?? Notice.Of(NoticeCodes.ValueNoData), Provenance(m)))],
            [.. score.Metrics.Where(m => m.IsIncluded && m.IsInformational).Select(ToExplainMetric)],
            score.CalculatedAt);
    }

    /// <summary>
    /// Linaje de una carga concreta. Sin version, la vigente.
    /// </summary>
    /// <remarks>
    /// El total de aplicaciones sale de la version, no de la fuente mejor cubierta:
    /// una fuente que alcanza al 71% del portafolio no dice cuantas aplicaciones hay,
    /// dice cuantas alcanzo.
    /// </remarks>
    public async Task<LineageView?> ComposeLineageAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var sourcesTask = system.GetSourcesAsync(versionId, cancellationToken);
        var versionsTask = system.GetVersionsAsync(cancellationToken);

        await Task.WhenAll(sourcesTask, versionsTask).ConfigureAwait(false);

        var sources = await sourcesTask.ConfigureAwait(false);
        var versions = await versionsTask.ConfigureAwait(false);

        if (sources is null)
        {
            return null;
        }
        var first = sources.Count > 0 ? sources[0] : null;

        var version = string.IsNullOrWhiteSpace(versionId)
            ? versions.FirstOrDefault(v => v.IsCurrent) ?? (versions.Count > 0 ? versions[0] : null)
            : versions.FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal));

        return new LineageView(
            first?.Provider ?? string.Empty,
            version?.ApplicationCount ?? first?.MatchedApplications ?? 0,
            [.. sources
                .OrderByDescending(s => s.CoveragePercent)
                .Select(s => new LineageSourceView(
                    s.SourceName,
                    s.Origin,
                    s.RecordCount,
                    s.MatchedApplications,
                    s.CoveragePercent,
                    s.RejectedCount,
                    [.. s.Rejections.Select(r => new LineageRejectionView(r.RowNumber, r.Reason, r.Key))],
                    s.ExtractedAt))],
            first?.ExtractedAt ?? DateTimeOffset.MinValue,
            version?.Id ?? string.Empty,
            version?.Label ?? string.Empty);
    }

    // -----------------------------------------------------------------------

    private static FormulaView BuildFormula(ScoreResponse score, RulesetResponse ruleset)
    {
        var expression = string.Create(
            CultureInfo.InvariantCulture,
            $"{ruleset.Floor} + ({score.Numerator:0.###} / {score.Denominator:0.##}) x {ruleset.Range} = {score.Score:0.00}");

        var excluded = score.Metrics.Count(m => !m.IsIncluded);
        var quotient = score.Denominator == 0 ? 0m : score.Numerator / score.Denominator;

        return new FormulaView(
            expression,
            ruleset.Floor,
            ruleset.Range,
            score.Numerator,
            score.Denominator,
            score.Score,
            score.RulesetVersion,
            [
                // Los pasos son codigos con sus numeros: la interfaz los redacta (ADR 0017).
                Notice.Of(NoticeCodes.FormulaNumerator, ("numerator", score.Numerator)),
                Notice.Of(NoticeCodes.FormulaDenominator, ("denominator", score.Denominator)),
                Notice.Of(NoticeCodes.FormulaExcluded, ("count", excluded)),
                Notice.Of(
                    NoticeCodes.FormulaQuotient,
                    ("numerator", score.Numerator),
                    ("denominator", score.Denominator),
                    ("quotient", Math.Round(quotient, 4, MidpointRounding.AwayFromZero))),
                Notice.Of(
                    NoticeCodes.FormulaNormalization,
                    ("floor", ruleset.Floor),
                    ("quotient", Math.Round(quotient, 4, MidpointRounding.AwayFromZero)),
                    ("range", ruleset.Range),
                    ("score", score.Score)),
            ]);
    }

    private static ExplainMetricView ToExplainMetric(MetricResponse m) => new(
        m.MetricKey,
        m.Name,
        m.PillarKey,
        m.Display,
        m.RuleApplied,
        m.MappedScore,
        m.Weight,
        m.WeightedScore,
        Provenance(m));

    private static ProvenanceView Provenance(MetricResponse m) => new(
        m.Provenance.System,
        m.Provenance.Report,
        m.Provenance.Field,
        m.Provenance.Provider,
        m.Provenance.ExtractedAt);

    /// <summary>Cabecera de identidad, compartida por todas las pantallas de detalle.</summary>
    internal static ApplicationHeaderView HeaderOf(ApplicationResponse a) => new(
        a.EaiCode,
        a.ApplicationName,
        a.Lob,
        a.SubLob,
        a.Country,
        a.Region,
        a.Cio,
        a.Cre,
        a.CriticalApplication,
        a.Disposition,
        a.Roadmap,
        a.CodingLanguage,
        a.UserCount);

    /// <summary>
    /// Tramo de un score. La tabla vive en el dominio desde la Fase 10, porque el
    /// tramo tambien es un filtro y filtrar exige puntuar: con la tabla duplicada,
    /// la pagina y el agregado podrian discrepar sobre que es "En riesgo".
    /// </summary>
    internal static string BandOfScore(decimal score) => ScoreBands.Of(score);

    private static IReadOnlyList<ScoreBandView> BuildDistribution(List<decimal> values)
        => [.. ScoreBands.All.Select(b =>
        {
            var count = values.Count(v => v >= b.Min && v <= b.Max);
            var percent = values.Count == 0 ? 0m : Round((decimal)count / values.Count * 100m);

            return new ScoreBandView(b.Code, b.Range, count, percent);
        })];

    private static IReadOnlyList<GroupSummaryView> GroupBy(
        IReadOnlyList<ScoreResponse> scores,
        IReadOnlyList<ApplicationResponse> applications,
        Func<ApplicationResponse, string?> selector)
    {
        var scoreByEai = scores.ToDictionary(s => s.EaiCode, s => s.EffectiveScore, StringComparer.Ordinal);

        return [.. applications
            .GroupBy(a => selector(a) ?? string.Empty, StringComparer.Ordinal)
            .Select(g =>
            {
                var values = g
                    .Select(a => scoreByEai.TryGetValue(a.EaiCode, out var v) ? v : (decimal?)null)
                    .Where(v => v is not null)
                    .Select(v => v!.Value)
                    .ToList();

                return new GroupSummaryView(g.Key, g.Count(), values.Count == 0 ? 0m : Round(values.Average()));
            })
            .OrderByDescending(g => g.Count)];
    }

    private static decimal Median(List<decimal> ordered)
    {
        if (ordered.Count == 0)
        {
            return 0m;
        }

        var middle = ordered.Count / 2;

        return ordered.Count % 2 == 1
            ? ordered[middle]
            : Round((ordered[middle - 1] + ordered[middle]) / 2m);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
