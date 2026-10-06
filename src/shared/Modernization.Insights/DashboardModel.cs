namespace Modernization.Insights;

/// <summary>
/// El dashboard ejecutivo: cinco indicadores, seis paneles y las filas de detalle que
/// los respaldan.
/// </summary>
/// <remarks>
/// Todo sale de lo que el motor ya calculo y de <see cref="DashboardRules"/>. Las
/// filas de <see cref="Applications"/> llevan los mismos campos para todos los
/// paneles: el detalle que se abre al hacer clic en una celda, una burbuja o una
/// barra es siempre la misma tabla, filtrada distinto. Los codigos de nivel —riesgo,
/// resiliencia, tramo— viajan como texto y la interfaz los nombra (ADR 0017).
/// </remarks>
public sealed record PortfolioDashboard
{
    /// <summary>Indicadores del encabezado: health, atRisk, candidates, currency y resilience.</summary>
    public required IReadOnlyList<DashboardKpi> Kpis { get; init; }

    /// <summary>Capacidad primaria contra el eje elegido; lo usan las preguntas 1 y 4.</summary>
    public required DashboardHeatmap Matrix { get; init; }

    public required DashboardHealth Health { get; init; }
    public required DashboardPrioritization Prioritization { get; init; }
    public required DashboardComplexity Complexity { get; init; }
    public required DashboardRisk Risk { get; init; }
    public required DashboardObsolescence Obsolescence { get; init; }
    public required DashboardResilience Resilience { get; init; }

    /// <summary>Una fila por aplicacion, con los campos del detalle.</summary>
    public required IReadOnlyList<DashboardApplication> Applications { get; init; }
}

/// <summary>Opciones de una consulta: el eje del mapa de calor y la base de usuarios significativa.</summary>
public sealed record DashboardOptions(string Axis = "country", int SignificantUserBase = 1000)
{
    /// <summary>
    /// Los ejes que admite el mapa de calor, contra el CIO. Hasta el 2026-10-05 las filas eran
    /// la capacidad primaria y el CIO era un eje; el negocio pidió invertirlo.
    /// </summary>
    public static readonly IReadOnlyList<string> Axes = ["region", "country", "capability", "disposition"];
}

/// <summary>
/// Un indicador del encabezado.
/// </summary>
/// <remarks>
/// <c>Value</c> es el numero grande: el score promedio en <c>health</c> y un
/// porcentaje en los demas. <c>Count</c> de <c>Base</c> es lo que ese numero mide,
/// para que el porcentaje siempre se pueda leer como «tantas de tantas».
/// <c>Level</c> es el tramo o nivel que le toca, cuando tiene uno.
/// </remarks>
public sealed record DashboardKpi(string Key, decimal? Value, int Count, int Base, decimal Share, string? Level);

/// <summary>Mapa de calor: filas de CIO, columnas del eje elegido, y sus totales.</summary>
/// <remarks>
/// Los totales promedian las aplicaciones de la fila, de la columna o de toda la grilla, no
/// las celdas: una celda de una aplicación no pesa lo mismo que una de cuarenta. <c>Total</c>
/// es null solo cuando no hay ninguna aplicación.
/// </remarks>
public sealed record DashboardHeatmap(
    string Axis,
    IReadOnlyList<string> Rows,
    IReadOnlyList<string> Columns,
    IReadOnlyList<DashboardHeatmapCell> Cells,
    IReadOnlyList<DashboardHeatmapTotal> RowTotals,
    IReadOnlyList<DashboardHeatmapTotal> ColumnTotals,
    DashboardHeatmapTotal? Total);

/// <summary>El total de una fila, de una columna o de la grilla: lo mismo que una celda.</summary>
public sealed record DashboardHeatmapTotal(
    string Key,
    int Applications,
    decimal AverageScore,
    string Band,
    decimal? AverageRisk,
    string? RiskLevel);

/// <summary>
/// Una celda del mapa de calor.
/// </summary>
/// <remarks>
/// Lleva a la vez la salud (score promedio y su tramo) y el riesgo (promedio y
/// nivel), porque las preguntas 1 y 4 comparten la grilla. <c>RiskMass</c> es la suma
/// del riesgo de sus aplicaciones: con eso se mide cuanto riesgo concentra.
/// </remarks>
public sealed record DashboardHeatmapCell(
    string Row,
    string Column,
    int Applications,
    decimal AverageScore,
    string Band,
    decimal? AverageRisk,
    string? RiskLevel,
    decimal RiskMass);

/// <summary>¿Qué tan sano está? Tramos y la parte que requiere intervención.</summary>
public sealed record DashboardHealth(
    int Applications,
    decimal AverageScore,
    decimal MedianScore,
    decimal DataCoverage,
    IReadOnlyList<BandShare> Bands,
    int RequiringIntervention,
    decimal InterventionShare);

/// <summary>
/// ¿Qué modernizar primero? La matriz criticidad × oportunidad y su top 10.
/// </summary>
/// <remarks>
/// Los mínimos del cuadrante de candidatos viajan con la matriz: la pantalla lo
/// resalta con la regla vigente en vez de copiarla.
/// </remarks>
public sealed record DashboardPrioritization(
    IReadOnlyList<DashboardMatrixPoint> Points,
    IReadOnlyList<DashboardMatrixPoint> Top,
    int Candidates,
    int OutsideMatrix,
    int MinCriticality,
    int MinOpportunity);

