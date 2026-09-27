namespace Modernization.Insights;

/// <summary>
/// Lo que pasa con el código de una aplicación si nadie lo toca —hoy, en uno, dos y tres
/// años— y qué habría que cambiar, en orden de urgencia.
/// </summary>
/// <remarks>
/// <para>
/// <b>Si nada cambia</b> quiere decir: las mismas versiones, las mismas alertas y ningún
/// commit más. Lo único que se mueve es el calendario: cada componente se evalúa contra su
/// fecha de fin de vida en la fecha del horizonte, y un repositorio sin commits cruza el
/// umbral de abandono con el tiempo. No se inventan vulnerabilidades futuras.
/// </para>
/// <para>
/// El destino de cada actualización sale del mismo catálogo: el ciclo con soporte que más
/// dura, prefiriendo LTS cuando el producto las tiene, y que no venza dentro de la ventana
/// de «ending soon». Si no hay ninguno —AngularJS—, el destino es el producto que lo
/// reemplaza.
/// </para>
/// </remarks>
internal static class CodeOutlook
{
    /// <summary>Los horizontes, en años desde hoy.</summary>
    public static readonly IReadOnlyList<int> Years = [0, 1, 2, 3];

    /// <summary>Productos sin ciclo con soporte y el que los reemplaza.</summary>
    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["angularjs"] = "angular",
    };

    // -------------------------------------------------------------- horizontes

    /// <summary>El estado de un componente en una fecha. Sin ciclo en el catálogo, no se sabe.</summary>
    public static string StatusAt(CodeComponent component, EndOfLifeCatalog catalog, DateOnly on, int approachingMonths)
        => catalog.Match(component.Product, component.Version)?.StatusOn(on, approachingMonths) ?? EndOfLifeStatus.Unknown;

    /// <summary>
    /// La aplicación en cada horizonte. <c>Currency</c> es la parte de los componentes
    /// evaluables que sigue con soporte: 100 es todo vigente, 0 todo vencido.
    /// </summary>
    public static IReadOnlyList<CodeApplicationHorizon> ForApplication(
        IReadOnlyList<CodeRepository> repositories,
        IReadOnlyList<RepositoryFacts> facts,
        EndOfLifeCatalog catalog,
        DashboardRules rules,
        DateOnly asOf,
        int critical,
        int high,
        int deprecated,
        int outdated)
    {
        var code = rules.Code;
        var result = new List<CodeApplicationHorizon>(Years.Count);

        foreach (var years in Years)
        {
            var on = asOf.AddYears(years);
            var statuses = repositories
                .SelectMany(r => r.Components)
                .Select(c => StatusAt(c, catalog, on, code.ApproachingMonths))
                .ToList();

            var eol = statuses.Count(s => s == EndOfLifeStatus.EndOfLife);
            var approaching = statuses.Count(s => s == EndOfLifeStatus.Approaching);
            var assessable = statuses.Count(s => s != EndOfLifeStatus.Unknown);
            var stale = facts.Count(f => f.IsStaleOn(on, code.StaleMonths));

            decimal? index = repositories.Count == 0
                ? null
                : (eol * code.Points.EolComponent)
                    + (approaching * code.Points.ApproachingComponent)
                    + (critical * code.Points.CriticalVulnerability)
                    + (high * code.Points.HighVulnerability)
                    + (deprecated * code.Points.DeprecatedLibrary)
                    + (outdated * code.Points.OutdatedLibrary)
                    + (stale * code.Points.StaleRepository);

            result.Add(new CodeApplicationHorizon(
                years,
                on,
                statuses.Count,
                eol,
                approaching,
                Currency(assessable - eol, assessable),
                index,
                index is { } value ? code.LevelOf(value) : null));
        }

        return result;
    }

    public static decimal? Currency(int supported, int assessable)
        => assessable == 0 ? null : Math.Round(supported * 100m / assessable, 1, MidpointRounding.AwayFromZero);

    // -------------------------------------------------------------- sugerencias

    /// <summary>Qué cambiar en una aplicación, de lo más urgente a lo que puede esperar.</summary>
    public static IReadOnlyList<CodeSuggestion> Suggestions(
        IReadOnlyList<CodeRepository> repositories,
        EndOfLifeCatalog catalog,
        DashboardRules rules,
        DateOnly asOf)
    {
        var code = rules.Code;
        var points = code.Points;
        var suggestions = new List<CodeSuggestion>();

        foreach (var repository in repositories)
        {
            foreach (var component in repository.Components)
            {
                if (ForComponent(repository.Name, component, catalog, code, asOf) is { } suggestion)
                {
                    suggestions.Add(suggestion);
                }
            }

            foreach (var dependency in repository.Dependencies)
            {
                var advisories = (dependency.Critical * points.CriticalVulnerability) + (dependency.High * points.HighVulnerability);

                if (dependency.Deprecated)
                {
                    suggestions.Add(new CodeSuggestion(
                        CodeActions.ReplaceLibrary, CodeUrgency.Now, repository.Name, dependency.Name, dependency.Ecosystem,
                        dependency.Version, null, null, null, null, points.DeprecatedLibrary + advisories, 0m));
                }
                else if (dependency.Critical + dependency.High > 0 || dependency.Outdated)
                {
                    suggestions.Add(new CodeSuggestion(
                        CodeActions.UpdateLibrary,
                        dependency.Critical + dependency.High > 0 ? CodeUrgency.Now : CodeUrgency.Planned,
                        repository.Name, dependency.Name, dependency.Ecosystem,
                        dependency.Version, dependency.Name, dependency.Latest, null, null,
                        advisories + (dependency.Outdated ? points.OutdatedLibrary : 0m), 0m));
                }
            }

            if (repository.Stale)
            {
                suggestions.Add(new CodeSuggestion(
                    repository.Archived ? CodeActions.RetireRepository : CodeActions.MaintainRepository,
                    CodeUrgency.Now, repository.Name, repository.Name, null,
                    repository.LastCommit?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    null, null, null, null, points.StaleRepository, 0m));
            }
        }

        return [.. suggestions
            .OrderBy(s => CodeUrgency.Rank(s.Urgency))
            .ThenByDescending(s => s.PointsNow + s.PointsAvoided)
            .ThenBy(s => s.Deadline ?? DateOnly.MaxValue)
            .ThenBy(s => s.Subject, StringComparer.Ordinal)
            .ThenBy(s => s.Repository, StringComparer.Ordinal)];
    }

    private static CodeSuggestion? ForComponent(
        string repository,
        CodeComponent component,
        EndOfLifeCatalog catalog,
        CodeRules code,
        DateOnly asOf)
    {
        var cycle = catalog.Match(component.Product, component.Version);

        if (cycle is null)
        {
            return null;
        }

        var urgency = cycle.Discontinued || (cycle.Eol is { } past && past <= asOf) ? CodeUrgency.Now
            : cycle.Eol is not { } eol ? (string?)null
            : eol <= asOf.AddYears(1) ? CodeUrgency.Year1
            : eol <= asOf.AddYears(2) ? CodeUrgency.Year2
            : eol <= asOf.AddYears(3) ? CodeUrgency.Year3
            : null;

        if (urgency is null)
        {
            return null;
        }

        var status = cycle.StatusOn(asOf, code.ApproachingMonths);
        var now = status switch
        {
            EndOfLifeStatus.EndOfLife => code.Points.EolComponent,
            EndOfLifeStatus.Approaching => code.Points.ApproachingComponent,
            _ => 0m,
        };
        var avoided = Math.Max(0m, code.Points.EolComponent - now);

        var product = catalog.Find(component.Product)!;
        var target = BestCycle(product, asOf, code.ApproachingMonths);

        string kind;
        EndOfLifeProduct? targetProduct = product;

        if (target is not null && target.Cycle != cycle.Cycle)
        {
            kind = CodeActions.Upgrade;
        }
        else if (Replacements.TryGetValue(product.Key, out var replacement) && catalog.Find(replacement) is { } other
            && BestCycle(other, asOf, code.ApproachingMonths) is { } next)
        {
            kind = CodeActions.Migrate;
            targetProduct = other;
            target = next;
        }
        else
        {
            kind = CodeActions.AwaitRelease;
            target = null;
        }

        return new CodeSuggestion(
            kind,
            urgency,
            repository,
            component.ProductName,
            null,
            cycle.Label ?? cycle.Cycle,
            target is null ? null : targetProduct!.Name,
            target is null ? null : target.Label ?? target.Cycle,
            cycle.Eol,
            target?.Eol,
            now,
            avoided);
    }

    /// <summary>
    /// El ciclo al que conviene ir: publicado, sin discontinuar, que no venza dentro de la
    /// ventana (o, si ninguno la pasa, que al menos no esté vencido), LTS si el producto
    /// las tiene, y el que más dura.
    /// </summary>
    public static EndOfLifeCycle? BestCycle(EndOfLifeProduct product, DateOnly asOf, int approachingMonths)
    {
        var released = product.Cycles
            .Where(c => !c.Discontinued && (c.ReleaseDate is null || c.ReleaseDate <= asOf))
            .ToList();

        var horizon = asOf.AddMonths(approachingMonths);
        var pool = released.Where(c => c.Eol is null || c.Eol > horizon).ToList();

        if (pool.Count == 0)
        {
            pool = [.. released.Where(c => c.Eol is null || c.Eol > asOf)];
        }

        if (pool.Count == 0)
        {
            return null;
        }

        if (pool.Any(c => c.Lts))
        {
            pool = [.. pool.Where(c => c.Lts)];
        }

        return pool
            .OrderByDescending(c => c.Eol ?? DateOnly.MaxValue)
            .ThenByDescending(c => c.ReleaseDate ?? DateOnly.MinValue)
            .First();
    }

    // -------------------------------------------------------------- plan del portafolio

    /// <summary>
    /// Las sugerencias de todas las aplicaciones juntas, por acción: «Node.js 14 → 26 en 7
    /// repositorios» dice más que siete filas iguales. Los repositorios abandonados quedan
    /// en el detalle de cada aplicación.
    /// </summary>
    public static IReadOnlyList<CodeActionRow> Plan(
        IReadOnlyList<(string EaiCode, CodeSuggestion Suggestion)> suggestions,
        int top)
        => [.. suggestions
            .Where(x => x.Suggestion.Kind is not (CodeActions.MaintainRepository or CodeActions.RetireRepository))
            .GroupBy(x => (
                x.Suggestion.Kind,
                x.Suggestion.Subject,
                x.Suggestion.Ecosystem,
                Current: x.Suggestion.Ecosystem is null ? x.Suggestion.Current : string.Empty,
                x.Suggestion.TargetSubject,
                x.Suggestion.Target))
            .Select(g => new CodeActionRow(
                g.Key.Kind,
                g.Select(x => x.Suggestion.Urgency).MinBy(CodeUrgency.Rank)!,
                g.Key.Subject,
                g.Key.Ecosystem,
                g.Key.Ecosystem is null
                    ? g.Key.Current
                    : string.Join(", ", g.Select(x => x.Suggestion.Current).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(3)),
                g.Key.TargetSubject,
                g.Key.Target,
                g.Select(x => x.Suggestion.Deadline).Min(),
                g.First().Suggestion.TargetEol,
                g.Select(x => x.Suggestion.Repository).Distinct(StringComparer.Ordinal).Count(),
                g.Select(x => x.EaiCode).Distinct(StringComparer.Ordinal).Count(),
                g.Sum(x => x.Suggestion.PointsNow),
                g.Sum(x => x.Suggestion.PointsAvoided)))
            .OrderBy(row => CodeUrgency.Rank(row.Urgency))
            .ThenByDescending(row => row.PointsNow + row.PointsAvoided)
            .ThenByDescending(row => row.Applications)
            .ThenBy(row => row.Subject, StringComparer.Ordinal)
            .Take(top)];
}

