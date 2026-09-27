using System.Globalization;
using Modernization.Domain;
using Modernization.Domain.Code;

namespace Modernization.Insights;

/// <summary>
/// Cruza lo que declaran los repositorios con el catálogo de fin de vida y las reglas de la
/// pantalla Code. No lee datos ni conoce GitHub: recibe el inventario ya armado.
/// </summary>
/// <remarks>
/// Solo entran los repositorios de las aplicaciones que recibe, que ya vienen recortadas
/// por el alcance del usuario y por el filtro: un repositorio de una aplicación que el
/// usuario no ve no aparece en ningún conteo (ADR 0012).
/// </remarks>
public static class CodeAnalyzer
{
    private const string NoRepositories = "none";

    private static readonly string[] Levels = ["critical", "high", "moderate", "low"];

    public static CodePortfolio Analyze(
        IReadOnlyList<ApplicationSnapshot> applications,
        CodeInventorySet inventory,
        EndOfLifeCatalog catalog,
        DashboardRules rules,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rules);

        var byApplication = RepositoriesOf(applications, inventory);
        var assessed = applications
            .Select(a => Assess(a, byApplication[a.EaiCode], catalog, rules, asOf))
            .ToList();

        var components = assessed.SelectMany(a => a.Repositories.SelectMany(r => r.Components.Select(c => (a, r, c)))).ToList();
        var dependencies = assessed
            .SelectMany(a => a.Repositories.SelectMany(r => r.Dependencies.Select(d => (Application: a, Repository: r, Dependency: d))))
            .ToList();
        var libraries = Libraries(dependencies);

        var kpis = new CodeKpis(
            applications.Count,
            assessed.Count(a => a.Repositories.Count > 0),
            assessed.Sum(a => a.Repositories.Count),
            components.Count,
            components.Count(x => x.c.Status == EndOfLifeStatus.EndOfLife),
            assessed.Count(a => a.Summary.EolComponents > 0),
            components.Count(x => x.c.Status == EndOfLifeStatus.Approaching),
            assessed.Count(a => a.Summary.ApproachingComponents > 0),
            components.Count(x => x.c.Status == EndOfLifeStatus.Unknown),
            assessed.Sum(a => a.Summary.CriticalAlerts),
            assessed.Sum(a => a.Summary.HighAlerts),
            assessed.Count(a => a.Summary.CriticalAlerts + a.Summary.HighAlerts > 0),
            libraries.Count,
            assessed.Count(a => a.Summary.RiskLevel is "high" or "critical"),
            assessed.Count(a => a.Summary.Mismatch is not null));

        var statuses = EndOfLifeStatus.Ordered
            .Select(status => new CodeStatusShare(
                status,
                components.Count(x => x.c.Status == status),
                components.Where(x => x.c.Status == status).Select(x => x.a.Summary.EaiCode).Distinct(StringComparer.Ordinal).Count()))
            .ToList();

        var lifecycle = components
            .GroupBy(x => (x.c.Product, Cycle: x.c.Cycle ?? x.c.Version))
            .Select(g =>
            {
                var first = g.First().c;
                return new CodeLifecycleRow(
                    first.Product,
                    first.ProductName,
                    first.Kind,
                    g.Key.Cycle,
                    first.CycleLabel,
                    first.Eol,
                    first.SupportEnds,
                    first.ExtendedSupportEnds,
                    first.Status == EndOfLifeStatus.EndOfLife && first.Eol is null,
                    first.Status,
                    first.MonthsToEol,
                    g.Select(x => x.r.Name).Distinct(StringComparer.Ordinal).Count(),
                    g.Select(x => x.a.Summary.EaiCode).Distinct(StringComparer.Ordinal).Count(),
                    first.SourceUrl);
            })
            .OrderBy(row => EndOfLifeStatus.Rank(row.Status))
            .ThenBy(row => row.Eol ?? DateOnly.MaxValue)
            .ThenByDescending(row => row.Applications)
            .ThenBy(row => row.ProductName, StringComparer.Ordinal)
            .ThenBy(row => row.Cycle, StringComparer.Ordinal)
            .ToList();

