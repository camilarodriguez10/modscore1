using Modernization.Domain;

namespace Modernization.Roadmap;

/// <summary>
/// Agrega los planes futuros de todo el portafolio: cuantas aplicaciones hay en
/// cada camino, como estan hoy, a donde llegarian y donde se contradicen los datos.
/// </summary>
/// <remarks>
/// Desde la Fase 10 el agregado se puede pedir por cualquier dimension —pais, CIO,
/// LOB, disposition, roadmap o aplicacion—. El agrupamiento <b>no cambia el
/// calculo</b>: cada proyeccion sale del mismo planificador y del mismo motor de
/// what-if, y lo unico que cambia es por que criterio se promedian.
/// </remarks>
public sealed class PortfolioRoadmapAnalyzer(RoadmapCatalog catalog, RoadmapPlanner planner)
{
    /// <summary>Cuantos EAI de ejemplo se muestran por cada regla de coherencia.</summary>
    private const int SamplesPerAlert = 5;

    /// <summary>
    /// Clave del grupo de los que no declaran el atributo por el que se agrupa. Vacia
    /// y no "Unassigned": nombrarla es trabajo de la interfaz (ADR 0017), y una clave
    /// vacia tampoco sirve como filtro, que es exactamente lo correcto.
    /// </summary>
    private const string Unassigned = "";

    public PortfolioRoadmapSummary Summarize(
        IReadOnlyList<ApplicationSnapshot> applications,
        DateOnly startDate,
        RoadmapGrouping grouping = RoadmapGrouping.Portfolio)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var plans = new List<ApplicationRoadmapPlan?>(applications.Count);

        foreach (var application in applications)
        {
            try
            {
                plans.Add(planner.Build(application, startDate));
            }
            catch (Scoring.Engine.RulesetException)
            {
                // Una aplicacion cuyo dato no encaja en el ruleset no puede
                // puntuarse; se omite del agregado en vez de tumbar la vista.
                plans.Add(null);
            }
        }

