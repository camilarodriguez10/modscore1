namespace Modernization.Sources;

/// <summary>
/// Registros con la forma de las fuentes, no del dominio. Los nombres de columna
/// y los tipos replican lo que hay en los archivos reales: texto crudo, "Yes"/"No",
/// severidades como "1-Critical". La traduccion al modelo canonico ocurre despues,
/// en <see cref="SnapshotAssembler"/>, que es la capa anticorrupcion.
/// </summary>
/// <remarks>
/// Que estos registros sean feos a proposito es lo que protege al dominio:
/// cuando lleguen los archivos productivos, la fealdad nueva se absorbe aqui.
/// </remarks>
internal static class SourceNames
{
    internal const string EaiMaster = "EAI Master";
    internal const string ArchitecturalValue = "Architectural Value";
    internal const string ProductStrategy = "Product Strategy";
    internal const string TechDebt = "Tech Debt";
    internal const string ScaFlaws = "SCA Flaws";
    internal const string OssScore = "OSS Score";
    internal const string Monitoring = "Monitoring";
    internal const string ApimOnboarding = "APIM Onboarding";
}

/// <summary>Fila del export EAI. Columnas segun la EAI TABLE observada.</summary>
public sealed record EaiRecord
{
    public required string EaiCode { get; init; }

    /// <summary>Identificador interno usado por tech_debt y por los scans SCA.</summary>
    public string? AppId { get; init; }

    public string? ApplicationName { get; init; }
    public string? ApplicationStatus { get; init; }
    public string? ApplicationDisposition { get; init; }
    public string? Roadmap { get; init; }
    public string? Cio { get; init; }
    public string? Cre { get; init; }
    public string? Lob { get; init; }
    public string? SubLob { get; init; }
    public string? RegionName { get; init; }
    public string? CountryName { get; init; }

    /// <summary>"Y" / "N" en el archivo.</summary>
    public string? CriticalApplicationIndicator { get; init; }

    /// <summary>"Y" / "N" en el archivo.</summary>
    public string? InternetFacing { get; init; }

    public string? CloudIndicator { get; init; }
    public string? CloudType { get; init; }
    public string? CloudVendor { get; init; }
    public string? CodingLanguage { get; init; }

    /// <summary>"Yes" / "No" en el archivo.</summary>
    public string? DrInScope { get; init; }

    /// <summary>Columna "Does this Application Utilize MFA?": "Yes" / "No".</summary>
    public string? UtilizesMfa { get; init; }

    public string? PrimaryBusinessCapability { get; init; }
    public int? L1BusinessCapabilityCount { get; init; }
    public string? RecoveryPointObjectiveRange { get; init; }
    public string? RecoveryTimeObjectiveRange { get; init; }
    public int? UserCount { get; init; }

    // El export EAI observado trae estas tres columnas aunque el BRD las atribuye
    // a CMT. Si mas adelante CMT las entrega por separado, se agrega un reader
    // propio sin tocar el ensamblador.
    public int? KennaScore { get; init; }
    public decimal? RawTechScore { get; init; }
    public decimal? OperationalScore { get; init; }
}

/// <summary>Fila del export de composicion arquitectonica de CMT.</summary>
public sealed record ArchitecturalValueRecord
{
    public required string EaiCode { get; init; }
    public int? ModernCount { get; init; }
    public int? HeritageCount { get; init; }
    public int? ObsoleteCount { get; init; }
    public int? NotApprovedForUseCount { get; init; }
}

/// <summary>Fila del archivo PRODUCT STRATEGY.</summary>
public sealed record ProductStrategyRecord
{
    public required string EaiCode { get; init; }
    public int? EliminateCount { get; init; }
    public int? InvestCount { get; init; }
    public int? MigrateCount { get; init; }
    public int? TolerateCount { get; init; }
}

/// <summary>
/// Fila del archivo tech_debt. Se identifica por APP_ID, no por EAI_CODE:
/// el ensamblador resuelve la correspondencia.
/// </summary>
public sealed record TechDebtRecord
{
    public required string AppId { get; init; }
    public decimal? CombinedDenialScore { get; init; }
    public decimal? CombinedAcceptedScore { get; init; }
    public decimal? CombinedAppScore { get; init; }
    public DateTimeOffset? LastUpdatedDate { get; init; }
}

/// <summary>
/// Fila del archivo veracode_sca_open_flaws. Hay muchas por aplicacion: una por
/// vulnerabilidad abierta.
/// </summary>
public sealed record ScaFlawRecord
{
    public required long FlawIndexNumber { get; init; }
    public required string EaiCode { get; init; }
    public string? ApplicationName { get; init; }
    public string? AppId { get; init; }

    /// <summary>"1-Critical", "2-High", "3-Medium", "4-Low".</summary>
    public required string SeverityDescription { get; init; }

    /// <summary>"Open" / "Closed". Solo cuentan las abiertas.</summary>
    public required string FindingStatus { get; init; }

    public DateTimeOffset? FirstFoundDate { get; init; }
    public DateTimeOffset? LastFoundDate { get; init; }
    public DateTimeOffset? ResolvedDate { get; init; }
    public int? FindingAge { get; init; }
    public string? ScanType { get; init; }
    public string? ReopenFlag { get; init; }
    public string? CveId { get; init; }
    public decimal? Cvssv3Score { get; init; }
    public string? VulnerabilityName { get; init; }
    public string? LibraryName { get; init; }
    public string? LibraryVendor { get; init; }
    public DateTimeOffset? PublishedDate { get; init; }

    /// <summary>"Internal" / "External".</summary>
    public string? Accessibility { get; init; }
}

/// <summary>Fila del archivo OSS_Score_by_EAI.</summary>
public sealed record OssScoreRecord
{
    public required string EaiCode { get; init; }
    public decimal? OssScore { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>Fila del archivo MONITOREO. Booleanos como TRUE/FALSE.</summary>
public sealed record MonitoringRecord
{
    public required string EaiCode { get; init; }
    public bool? Agent { get; init; }
    public bool? Apm { get; init; }
    public bool? Monitoring { get; init; }
    public bool? Synthetics { get; init; }
}

/// <summary>Fila del archivo APIM_Onboarded_EAIs. Su presencia implica APIM.</summary>
public sealed record ApimOnboardingRecord
{
    public required string EaiCode { get; init; }
    public string? Country { get; init; }
    public string? ApplicationShortName { get; init; }
    public string? ApplicationName { get; init; }

    /// <summary>"APIM", "APIC", "SOAP" o "DataPower/SOAP Libraries".</summary>
    public string? ApiType { get; init; }
}
