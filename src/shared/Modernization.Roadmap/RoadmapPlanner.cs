using System.Runtime.CompilerServices;
using Modernization.Domain;
using Modernization.Scoring.Engine;
using Modernization.WhatIf;

namespace Modernization.Roadmap;

/// <summary>
/// Construye el plan futuro de una aplicacion a partir de su disposition y su
/// roadmap.
/// </summary>
/// <remarks>
/// El score proyectado no es un numero escrito a mano: sale de aplicar al motor
/// de what-if las palancas que ese roadmap entrega de forma natural. Si el modelo
/// de scoring cambia, la proyeccion cambia con el.
/// <para>
/// Determinista: la fecha de inicio entra como parametro, no se lee del reloj.
/// </para>
/// </remarks>
public sealed class RoadmapPlanner(RoadmapCatalog catalog, WhatIfEngine whatIf, ScoringEngine engine)
{
    // El plan hacia el objetivo cuesta varios calculos por aplicacion. Un planificador vive con
    // unas reglas, y una aplicacion es la misma instancia mientras su carga siga en memoria: se
    // calcula una vez por aplicacion y reglas, y se libera con la carga.
    private readonly ConditionalWeakTable<ApplicationSnapshot, TargetPlanCache> _targets = [];

    /// <summary>
    /// El plan de una aplicación hacia el objetivo del catálogo (<c>automaticPlanTarget</c>, 90):
    /// la misma búsqueda que el what-if a ese objetivo, así que los dos dan el mismo plan
    /// (2026-10-21). Null si la aplicación se va a eliminar —proyectar una mejora ahí
    /// justificaría invertir en algo que se apaga— o si los planes automáticos están apagados.
    /// </summary>
    public TargetResult? TargetPlan(ApplicationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Cached(snapshot)?.Target;
    }

    /// <summary>
    /// El future plan detallado de una aplicación: cada paso del plan hacia el objetivo con su
    /// métrica, su valor de hoy y el objetivo, los puntos que suma, quién lo hace y las acciones
    /// concretas. Null si la aplicación no tiene plan (se va a eliminar, o los planes
    /// automáticos están apagados).
    /// </summary>
    public ActionPlan? ActionPlan(ApplicationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Cached(snapshot) is { } cached ? ActionPlanBuilder.Build(snapshot, cached.Target, whatIf, engine) : null;
    }