/// <summary>
/// Una aplicación en la matriz.
/// </summary>
/// <remarks>
/// <c>Criticality</c> y <c>Opportunity</c> van de 0 (baja) a 3 (muy alta). El tamaño
/// de la burbuja es <c>RiskScore</c> y su color, <c>RiskLevel</c>.
/// </remarks>
public sealed record DashboardMatrixPoint(
    string EaiCode,
    string ApplicationName,
    int Criticality,
    int Opportunity,
    decimal? RiskScore,
    string? RiskLevel,
    decimal Uplift,
    int Weeks,
    string? RoadmapName,
    bool Candidate);

/// <summary>¿Dónde reducir complejidad? Seis dimensiones y cuatro oportunidades.</summary>
public sealed record DashboardComplexity(
    IReadOnlyList<DashboardComplexityDimension> Dimensions,
    IReadOnlyList<DashboardComplexityOpportunity> Opportunities);

/// <summary>
/// Una dimensión de complejidad.
/// </summary>
/// <remarks>
/// <c>Status</c> dice qué tan firme es: <c>provisional</c> si se mide con una
/// definición por confirmar, <c>needs-data</c> si falta la fuente. En ese caso los
/// números son null: no se inventa una medición. <c>Share</c> es su parte de la
/// complejidad medida; <c>PortfolioShare</c>, su parte del portafolio.
/// </remarks>
public sealed record DashboardComplexityDimension(
    string Key,
    string Status,
    int? Applications,
    decimal? Share,
    decimal? PortfolioShare);

/// <summary>Una oportunidad de reducción de complejidad y cuántas piezas toca.</summary>
public sealed record DashboardComplexityOpportunity(string Key, int Count);

/// <summary>¿Dónde se concentra el riesgo? Top 5 y el índice de concentración.</summary>
public sealed record DashboardRisk(
    decimal? AverageRisk,
    IReadOnlyList<DashboardHotspot> Hotspots,
    decimal ConcentrationIndex,
    string ConcentrationLevel);

/// <summary>Una aplicación entre las de más riesgo.</summary>
public sealed record DashboardHotspot(
    string EaiCode,
    string ApplicationName,
    string? Capability,
    decimal RiskScore,
    string RiskLevel);

/// <summary>¿Qué causa la obsolescencia? Los conceptos que explican los puntos perdidos.</summary>
public sealed record DashboardObsolescence(
    IReadOnlyList<DashboardObsolescenceFactor> Factors,
    decimal CurrencyDrivenShare,
    decimal? AverageCurrency,
    int WithCurrency);

/// <summary>
/// Un concepto de obsolescencia.
/// </summary>
/// <remarks>
/// <c>Share</c> es su parte de los puntos que pierde el portafolio. Los que tienen
/// <c>Currency</c> son de ciclo de vida o vigencia tecnológica. EOS y EOL aparecen con
/// <c>Status</c> <c>needs-data</c> hasta que TLM sea una fuente.
/// </remarks>
public sealed record DashboardObsolescenceFactor(
    string Key,
    string Status,
    bool Currency,
    decimal LostPoints,
    decimal Share,
    int ApplicationsAffected);

/// <summary>¿Dónde faltan defensas? Niveles por dimensión y el total.</summary>
public sealed record DashboardResilience(
    IReadOnlyList<DashboardResilienceDimension> Dimensions,
    IReadOnlyList<DashboardLevelShare> Overall,
    int Exposed,
    decimal ExposedShare,
    int WithData);

/// <summary>Una dimensión de resiliencia, medida o a la espera de su fuente.</summary>
public sealed record DashboardResilienceDimension(
    string Key,
    string Status,
    int WithData,
    IReadOnlyList<DashboardLevelShare> Levels);

/// <summary>Cuántas aplicaciones caen en un nivel, y qué parte son.</summary>
public sealed record DashboardLevelShare(string Level, int Applications, decimal Share);

/// <summary>
/// Una fila del detalle. Son los mismos campos en todos los paneles.
/// </summary>
/// <remarks>
/// Lleva las claves por las que se la selecciona: su celda del mapa de calor
/// (capacidad y los valores de cada eje), su lugar en la matriz, su nivel de
/// resiliencia y las dimensiones de complejidad en las que cae.
/// </remarks>
public sealed record DashboardApplication(
    string EaiCode,
    string ApplicationName,
    string? Region,
    string? Country,
    string? Cio,
    string? Cre,
    string? Capability,
    string? Status,
    bool Critical,
    string? Disposition,
    string? Roadmap,
    decimal Score,
    string Band,
    decimal? RiskScore,
    string? RiskLevel,
    decimal? Currency,
    bool Legacy,
    int Criticality,
    int? Opportunity,
    bool Candidate,
    string? Resilience,
    string? Recovery,
    string? Operational,
    IReadOnlyList<string> Complexity);
