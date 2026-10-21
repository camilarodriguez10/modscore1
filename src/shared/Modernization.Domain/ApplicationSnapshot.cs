namespace Modernization.Domain;

/// <summary>
/// Modelo canonico de una aplicacion en un momento dado.
/// Lo producen los tres adaptadores de fuente (Dummy, File, Database) y lo consume
/// el motor de scoring. Es el unico contrato entre la capa de datos y las reglas de
/// negocio: cambiar de fuente no puede cambiar esta forma.
/// </summary>
public sealed record ApplicationSnapshot
{
    /// <summary>Identificador unico de la aplicacion (EAI Code).</summary>
    public required string EaiCode { get; init; }

    public required string ApplicationName { get; init; }

    /// <summary>Momento en que se extrajeron los datos de las fuentes.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>
    /// Adaptador que produjo este snapshot: Dummy, File o Database.
    /// Viaja hasta la procedencia de cada metrica para que la pantalla de linaje
    /// pueda decir no solo de que sistema salio el dato, sino quien lo entrego.
    /// </summary>
    public string? Provider { get; init; }

    // ---------------------------------------------------------------------
    // General Application Details — no puntuan (BRD: "General Application
    // Details (Not scored)"), pero se muestran y se usan para filtrar.
    // ---------------------------------------------------------------------

    public string? ApplicationStatus { get; init; }
    public string? Cio { get; init; }
    public string? Cre { get; init; }
    public string? Lob { get; init; }
    public string? SubLob { get; init; }
    public string? Region { get; init; }
    public string? Country { get; init; }
    public bool? CriticalApplicationIndicator { get; init; }
    public bool? InternetFacing { get; init; }
    public string? CloudIndicator { get; init; }
    public string? CloudType { get; init; }
    public string? CloudVendor { get; init; }
    public string? CodingLanguage { get; init; }
    public string? RecoveryTimeObjectiveRange { get; init; }
    public string? RecoveryPointObjectiveRange { get; init; }
    public string? PrimaryBusinessCapability { get; init; }
    public int? L1BusinessCapabilityCount { get; init; }

    // ---------------------------------------------------------------------
    // Disposition y roadmap (MAPS). Hoy fuera del calculo (peso 0) pero
    // mapeados: alimentan la seccion de plan futuro.
    // ---------------------------------------------------------------------

    public ApplicationDisposition? Disposition { get; init; }
    public RoadmapType? Roadmap { get; init; }

    // ---------------------------------------------------------------------
    // Modernization Risk Vectors (50%)
    // ---------------------------------------------------------------------

    /// <summary>Kenna Score 0-1000. Fuente: CMT, App Sec Kenna Application Report.</summary>
    public int? KennaScore { get; init; }

    /// <summary>Fuente: EAI, Application Security and IT Risk.</summary>
    public bool? DisasterRecoveryInScope { get; init; }

    /// <summary>Flaws SCA abiertos por severidad. Fuente: Veracode SCA scan.</summary>
    public ScaFlawCounts? ScaFlaws { get; init; }

    /// <summary>
    /// Los hallazgos SCA abiertos más graves —hasta <see cref="ScaFinding.MaxPerSeverity"/>
    /// críticos y otros tantos altos, por CVSS—, para decir en el future plan qué librerías
    /// actualizar. No intervienen en el score: el score usa los conteos.
    /// </summary>
    public IReadOnlyList<ScaFinding> OpenScaFindings { get; init; } = [];

    /// <summary>Indica si la aplicacion utiliza MFA. Fuente: EAI.</summary>
    public bool? UtilizesMfa { get; init; }

    /// <summary>
    /// Cantidad de usuarios. Interviene en la regla de MFA: una aplicacion sin
    /// usuarios no se penaliza por no tener MFA.
    /// </summary>
    public int? UserCount { get; init; }

    /// <summary>Open-Source Score 0-100. Fuente: RPT (Risk Profile Tool).</summary>
    public decimal? OpenSourceScore { get; init; }

    // ---------------------------------------------------------------------
    // Architecture & Maturity (40%)
    // ---------------------------------------------------------------------

    /// <summary>RAW Tech Score 0-100. Fuente: CMT, Currency Model Detail Report.</summary>
    public decimal? RawTechScore { get; init; }

    /// <summary>Tipo de exposicion de APIs. Fuente: API management y DataPower.</summary>
    public ApiExposure? NativeApi { get; init; }

    /// <summary>Composicion arquitectonica de los productos. Fuente: CMT.</summary>
    public ArchitecturalValue? ArchitecturalValue { get; init; }

    /// <summary>Estrategia de producto por categoria. Fuente: MTS.</summary>
    public ProductStrategy? ProductStrategy { get; init; }

    /// <summary>
    /// Tech Debt Score 0-5 (COMBINED_APP_SCORE del archivo tech_debt).
    /// Menor es mejor: 0 = sin deuda.
    /// </summary>
    public decimal? TechDebtScore { get; init; }

    /// <summary>Automation DevSecOps Score 0-5. Removida del calculo (peso 0).</summary>
    public int? AutomationDevSecOpsScore { get; init; }

    /// <summary>Desplegada en contenedores. Removida del calculo (peso 0).</summary>
    public bool? Containerized { get; init; }

    // ---------------------------------------------------------------------
    // Stability (10%)
    // ---------------------------------------------------------------------

    /// <summary>Capacidades de monitoreo detectadas.</summary>
    public MonitoringCapabilities? Monitoring { get; init; }

