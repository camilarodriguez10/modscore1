using Modernization.Domain;
using Modernization.WhatIf;

namespace Modernization.Roadmap;

/// <summary>Plan futuro de una aplicacion: donde esta, a donde va y por que pasos.</summary>
public sealed record ApplicationRoadmapPlan
{
    public required string EaiCode { get; init; }
    public required string ApplicationName { get; init; }

    public required ApplicationDisposition? Disposition { get; init; }
    public required DispositionDefinition? DispositionDefinition { get; init; }
    public required RoadmapType? Roadmap { get; init; }
    public required RoadmapDefinition? RoadmapDefinition { get; init; }

    /// <summary>Score actual, con su desglose completo.</summary>
    public required ScoreBreakdown Baseline { get; init; }

    /// <summary>
    /// Proyeccion al terminar el roadmap. Null cuando la aplicacion sale del
    /// portafolio: su score no mejora, deja de existir.
    /// </summary>
    public required ScenarioResult? Projected { get; init; }

    public required IReadOnlyList<ScheduledMilestone> Milestones { get; init; }

    /// <summary>Contradicciones entre el plan declarado y los datos.</summary>
    public required IReadOnlyList<CoherenceAlert> Alerts { get; init; }

    public required DateOnly StartDate { get; init; }

    public DateOnly? CompletionDate => Milestones.Count == 0 ? null : Milestones[^1].End;

    public int TotalWeeks => Milestones.Sum(m => m.Weeks);

    /// <summary>Puntos que gana el score al ejecutar el roadmap. Cero si sale del portafolio.</summary>
    public decimal ProjectedDelta => Projected?.Delta ?? 0m;

    public bool LeavesPortfolio => RoadmapDefinition?.RemovesFromPortfolio ?? false;
}

/// <summary>Hito con sus fechas ya calculadas desde el inicio del plan.</summary>
public sealed record ScheduledMilestone
{
    public required int Order { get; init; }
    public required string Name { get; init; }
    public required string Owner { get; init; }
    public required int Weeks { get; init; }
    public required DateOnly Start { get; init; }
    public required DateOnly End { get; init; }
}

/// <summary>Una contradiccion concreta, con la evidencia que la sostiene.</summary>
public sealed record CoherenceAlert
{
    public required CoherenceRuleDefinition Rule { get; init; }

    /// <summary>
    /// El dato que dispara la alerta, para que no haya que buscarlo. Codigo y datos:
    /// la interfaz arma la frase (ADR 0017).
    /// </summary>
    public required Notice Evidence { get; init; }
}

/// <summary>
/// Por que dimension se agrupa el plan del portafolio.
/// </summary>
/// <remarks>
/// El agrupamiento no cambia el calculo: la proyeccion de cada aplicacion sale del
/// mismo planificador y del mismo motor de what-if. Cambia solo por que criterio se
/// promedian, que es exactamente lo que distingue "como esta Mexico" de "como esta
/// Refactor".
/// </remarks>
public enum RoadmapGrouping
{
    /// <summary>Todo junto: una sola fila con el portafolio filtrado.</summary>
    Portfolio,

    /// <summary>Una fila por aplicacion.</summary>
    Application,

    Country,
    Cio,
    Lob,
    Disposition,
    Roadmap,
}

/// <summary>Vista de portafolio: cuantas aplicaciones hay en cada camino y como estan.</summary>
public sealed record PortfolioRoadmapSummary
{
    public required int TotalApplications { get; init; }
    public required IReadOnlyList<DispositionSummary> Dispositions { get; init; }
    public required IReadOnlyList<RoadmapSummary> Roadmaps { get; init; }

    /// <summary>Dimension por la que se agrupo.</summary>
    public required RoadmapGrouping Grouping { get; init; }

    /// <summary>
    /// El portafolio partido por esa dimension, de mayor a menor. Con
    /// <see cref="RoadmapGrouping.Portfolio"/> es una sola fila.
    /// </summary>
    public required IReadOnlyList<RoadmapGroupSummary> Groups { get; init; }

    /// <summary>Aplicaciones sin disposition asignada: no tienen plan futuro.</summary>
    public required int WithoutDisposition { get; init; }

    /// <summary>Alertas de coherencia agregadas por regla, de mayor gravedad primero.</summary>
    public required IReadOnlyList<AlertSummary> Alerts { get; init; }

    /// <summary>Cuantas aplicaciones dejarian el portafolio al ejecutar sus roadmaps.</summary>
    public required int LeavingPortfolio { get; init; }
}

public sealed record DispositionSummary
{
    public required ApplicationDisposition Disposition { get; init; }
    public required string Name { get; init; }
    public required string Definition { get; init; }
    public required int Count { get; init; }
    public required decimal AverageScore { get; init; }
}

public sealed record RoadmapSummary
{
    public required RoadmapType Roadmap { get; init; }
    public required string Name { get; init; }
    public required ApplicationDisposition Disposition { get; init; }
    public required string Definition { get; init; }
    public required int Count { get; init; }
    public required decimal AverageScore { get; init; }

    /// <summary>Score promedio proyectado al terminar. Null si sale del portafolio.</summary>
    public required decimal? AverageProjectedScore { get; init; }

    public required int TotalWeeks { get; init; }
    public required bool RemovesFromPortfolio { get; init; }
}

/// <summary>
/// Un grupo del portafolio con su plan agregado: donde esta hoy, a donde llegaria
/// y cuanto de el se apaga en el camino.
/// </summary>
public sealed record RoadmapGroupSummary
{
    /// <summary>
    /// El valor por el que se agrupo, tal como se escribe en un filtro. Es lo que
    /// permite que hacer clic en un grupo filtre el resto de las pantallas.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Como se lee en pantalla. Coincide con la clave salvo en aplicaciones.</summary>
    public required string Name { get; init; }

    public required int Count { get; init; }
    public required decimal AverageScore { get; init; }

    /// <summary>Promedio al terminar, sobre las que proyectan. Null si ninguna lo hace.</summary>
    public required decimal? AverageProjectedScore { get; init; }

    /// <summary>Puntos que gana el grupo al ejecutar sus roadmaps.</summary>
    public required decimal? AverageDelta { get; init; }

    /// <summary>Cuantas dejan el portafolio: su score no mejora, dejan de existir.</summary>
    public required int LeavingPortfolio { get; init; }

    /// <summary>Cuantas no tienen plan que proyectar.</summary>
    public required int WithoutDisposition { get; init; }

    /// <summary>Duracion media del plan, en semanas.</summary>
    public required int AverageWeeks { get; init; }

    /// <summary>Cuantas alertas de coherencia acumulan sus aplicaciones.</summary>
    public required int Alerts { get; init; }

    /// <summary>De que roadmaps esta hecho el grupo, de mayor a menor.</summary>
    public required IReadOnlyList<RoadmapMixEntry> Mix { get; init; }
}

/// <summary>Cuantas aplicaciones de un grupo siguen un roadmap dado.</summary>
public sealed record RoadmapMixEntry(string Roadmap, string Name, int Count);

public sealed record AlertSummary
{
    public required string RuleKey { get; init; }
    public required CoherenceSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required int Count { get; init; }

    /// <summary>Unos pocos EAI de ejemplo, para poder ir a mirarlos.</summary>
    public required IReadOnlyList<string> SampleEaiCodes { get; init; }
}
