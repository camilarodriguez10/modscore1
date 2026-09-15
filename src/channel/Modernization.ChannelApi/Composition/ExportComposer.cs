using System.Globalization;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.Exports;
using Modernization.Domain;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Composition;

/// <summary>Un archivo listo para descargar.</summary>
public sealed record ExportFile(string FileName, CsvDocument Document);

/// <summary>
/// Descargas en CSV de lo que la persona esta viendo, con los mismos filtros.
/// </summary>
/// <remarks>
/// Como el resto de la capa Channel, no calcula: junta lo que la capa System ya calculo,
/// dentro del alcance de quien pide. Un filtro nunca amplia lo que se exporta, igual que
/// no amplia lo que se ve (ADR 0012, ADR 0016). Los encabezados van en ingles, como el
/// sitio.
/// </remarks>
public sealed class ExportComposer(SystemApiClient system, TimeProvider clock)
{
    private static readonly Dictionary<string, string> BandNames = new(StringComparer.Ordinal)
    {
        ["critical"] = "Critical",
        ["at-risk"] = "At risk",
        ["acceptable"] = "Acceptable",
        ["solid"] = "Solid",
    };

    /// <summary>El portafolio filtrado: una fila por aplicacion, con su score y sus pilares.</summary>
    public async Task<ExportFile> PortfolioAsync(ApplicationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var applicationsTask = system.GetAllApplicationsAsync(query, cancellationToken);
        var scoresTask = system.GetAllScoresAsync(cancellationToken);

        await Task.WhenAll(applicationsTask, scoresTask).ConfigureAwait(false);

        var applications = await applicationsTask.ConfigureAwait(false);
        var scores = (await scoresTask.ConfigureAwait(false)).ToDictionary(s => s.EaiCode, StringComparer.Ordinal);

        var csv = new CsvDocument(
            "EAI Code",
            "Application",
            "Status",
            "LOB",
            "Sub-LOB",
            "Region",
            "Country",
            "CIO",
            "CRE",
            "Critical application",
            "Disposition",
            "Roadmap",
            "Score",
            "Score band",
            "Available weight",
            "Modernization Risk Vectors",
            "Architecture & Maturity",
            "Stability",
            "Rules version");

        foreach (var application in applications)
        {
            scores.TryGetValue(application.EaiCode, out var score);

            csv.AddRow(
                CsvCell.Text(application.EaiCode),
                CsvCell.Text(application.ApplicationName),
                CsvCell.Text(application.ApplicationStatus),
                CsvCell.Text(application.Lob),
                CsvCell.Text(application.SubLob),
                CsvCell.Text(application.Region),
                CsvCell.Text(application.Country),
                CsvCell.Text(application.Cio),
                CsvCell.Text(application.Cre),
                CsvCell.YesNo(application.CriticalApplication),
                CsvCell.Text(application.Disposition),
                CsvCell.Text(application.Roadmap),
                CsvCell.Number(score?.EffectiveScore),
                CsvCell.Text(score is null ? null : BandName(ScoreBands.Of(score.EffectiveScore))),
                CsvCell.Number(score?.Denominator),
                CsvCell.Number(PillarScore(score, "risk")),
                CsvCell.Number(PillarScore(score, "architecture")),
                CsvCell.Number(PillarScore(score, "stability")),
                CsvCell.Text(score?.RulesetVersion));
        }

        return new ExportFile(FileName("portfolio"), csv);
    }

    /// <summary>El plan del portafolio filtrado, una fila por grupo de la dimension pedida.</summary>
    public async Task<ExportFile> RoadmapAsync(
        ApplicationQuery query,
        string? groupBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var summary = await system.GetPortfolioRoadmapAsync(query, groupBy, cancellationToken).ConfigureAwait(false);

        var csv = new CsvDocument(
            "Grouping",
            "Group",
            "Applications",
            "Share of portfolio (%)",
            "Average score",
            "Score band",
            "Projected score",
            "Projected band",
            "Projected change",
            "Leaving the portfolio",
            "Without disposition",
            "Average weeks",
            "Alerts",
            "Roadmap mix");

        foreach (var group in summary.Groups)
        {
            csv.AddRow(
                CsvCell.Text(summary.Grouping),
                CsvCell.Text(group.Name),
                CsvCell.Number(group.Count),
                CsvCell.Number(Share(group.Count, summary.TotalApplications)),
                CsvCell.Number(group.AverageScore),
                CsvCell.Text(BandName(ScoreBands.Of(group.AverageScore))),
                CsvCell.Number(group.AverageProjectedScore),
                CsvCell.Text(group.AverageProjectedScore is { } projected ? BandName(ScoreBands.Of(projected)) : null),
                CsvCell.Number(group.AverageDelta),
                CsvCell.Number(group.LeavingPortfolio),
                CsvCell.Number(group.WithoutDisposition),
                CsvCell.Number(group.AverageWeeks),
                CsvCell.Number(group.Alerts),
                CsvCell.Text(string.Join("; ", group.Mix.Select(m =>
                    string.Create(CultureInfo.InvariantCulture, $"{m.Name}: {m.Count}")))));
        }

        return new ExportFile(FileName("roadmap-" + summary.Grouping), csv);
    }

    /// <summary>Cada aplicacion antes y despues entre dos versiones de reglas. Null si alguna no existe.</summary>
    public async Task<ExportFile?> RulesComparisonAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        var rows = await system.GetRulesComparisonRowsAsync(from, to, cancellationToken).ConfigureAwait(false);

        if (rows is null)
        {
            return null;
        }

        var csv = new CsvDocument(
            "EAI Code",
            "Application",
            "Score " + from,
            "Score " + to,
            "Change",
            "Band " + from,
            "Band " + to);

        foreach (var row in rows)
        {
            csv.AddRow(
                CsvCell.Text(row.EaiCode),
                CsvCell.Text(row.ApplicationName),
                CsvCell.Number(row.Before),
                CsvCell.Number(row.After),
                CsvCell.Number(row.Delta),
                CsvCell.Text(BandName(row.BandBefore)),
                CsvCell.Text(BandName(row.BandAfter)));
        }

        return new ExportFile(FileName($"rules-{from}-to-{to}"), csv);
    }

    // -----------------------------------------------------------------------

    private string FileName(string kind)
        => string.Create(CultureInfo.InvariantCulture, $"modscore-{kind}-{clock.GetUtcNow():yyyyMMdd-HHmm}.csv");

    private static string? BandName(string? code)
        => code is null ? null : BandNames.GetValueOrDefault(code, code);

    private static decimal? PillarScore(ScoreResponse? score, string pillar)
        => score?.Pillars.FirstOrDefault(p => string.Equals(p.Key, pillar, StringComparison.Ordinal))?.Score;

    private static decimal Share(int count, int total)
        => total == 0 ? 0m : Math.Round((decimal)count / total * 100m, 1, MidpointRounding.AwayFromZero);
}