    /// <summary>
    /// Operational Score 0-100. Fuente: CMT, derivado de tickets de incidente y
    /// problema de ServiceNow sobre una ventana de 12 meses.
    /// </summary>
    public decimal? OperationalScore { get; init; }
}

/// <summary>
/// Flaws SCA abiertos por severidad. Cada severidad es nullable de forma
/// independiente: la fuente puede reportar unas y no otras, y una severidad sin
/// dato se excluye del numerador y del denominador (BRD, "ALL Sections").
/// Un conteo de 0 significa "None" y puntua 100 — no es ausencia de dato.
/// </summary>
/// <summary>Un hallazgo SCA abierto: la librería, la vulnerabilidad y su gravedad.</summary>
public sealed record ScaFinding
{
    /// <summary>Cuántos hallazgos por severidad se guardan con la aplicación.</summary>
    public const int MaxPerSeverity = 10;

    /// <summary>"Critical" o "High".</summary>
    public required string Severity { get; init; }

    public string? CveId { get; init; }
    public string? VulnerabilityName { get; init; }
    public string? LibraryName { get; init; }
    public string? LibraryVendor { get; init; }
    public decimal? Cvssv3Score { get; init; }

    /// <summary>Días desde que se encontró, según la fuente.</summary>
    public int? AgeDays { get; init; }
}

public sealed record ScaFlawCounts
{
    public int? Critical { get; init; }
    public int? High { get; init; }
    public int? Medium { get; init; }
    public int? Low { get; init; }
}

/// <summary>
/// Composicion arquitectonica. CMT puede entregar el valor compuesto ya calculado
/// o solo los conteos por categoria; <see cref="ProvidedValue"/> tiene prioridad
/// cuando existe.
/// </summary>
public sealed record ArchitecturalValue
{
    /// <summary>Valor compuesto 0-100 entregado directamente por la fuente.</summary>
    public decimal? ProvidedValue { get; init; }

    public int? ModernCount { get; init; }
    public int? HeritageCount { get; init; }
    public int? ObsoleteCount { get; init; }
    public int? NotApprovedForUseCount { get; init; }

    public int TotalCount =>
        (ModernCount ?? 0) + (HeritageCount ?? 0) + (ObsoleteCount ?? 0) + (NotApprovedForUseCount ?? 0);
}

/// <summary>
/// Conteo de productos por estrategia. Mismo criterio que
/// <see cref="ArchitecturalValue"/>: el valor provisto gana sobre los conteos.
/// </summary>
public sealed record ProductStrategy
{
    /// <summary>Valor compuesto 0-100 entregado directamente por la fuente.</summary>
    public decimal? ProvidedValue { get; init; }

    public int? InvestCount { get; init; }
    public int? TolerateCount { get; init; }
    public int? EliminateCount { get; init; }
    public int? MigrateCount { get; init; }

    public int TotalCount =>
        (InvestCount ?? 0) + (TolerateCount ?? 0) + (EliminateCount ?? 0) + (MigrateCount ?? 0);
}

/// <summary>
/// Capacidades de monitoreo (archivo MONITOREO). Basta una para puntuar 100.
/// </summary>
public sealed record MonitoringCapabilities
{
    public bool? ElasticAgent { get; init; }
    public bool? ApplicationPerformanceMonitoring { get; init; }
    public bool? SyntheticMonitoring { get; init; }
    public bool? GeneralMonitoring { get; init; }

    /// <summary>True si alguna capacidad esta habilitada; null si ninguna se reporto.</summary>
    public bool? AnyEnabled
    {
        get
        {
            bool?[] all = [ElasticAgent, ApplicationPerformanceMonitoring, SyntheticMonitoring, GeneralMonitoring];
            if (Array.TrueForAll(all, static f => f is null))
            {
                return null;
            }

            return Array.Exists(all, static f => f == true);
        }
    }
}

/// <summary>Tipo de exposicion de APIs de la aplicacion.</summary>
public enum ApiExposure
{
    Apim,
    Apic,
    Soap,
    DataPowerSoapLibraries,
}

/// <summary>Disposition MAPS. Agrupa los roadmaps posibles.</summary>
public enum ApplicationDisposition
{
    Tolerate,
    Invest,
    Eliminate,
}

/// <summary>Roadmap MAPS.</summary>
public enum RoadmapType
{
    MaintainPatch,
    Rehost,
    MaintainEvolve,
    Refactor,
    Reimagine,
    Retire,
    Consolidate,
}

/// <summary>
/// Nombres MAPS tal como aparecen en el BRD. Son parte del modelo, no de la
/// presentacion: "Maintain (Patch)" es el nombre del roadmap, no una etiqueta
/// que cada capa pueda inventar por su cuenta.
/// </summary>
public static class MapsNames
{
    public static string Display(RoadmapType roadmap) => roadmap switch
    {
        RoadmapType.MaintainPatch => "Maintain (Patch)",
        RoadmapType.Rehost => "Rehost",
        RoadmapType.MaintainEvolve => "Maintain (Evolve)",
        RoadmapType.Refactor => "Refactor",
        RoadmapType.Reimagine => "Reimagine",
        RoadmapType.Retire => "Retire",
        RoadmapType.Consolidate => "Consolidate",
        _ => roadmap.ToString(),
    };

    public static string Display(ApplicationDisposition disposition) => disposition switch
    {
        ApplicationDisposition.Tolerate => "Tolerate",
        ApplicationDisposition.Invest => "Invest",
        ApplicationDisposition.Eliminate => "Eliminate",
        _ => disposition.ToString(),
    };
}