        var withoutRepositories = assessed.Count(a => a.Repositories.Count == 0);
        var levels = Levels
            .Select(level => (Level: level, Count: assessed.Count(a => a.Summary.RiskLevel == level)))
            .Append((Level: NoRepositories, Count: withoutRepositories))
            .Select(x => new CodeLevelShare(x.Level, x.Count, Share(x.Count, applications.Count)))
            .ToList();

        var ranked = assessed
            .Select(a => a.Summary)
            .OrderByDescending(a => a.RiskIndex ?? -1m)
            .ThenByDescending(a => a.EolComponents)
            .ThenBy(a => a.EaiCode, StringComparer.Ordinal)
            .ToList();

        var outlook = CodeOutlook.Years
            .Select((years, i) =>
            {
                var on = asOf.AddYears(years);
                var horizons = assessed.Select(a => (a.Summary, Horizon: a.Outlook[i])).ToList();
                var at = components
                    .Select(x => (x.a.Summary.EaiCode, Status: CodeOutlook.StatusAt(x.c, catalog, on, rules.Code.ApproachingMonths)))
                    .ToList();
                var assessable = at.Count(x => x.Status != EndOfLifeStatus.Unknown);
                var eol = at.Count(x => x.Status == EndOfLifeStatus.EndOfLife);
                var indexes = horizons.Where(h => h.Horizon.RiskIndex is not null).Select(h => h.Horizon.RiskIndex!.Value).ToList();

                return new CodeHorizon(
                    years,
                    on,
                    eol,
                    horizons.Count(h => h.Horizon.EolComponents > 0),
                    CodeOutlook.Currency(assessable - eol, assessable),
                    horizons.Count(h => h.Horizon.RiskLevel is "high" or "critical"),
                    indexes.Count == 0 ? null : Math.Round(indexes.Average(), 1, MidpointRounding.AwayFromZero),
                    [.. EndOfLifeStatus.Ordered.Select(status => new CodeStatusShare(
                        status,
                        at.Count(x => x.Status == status),
                        at.Where(x => x.Status == status).Select(x => x.EaiCode).Distinct(StringComparer.Ordinal).Count()))]);
            })
            .ToList();

        var plan = CodeOutlook.Plan(
            [.. assessed.SelectMany(a => a.Suggestions.Select(s => (a.Summary.EaiCode, s)))],
            rules.Code.TopActions);