    /// <summary>
    /// Calcula de antemano el plan de cada aplicación: se llama al publicar una carga, para
    /// que el plan ya esté cuando alguien lo abre.
    /// </summary>
    public int Prepare(IEnumerable<ApplicationSnapshot> applications, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var prepared = 0;

        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Cached(application) is not null)
                {
                    prepared++;
                }
            }
            catch (RulesetException)
            {
                // Una aplicación que el motor no puede puntuar no tiene plan; su detalle dice por qué.
            }
        }

        return prepared;
    }

    private TargetPlanCache? Cached(ApplicationSnapshot snapshot)
    {
        if (snapshot.Disposition == ApplicationDisposition.Eliminate
            || catalog.Parameters.AutomaticPlanTarget is not { } goal)
        {
            return null;
        }

        return _targets.GetValue(snapshot, s =>
        {
            var target = whatIf.ReachTarget(s, goal);
            return new TargetPlanCache(target, target.Result ?? whatIf.Evaluate(s, []));
        });
    }

    public ApplicationRoadmapPlan Build(ApplicationSnapshot snapshot, DateOnly startDate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var baseline = engine.Calculate(snapshot);
        var alerts = CheckCoherence(snapshot, baseline);
        var targetPlan = Cached(snapshot);

        if (snapshot.Roadmap is not { } roadmapType)
        {
            // Sin roadmap de MAPS, el plan hacia el objetivo del catalogo es el plan.
            return new ApplicationRoadmapPlan
            {
                EaiCode = snapshot.EaiCode,
                ApplicationName = snapshot.ApplicationName,
                Disposition = snapshot.Disposition,
                DispositionDefinition = snapshot.Disposition is { } missing ? catalog.RequireDisposition(missing) : null,
                Roadmap = null,
                RoadmapDefinition = null,
                Baseline = baseline,
                Projected = targetPlan?.Projection,
                Milestones = [],
                Alerts = alerts,
                StartDate = startDate,
                AutomaticTarget = targetPlan?.Target.Target,
                TargetPlan = targetPlan?.Target,
            };
        }

        var definition = catalog.Require(roadmapType);
        var projection = Project(snapshot, definition);

        return new ApplicationRoadmapPlan
        {
            EaiCode = snapshot.EaiCode,
            ApplicationName = snapshot.ApplicationName,
            Disposition = snapshot.Disposition,
            DispositionDefinition = snapshot.Disposition is { } assigned ? catalog.RequireDisposition(assigned) : null,
            Roadmap = roadmapType,
            RoadmapDefinition = definition,
            Baseline = baseline,
            Projected = projection,
            Milestones = Schedule(definition, startDate),
            Alerts = alerts,
            StartDate = startDate,
            TargetPlan = targetPlan?.Target,
        };
    }

    /// <summary>El plan hacia el objetivo y su proyección (sin palancas si ya lo alcanza).</summary>
    private sealed record TargetPlanCache(TargetResult Target, ScenarioResult Projection);

    /// <summary>
    /// Score al terminar el roadmap. Null para Retire y Consolidate: esas
    /// aplicaciones dejan el portafolio, asi que su score no mejora — deja de
    /// existir. Proyectar una mejora ahi seria justificar inversion en algo que se
    /// va a apagar.
    /// </summary>
    private ScenarioResult? Project(ApplicationSnapshot snapshot, RoadmapDefinition definition)
    {
        if (definition.RemovesFromPortfolio || definition.Levers.Count == 0)
        {
            return null;
        }

        var selections = definition.Levers
            .Select(key => new LeverSelection(key))
            .ToList();

        return whatIf.Evaluate(snapshot, selections);
    }

    /// <summary>Encadena los hitos desde la fecha de inicio, uno tras otro.</summary>
    private static List<ScheduledMilestone> Schedule(RoadmapDefinition definition, DateOnly startDate)
    {
        var milestones = new List<ScheduledMilestone>(definition.Milestones.Count);
        var cursor = startDate;

        for (var i = 0; i < definition.Milestones.Count; i++)
        {
            var milestone = definition.Milestones[i];
            var end = cursor.AddDays(milestone.Weeks * 7);

            milestones.Add(new ScheduledMilestone
            {
                Order = i + 1,
                Name = milestone.Name,
                Owner = milestone.Owner,
                Weeks = milestone.Weeks,
                Start = cursor,
                End = end,
            });

            cursor = end;
        }

        return milestones;
    }

    /// <summary>
    /// Contrasta el plan declarado contra los datos de la aplicacion. Cada alerta
    /// senala una contradiccion concreta, no una opinion sobre la estrategia.
    /// </summary>
    private IReadOnlyList<CoherenceAlert> CheckCoherence(ApplicationSnapshot snapshot, ScoreBreakdown baseline)
    {
        var alerts = new List<CoherenceAlert>();

        void Raise(string key, Notice evidence) => alerts.Add(new CoherenceAlert
        {
            Rule = catalog.RequireRule(key),
            Evidence = evidence,
        });

        // Dato incompleto o incoherente con el propio modelo MAPS.
        if (snapshot.Disposition is { } disposition)
        {
            if (snapshot.Roadmap is null)
            {
                Raise(
                    "roadmap.missing",
                    Notice.Of(NoticeCodes.AlertRoadmapMissing, ("disposition", MapsNames.Display(disposition))));
            }
            else if (!catalog.IsValidPair(disposition, snapshot.Roadmap.Value))
            {
                Raise(
                    "roadmap.mismatched",
                    Notice.Of(
                        NoticeCodes.AlertPairInvalid,
                        ("disposition", MapsNames.Display(disposition)),
                        ("roadmap", MapsNames.Display(snapshot.Roadmap.Value))));
            }
        }

        // La aplicacion sale del portafolio pero sus productos dicen lo contrario.
        if (snapshot.Disposition == ApplicationDisposition.Eliminate)
        {
            var invest = snapshot.ProductStrategy?.InvestCount ?? 0;
            if (invest > 0)
            {
                Raise(
                    "eliminate.withInvestProducts",
                    Notice.Of(NoticeCodes.AlertInvestProducts, ("count", invest)));
            }

            if (snapshot.UserCount >= catalog.Parameters.SignificantUserBase)
            {
                Raise(
                    "eliminate.withManyUsers",
                    Notice.Of(NoticeCodes.AlertManyUsers, ("users", snapshot.UserCount)));
            }
        }

        // Tolerar no es aceptar riesgo critico abierto.
        if (snapshot.Disposition == ApplicationDisposition.Tolerate
            && snapshot.ScaFlaws?.Critical > 0)
        {
            Raise(
                "tolerate.withOpenCriticalFlaws",
                Notice.Of(NoticeCodes.AlertCriticalFlaws, ("count", snapshot.ScaFlaws.Critical)));
        }

        // Rehost no altera la autenticacion.
        if (snapshot.Roadmap == RoadmapType.Rehost
            && snapshot.UtilizesMfa == false
            && snapshot.UserCount > 0)
        {
            Raise(
                "rehost.withoutMfa",
                Notice.Of(NoticeCodes.AlertUsersWithoutMfa, ("users", snapshot.UserCount)));
        }

        // Reconstruir desde cero algo que ya esta sano.
        if (snapshot.Roadmap == RoadmapType.Reimagine
            && baseline.EffectiveScore >= catalog.Parameters.HealthyScore)
        {
            Raise(
                "reimagine.onHealthyApplication",
                Notice.Of(
                    NoticeCodes.AlertHealthyScore,
                    ("score", baseline.EffectiveScore),
                    ("threshold", catalog.Parameters.HealthyScore)));
        }

        // Independiente del roadmap: una aplicacion critica sin DR es un riesgo.
        if (snapshot.CriticalApplicationIndicator == true
            && snapshot.DisasterRecoveryInScope == false)
        {
            Raise("critical.withoutDisasterRecovery", Notice.Of(NoticeCodes.AlertCriticalWithoutDr));
        }

        return [.. alerts.OrderByDescending(a => a.Rule.Severity).ThenBy(a => a.Rule.Key, StringComparer.Ordinal)];
    }
}