        return Summarize(applications, plans, grouping);
    }

    /// <summary>
    /// El mismo resumen con los planes ya construidos, en el orden de las aplicaciones;
    /// null donde la aplicacion no se pudo puntuar. Lo usa quien necesita los planes para
    /// algo mas, como el impacto de un cambio de reglas: asi se construyen una sola vez.
    /// </summary>
    public PortfolioRoadmapSummary Summarize(
        IReadOnlyList<ApplicationSnapshot> applications,
        IReadOnlyList<ApplicationRoadmapPlan?> built,
        RoadmapGrouping grouping = RoadmapGrouping.Portfolio)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(built);

        if (built.Count != applications.Count)
        {
            throw new ArgumentException("Tiene que haber un plan, o null, por aplicacion.", nameof(built));
        }

        var planned = new List<PlannedApplication>(applications.Count);

        for (var index = 0; index < applications.Count; index++)
        {
            if (built[index] is { } plan)
            {
                planned.Add(new PlannedApplication(applications[index], plan));
            }
        }

        var plans = planned.Select(p => p.Plan).ToList();

        return new PortfolioRoadmapSummary
        {
            TotalApplications = plans.Count,
            WithoutDisposition = plans.Count(p => p.Disposition is null),
            LeavingPortfolio = plans.Count(p => p.LeavesPortfolio),
            Dispositions = SummarizeDispositions(plans),
            Roadmaps = SummarizeRoadmaps(plans),
            Alerts = SummarizeAlerts(plans),
            Grouping = grouping,
            Groups = SummarizeGroups(planned, grouping),
        };
    }

    private IReadOnlyList<DispositionSummary> SummarizeDispositions(List<ApplicationRoadmapPlan> plans)
        => [.. catalog.Dispositions.Select(definition =>
        {
            var owned = plans.Where(p => p.Disposition == definition.Key).ToList();

            return new DispositionSummary
            {
                Disposition = definition.Key,
                Name = definition.Name,
                Definition = definition.Definition,
                Count = owned.Count,
                AverageScore = Average(owned.Select(p => p.Baseline.EffectiveScore)),
            };
        })];

    private IReadOnlyList<RoadmapSummary> SummarizeRoadmaps(List<ApplicationRoadmapPlan> plans)
        => [.. catalog.Roadmaps.Select(definition =>
        {
            var owned = plans.Where(p => p.Roadmap == definition.Key).ToList();

            return new RoadmapSummary
            {
                Roadmap = definition.Key,
                Name = definition.Name,
                Disposition = definition.Disposition,
                Definition = definition.Definition,
                Count = owned.Count,
                AverageScore = Average(owned.Select(p => p.Baseline.EffectiveScore)),
                // Las que salen del portafolio no proyectan mejora: proyectar una
                // seria justificar inversion en algo que se va a apagar.
                AverageProjectedScore = definition.RemovesFromPortfolio
                    ? null
                    : Average(owned.Where(p => p.Projected is not null)
                        .Select(p => p.Projected!.Projected.EffectiveScore)),
                TotalWeeks = definition.TotalWeeks,
                RemovesFromPortfolio = definition.RemovesFromPortfolio,
            };
        })];

    // ------------------------------------------------------------ agrupacion

    /// <summary>
    /// Parte el portafolio por la dimension pedida. Las claves son las mismas que
    /// entienden los filtros, para que hacer clic en un grupo acote el resto de las
    /// pantallas sin traducir nada por el camino.
    /// </summary>
    private IReadOnlyList<RoadmapGroupSummary> SummarizeGroups(
        List<PlannedApplication> planned,
        RoadmapGrouping grouping)
    {
        if (planned.Count == 0)
        {
            return [];
        }

        if (grouping == RoadmapGrouping.Portfolio)
        {
            // Una sola fila: la interfaz la nombra segun la agrupacion.
            return [Build("*", string.Empty, planned)];
        }

        if (grouping == RoadmapGrouping.Application)
        {
            // Por aplicacion, lo util es quien mas gana con su plan.
            return [.. planned
                .OrderByDescending(p => p.Plan.ProjectedDelta)
                .ThenBy(p => p.Plan.EaiCode, StringComparer.Ordinal)
                .Select(p => Build(p.Plan.EaiCode, p.Plan.ApplicationName, [p]))];
        }

        return [.. planned
            .GroupBy(p => KeyOf(p, grouping), StringComparer.Ordinal)
            .Select(group => Build(group.Key, NameOf(group.Key, grouping), [.. group]))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)];
    }

    private static string KeyOf(PlannedApplication entry, RoadmapGrouping grouping) => grouping switch
    {
        RoadmapGrouping.Country => Or(entry.Application.Country),
        RoadmapGrouping.Cio => Or(entry.Application.Cio),
        RoadmapGrouping.Lob => Or(entry.Application.Lob),
        // El nombre de pantalla y no el del enum: es el mismo que emite el inventario,
        // el que muestran los selectores y el que se lee en la URL. Una sola forma de
        // escribir "Maintain (Patch)" en todo el sistema.
        RoadmapGrouping.Disposition => entry.Plan.Disposition is { } d ? MapsNames.Display(d) : Unassigned,
        RoadmapGrouping.Roadmap => entry.Plan.Roadmap is { } r ? MapsNames.Display(r) : Unassigned,
        _ => Unassigned,
    };

    /// <summary>
    /// El nombre legible del grupo. Para disposition y roadmap sale del catalogo
    /// MAPS, que es donde vive el vocabulario; para el resto, la clave ya es el
    /// nombre.
    /// </summary>
    private string NameOf(string key, RoadmapGrouping grouping) => grouping switch
    {
        RoadmapGrouping.Disposition => catalog.Dispositions
            .FirstOrDefault(d => string.Equals(MapsNames.Display(d.Key), key, StringComparison.Ordinal))?.Name ?? key,
        RoadmapGrouping.Roadmap => catalog.Roadmaps
            .FirstOrDefault(r => string.Equals(MapsNames.Display(r.Key), key, StringComparison.Ordinal))?.Name ?? key,
        _ => key,
    };

    private RoadmapGroupSummary Build(string key, string name, IReadOnlyList<PlannedApplication> entries)
    {
        var plans = entries.Select(e => e.Plan).ToList();
        var projected = plans.Where(p => p.Projected is not null).ToList();

        return new RoadmapGroupSummary
        {
            Key = key,
            Name = name,
            Count = plans.Count,
            AverageScore = Average(plans.Select(p => p.Baseline.EffectiveScore)),

            // Solo promedian las que proyectan: mezclar un Retire como si su score
            // se quedara igual hundiria el promedio de un grupo que en realidad se
            // esta apagando a proposito.
            AverageProjectedScore = projected.Count == 0
                ? null
                : Average(projected.Select(p => p.Projected!.Projected.EffectiveScore)),
            AverageDelta = projected.Count == 0 ? null : Average(projected.Select(p => p.ProjectedDelta)),

            LeavingPortfolio = plans.Count(p => p.LeavesPortfolio),
            WithoutDisposition = plans.Count(p => p.Disposition is null),
            AverageWeeks = plans.Count == 0
                ? 0
                : (int)Math.Round(plans.Average(p => (double)p.TotalWeeks), MidpointRounding.AwayFromZero),
            Alerts = plans.Sum(p => p.Alerts.Count),
            Mix = MixOf(plans),
        };
    }

    /// <summary>De que roadmaps esta hecho un grupo. Es lo que explica su promedio.</summary>
    private IReadOnlyList<RoadmapMixEntry> MixOf(List<ApplicationRoadmapPlan> plans)
        => [.. plans
            .Where(p => p.Roadmap is not null)
            .GroupBy(p => p.Roadmap!.Value)
            .Select(g => new RoadmapMixEntry(
                MapsNames.Display(g.Key),
                catalog.Roadmaps.FirstOrDefault(r => r.Key == g.Key)?.Name ?? g.Key.ToString(),
                g.Count()))
            .OrderByDescending(m => m.Count)
            .ThenBy(m => m.Roadmap, StringComparer.Ordinal)];

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Unassigned : value;

    // -----------------------------------------------------------------------

    private static IReadOnlyList<AlertSummary> SummarizeAlerts(List<ApplicationRoadmapPlan> plans)
        => [.. plans
            .SelectMany(plan => plan.Alerts.Select(alert => (plan.EaiCode, alert.Rule)))
            .GroupBy(entry => entry.Rule.Key, StringComparer.Ordinal)
            .Select(group => new AlertSummary
            {
                RuleKey = group.Key,
                Severity = group.First().Rule.Severity,
                Title = group.First().Rule.Title,
                Count = group.Count(),
                SampleEaiCodes = [.. group.Select(e => e.EaiCode).Order(StringComparer.Ordinal).Take(SamplesPerAlert)],
            })
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.Count)];

    private static decimal Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0m : Math.Round(list.Average(), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Una aplicacion junto a su plan. Agrupar por pais o por CIO necesita los
    /// atributos de la aplicacion, que el plan no lleva.
    /// </summary>
    private sealed record PlannedApplication(ApplicationSnapshot Application, ApplicationRoadmapPlan Plan);
}