        return new CodePortfolio(
            asOf,
            kpis,
            statuses,
            lifecycle,
            [.. libraries.Take(rules.Code.TopLibraries)],
            levels,
            ranked,
            outlook,
            plan);
    }

    /// <summary>Una aplicación con todos sus repositorios y de dónde sale cada punto de su índice.</summary>
    public static CodeApplicationDetail Detail(
        ApplicationSnapshot application,
        CodeInventorySet inventory,
        EndOfLifeCatalog catalog,
        DashboardRules rules,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(inventory);

        var repositories = RepositoriesOf([application], inventory)[application.EaiCode];
        var assessed = Assess(application, repositories, catalog, rules, asOf);

        return new CodeApplicationDetail(
            assessed.Summary,
            assessed.Factors,
            assessed.Repositories,
            assessed.Outlook,
            assessed.Suggestions);
    }

    // -------------------------------------------------------------- por aplicación

    private sealed record Assessed(
        CodeApplication Summary,
        IReadOnlyList<CodeRiskFactor> Factors,
        IReadOnlyList<CodeRepository> Repositories,
        IReadOnlyList<CodeApplicationHorizon> Outlook,
        IReadOnlyList<CodeSuggestion> Suggestions);

    private static Dictionary<string, List<RepositoryInventory>> RepositoriesOf(
        IReadOnlyList<ApplicationSnapshot> applications,
        CodeInventorySet inventory)
    {
        var result = applications
            .Select(a => a.EaiCode)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(code => code, _ => new List<RepositoryInventory>(), StringComparer.Ordinal);

        foreach (var repository in inventory.Repositories)
        {
            if (result.TryGetValue(repository.EaiCode, out var list))
            {
                list.Add(repository);
            }
        }

        return result;
    }

    private static Assessed Assess(
        ApplicationSnapshot application,
        IReadOnlyList<RepositoryInventory> repositories,
        EndOfLifeCatalog catalog,
        DashboardRules rules,
        DateOnly asOf)
    {
        var code = rules.Code;
        var staleBefore = asOf.AddMonths(-code.StaleMonths);

        var views = repositories
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(repository => new CodeRepository(
                repository.Name,
                repository.DefaultBranch,
                repository.PrimaryLanguage,
                repository.LastCommit,
                repository.Archived,
                repository.Archived || (repository.LastCommit is { } last && last < staleBefore),
                [.. repository.Components
                    .Select(c => Component(c, catalog, code, asOf))
                    .OrderBy(c => EndOfLifeStatus.Rank(c.Status))
                    .ThenBy(c => c.ProductName, StringComparer.Ordinal)],
                [.. repository.Dependencies
                    .Select(d => Dependency(d, code))
                    .OrderByDescending(d => d.Critical)
                    .ThenByDescending(d => d.High)
                    .ThenByDescending(d => d.Deprecated)
                    .ThenByDescending(d => d.MajorsBehind ?? 0)
                    .ThenBy(d => d.Name, StringComparer.Ordinal)]))
            .ToList();

        var components = views.SelectMany(r => r.Components).ToList();
        var dependencies = views.SelectMany(r => r.Dependencies).ToList();

        var eol = components.Count(c => c.Status == EndOfLifeStatus.EndOfLife);
        var approaching = components.Count(c => c.Status == EndOfLifeStatus.Approaching);
        var unknown = components.Count(c => c.Status == EndOfLifeStatus.Unknown);
        var critical = dependencies.Sum(d => d.Critical);
        var high = dependencies.Sum(d => d.High);
        // Una biblioteca obsoleta ya dice todo lo que diria estar desactualizada: cuenta una vez.
        var deprecated = dependencies.Count(d => d.Deprecated);
        var outdated = dependencies.Count(d => d.Outdated && !d.Deprecated);
        var stale = views.Count(r => r.Stale);

        var factors = new List<CodeRiskFactor>
        {
            Factor(CodeFactors.EolComponent, eol, code.Points.EolComponent),
            Factor(CodeFactors.ApproachingComponent, approaching, code.Points.ApproachingComponent),
            Factor(CodeFactors.CriticalVulnerability, critical, code.Points.CriticalVulnerability),
            Factor(CodeFactors.HighVulnerability, high, code.Points.HighVulnerability),
            Factor(CodeFactors.DeprecatedLibrary, deprecated, code.Points.DeprecatedLibrary),
            Factor(CodeFactors.OutdatedLibrary, outdated, code.Points.OutdatedLibrary),
            Factor(CodeFactors.StaleRepository, stale, code.Points.StaleRepository),
        };

        decimal? index = views.Count == 0 ? null : factors.Sum(f => f.Total);
        var currency = application.RawTechScore is { } score and > 0m
            ? Math.Round(score, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var legacy = currency < rules.Currency.LegacyBelow;

        string? mismatch = null;

        if (views.Count > 0 && currency is not null)
        {
            if (!legacy && eol > 0)
            {
                mismatch = CodeMismatch.HiddenObsolescence;
            }
            else if (legacy && components.Count > 0 && eol == 0)
            {
                mismatch = CodeMismatch.InventoryBehind;
            }
        }

        var worst = components.Count == 0
            ? null
            : components.MinBy(c => EndOfLifeStatus.Rank(c.Status))!.Status;

        var outlook = CodeOutlook.ForApplication(
            views,
            [.. repositories.Select(r => new RepositoryFacts(r.Archived, r.LastCommit))],
            catalog,
            rules,
            asOf,
            critical,
            high,
            deprecated,
            outdated);
        var suggestions = CodeOutlook.Suggestions(views, catalog, rules, asOf);

        var summary = new CodeApplication(
            application.EaiCode,
            application.ApplicationName,
            application.Lob,
            application.Country,
            application.CriticalApplicationIndicator == true,
            currency,
            legacy,
            views.Count,
            components.Count,
            eol,
            approaching,
            unknown,
            critical,
            high,
            deprecated,
            outdated,
            stale,
            index,
            index is { } value ? code.LevelOf(value) : null,
            worst,
            mismatch,
            outlook[^1].EolComponents,
            outlook[^1].RiskLevel);

        return new Assessed(summary, factors, views, outlook, suggestions);
    }

    private static CodeRiskFactor Factor(string key, int count, decimal points)
        => new(key, count, points, count * points);

    private static CodeComponent Component(RepositoryComponent component, EndOfLifeCatalog catalog, CodeRules rules, DateOnly asOf)
    {
        var product = catalog.Find(component.Product);
        var cycle = product is null ? null : catalog.Match(component.Product, component.Version);

        return new CodeComponent(
            component.Product,
            product?.Name ?? component.Product,
            product?.Kind ?? EndOfLifeStatus.Unknown,
            component.Version,
            component.DetectedIn,
            cycle?.Cycle,
            cycle?.Label,
            cycle?.Eol,
            cycle?.SupportEnds,
            cycle?.ExtendedSupportEnds,
            cycle?.MonthsToEol(asOf),
            cycle?.StatusOn(asOf, rules.ApproachingMonths) ?? EndOfLifeStatus.Unknown,
            product?.SourceUrl);
    }

    private static CodeDependency Dependency(RepositoryDependency dependency, CodeRules rules)
    {
        var behind = MajorsBehind(dependency.Version, dependency.Latest);

        return new CodeDependency(
            dependency.Ecosystem,
            dependency.Name,
            dependency.Version,
            dependency.Latest,
            behind,
            dependency.Direct,
            dependency.Deprecated,
            behind >= rules.OutdatedMajorsBehind,
            Math.Max(0, dependency.Critical),
            Math.Max(0, dependency.High),
            Math.Max(0, dependency.Medium),
            Math.Max(0, dependency.Low));
    }

    // -------------------------------------------------------------- bibliotecas

    private static List<CodeLibraryRow> Libraries(
        IReadOnlyList<(Assessed Application, CodeRepository Repository, CodeDependency Dependency)> usages)
        => [.. usages
            .GroupBy(x => (x.Dependency.Ecosystem, x.Dependency.Name))
            .Select(g => new CodeLibraryRow(
                g.Key.Ecosystem,
                g.Key.Name,
                [.. g.Select(x => x.Dependency.Version).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal)],
                g.Select(x => x.Dependency.Latest).FirstOrDefault(v => v is not null),
                g.Max(x => x.Dependency.MajorsBehind),
                g.Any(x => x.Dependency.Deprecated),
                g.Any(x => x.Dependency.Outdated),
                g.Sum(x => x.Dependency.Critical),
                g.Sum(x => x.Dependency.High),
                g.Select(x => x.Repository.Name).Distinct(StringComparer.Ordinal).Count(),
                g.Select(x => x.Application.Summary.EaiCode).Distinct(StringComparer.Ordinal).Count()))
            .Where(row => row.Deprecated || row.Outdated || row.Critical + row.High > 0)
            .OrderByDescending(row => row.Critical)
            .ThenByDescending(row => row.High)
            .ThenByDescending(row => row.Deprecated)
            .ThenByDescending(row => row.Applications)
            .ThenByDescending(row => row.MajorsBehind ?? 0)
            .ThenBy(row => row.Name, StringComparer.Ordinal)];

    /// <summary>
    /// Versiones mayores entre la usada y la última. Toma el primer número de cada una:
    /// <c>5.2.10.Final</c> es 5, <c>33.2.1-jre</c> es 33, <c>v0.0.0-2020…</c> es 0. Null
    /// si falta la última o alguna no empieza con un número.
    /// </summary>
    public static int? MajorsBehind(string version, string? latest)
    {
        if (latest is null || Major(version) is not { } used || Major(latest) is not { } newest)
        {
            return null;
        }

        return Math.Max(0, newest - used);
    }

    private static int? Major(string version)
    {
        var trimmed = version.Trim().TrimStart('v', 'V');
        var digits = new string([.. trimmed.TakeWhile(char.IsAsciiDigit)]);

        return digits.Length is > 0 and <= 6 ? int.Parse(digits, CultureInfo.InvariantCulture) : null;
    }

    private static decimal Share(int part, int whole)
        => whole == 0 ? 0m : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
}