/// <summary>Lo que hace falta de un repositorio para saber si estará abandonado en una fecha.</summary>
internal sealed record RepositoryFacts(bool Archived, DateOnly? LastCommit)
{
    public bool IsStaleOn(DateOnly on, int staleMonths)
        => Archived || (LastCommit is { } last && last < on.AddMonths(-staleMonths));
}

/// <summary>Las acciones que puede sugerir la pantalla Code.</summary>
public static class CodeActions
{
    /// <summary>A un ciclo con soporte del mismo producto.</summary>
    public const string Upgrade = "upgrade";

    /// <summary>A otro producto, porque el actual no tiene ningún ciclo con soporte.</summary>
    public const string Migrate = "migrate";

    /// <summary>Por vencer, pero todavía no hay un ciclo más nuevo con soporte.</summary>
    public const string AwaitRelease = "awaitRelease";

    public const string UpdateLibrary = "updateLibrary";
    public const string ReplaceLibrary = "replaceLibrary";
    public const string MaintainRepository = "maintainRepository";
    public const string RetireRepository = "retireRepository";
}

/// <summary>Cuándo hay que hacerlo.</summary>
public static class CodeUrgency
{
    /// <summary>Ya vencido, alertas abiertas, obsoleto o abandonado.</summary>
    public const string Now = "now";

    public const string Year1 = "year1";
    public const string Year2 = "year2";
    public const string Year3 = "year3";

    /// <summary>Desactualizado sin fecha que lo apure.</summary>
    public const string Planned = "planned";

    public static readonly IReadOnlyList<string> Ordered = [Now, Year1, Year2, Year3, Planned];

    public static int Rank(string urgency)
    {
        var index = Ordered.ToList().IndexOf(urgency);
        return index < 0 ? Ordered.Count : index;
    }
}
