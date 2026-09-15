using Modernization.Domain;
using Modernization.Domain.Sources;

namespace Modernization.Sources;

/// <summary>
/// Capa anticorrupcion. Toma los registros crudos de las ocho fuentes y produce
/// el modelo canonico. Es el unico lugar del sistema que conoce los formatos de
/// los archivos: "Yes"/"No", "1-Critical", TRUE/FALSE, APP_ID contra EAI_CODE.
/// </summary>
/// <remarks>
/// Los tres adaptadores (Dummy, File, Database) comparten este ensamblador. Por
/// eso migrar de fuente no puede cambiar un score: la normalizacion es la misma.
/// </remarks>
public static class SnapshotAssembler
{
    /// <summary>Insumos ya leidos, uno por fuente.</summary>
    public sealed record Input
    {
        public required string Provider { get; init; }
        public required DateTimeOffset CapturedAt { get; init; }
        public required SnapshotVersion Version { get; init; }
        public required SourceReadResult<EaiRecord> Eai { get; init; }
        public required SourceReadResult<ArchitecturalValueRecord> ArchitecturalValue { get; init; }
        public required SourceReadResult<ProductStrategyRecord> ProductStrategy { get; init; }
        public required SourceReadResult<TechDebtRecord> TechDebt { get; init; }
        public required SourceReadResult<ScaFlawRecord> ScaFlaws { get; init; }
        public required SourceReadResult<OssScoreRecord> OssScore { get; init; }
        public required SourceReadResult<MonitoringRecord> Monitoring { get; init; }
        public required SourceReadResult<ApimOnboardingRecord> ApimOnboarding { get; init; }
    }

    public static SnapshotSet Assemble(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var archValue = ByEai(input.ArchitecturalValue.Records, r => r.EaiCode);
        var productStrategy = ByEai(input.ProductStrategy.Records, r => r.EaiCode);
        var oss = ByEai(input.OssScore.Records, r => r.EaiCode);
        var monitoring = ByEai(input.Monitoring.Records, r => r.EaiCode);
        var apim = ByEai(input.ApimOnboarding.Records, r => r.EaiCode);
        var techDebtByAppId = ByEai(input.TechDebt.Records, r => r.AppId);
        var scaByEai = GroupOpenFlawsByEai(input.ScaFlaws.Records);

        var applications = new List<ApplicationSnapshot>(input.Eai.Records.Count);
        var matched = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var eai in input.Eai.Records)
        {
            var techDebt = eai.AppId is { Length: > 0 } appId && techDebtByAppId.TryGetValue(appId, out var td)
                ? td
                : null;

            applications.Add(Build(
                eai,
                input.Provider,
                input.CapturedAt,
                Lookup(archValue, eai.EaiCode, matched, SourceNames.ArchitecturalValue),
                Lookup(productStrategy, eai.EaiCode, matched, SourceNames.ProductStrategy),
                Count(techDebt, matched, SourceNames.TechDebt),
                Lookup(scaByEai, eai.EaiCode, matched, SourceNames.ScaFlaws),
                Lookup(oss, eai.EaiCode, matched, SourceNames.OssScore),
                Lookup(monitoring, eai.EaiCode, matched, SourceNames.Monitoring),
                Lookup(apim, eai.EaiCode, matched, SourceNames.ApimOnboarding)));

            Count(eai, matched, SourceNames.EaiMaster);
        }

        var knownEais = applications.Select(a => a.EaiCode).ToHashSet(StringComparer.Ordinal);

        return new SnapshotSet
        {
            Provider = input.Provider,
            Applications = applications,
            CapturedAt = input.CapturedAt,
            Version = input.Version,
            Sources =
            [
                Status(input.Eai, matched),
                WithOrphans(input.ArchitecturalValue, matched, knownEais, r => r.EaiCode),
                WithOrphans(input.ProductStrategy, matched, knownEais, r => r.EaiCode),
                Status(input.TechDebt, matched),
                WithOrphans(input.ScaFlaws, matched, knownEais, r => r.EaiCode),
                WithOrphans(input.OssScore, matched, knownEais, r => r.EaiCode),
                WithOrphans(input.Monitoring, matched, knownEais, r => r.EaiCode),
                WithOrphans(input.ApimOnboarding, matched, knownEais, r => r.EaiCode),
            ],
        };
    }

    // -----------------------------------------------------------------------

    private static ApplicationSnapshot Build(
        EaiRecord eai,
        string provider,
        DateTimeOffset capturedAt,
        ArchitecturalValueRecord? archValue,
        ProductStrategyRecord? productStrategy,
        TechDebtRecord? techDebt,
        ScaFlawCounts? sca,
        OssScoreRecord? oss,
        MonitoringRecord? monitoring,
        ApimOnboardingRecord? apim) => new()
        {
            EaiCode = eai.EaiCode,
            ApplicationName = eai.ApplicationName ?? $"EAI {eai.EaiCode}",
            CapturedAt = capturedAt,
            Provider = provider,

            ApplicationStatus = eai.ApplicationStatus,
            Cio = eai.Cio,
            Cre = eai.Cre,
            Lob = eai.Lob,
            SubLob = eai.SubLob,
            Region = eai.RegionName,
            Country = eai.CountryName,
            CriticalApplicationIndicator = ParseFlag(eai.CriticalApplicationIndicator),
            InternetFacing = ParseFlag(eai.InternetFacing),
            CloudIndicator = eai.CloudIndicator,
            CloudType = eai.CloudType,
            CloudVendor = eai.CloudVendor,
            CodingLanguage = eai.CodingLanguage,
            RecoveryPointObjectiveRange = eai.RecoveryPointObjectiveRange,
            RecoveryTimeObjectiveRange = eai.RecoveryTimeObjectiveRange,
            PrimaryBusinessCapability = eai.PrimaryBusinessCapability,
            L1BusinessCapabilityCount = eai.L1BusinessCapabilityCount,

            Disposition = ParseDisposition(eai.ApplicationDisposition),
            Roadmap = ParseRoadmap(eai.Roadmap),

            KennaScore = eai.KennaScore,
            DisasterRecoveryInScope = ParseFlag(eai.DrInScope),
            ScaFlaws = sca,
            UtilizesMfa = ParseFlag(eai.UtilizesMfa),
            UserCount = eai.UserCount,
            OpenSourceScore = oss?.OssScore,

            RawTechScore = eai.RawTechScore,
            NativeApi = ParseApiExposure(apim?.ApiType),
            ArchitecturalValue = archValue is null
                ? null
                : new ArchitecturalValue
                {
                    ModernCount = archValue.ModernCount,
                    HeritageCount = archValue.HeritageCount,
                    ObsoleteCount = archValue.ObsoleteCount,
                    NotApprovedForUseCount = archValue.NotApprovedForUseCount,
                },
            ProductStrategy = productStrategy is null
                ? null
                : new ProductStrategy
                {
                    InvestCount = productStrategy.InvestCount,
                    TolerateCount = productStrategy.TolerateCount,
                    EliminateCount = productStrategy.EliminateCount,
                    MigrateCount = productStrategy.MigrateCount,
                },
            TechDebtScore = techDebt?.CombinedAppScore,

            Monitoring = monitoring is null
                ? null
                : new MonitoringCapabilities
                {
                    ElasticAgent = monitoring.Agent,
                    ApplicationPerformanceMonitoring = monitoring.Apm,
                    SyntheticMonitoring = monitoring.Synthetics,
                    GeneralMonitoring = monitoring.Monitoring,
                },
            OperationalScore = eai.OperationalScore,
        };

    /// <summary>
    /// Agrupa los flaws abiertos por EAI y severidad.
    /// Una aplicacion presente en el archivo se considera escaneada: las severidades
    /// sin filas valen 0 ("None"), que puntua 100. Una aplicacion ausente del archivo
    /// no se considera escaneada y sus metricas SCA quedan sin dato.
    /// La distincion es deliberada: tratar "ausente" como "cero flaws" regalaria
    /// puntos a las aplicaciones que nunca se escanearon.
    /// </summary>
    private static Dictionary<string, ScaFlawCounts> GroupOpenFlawsByEai(IReadOnlyList<ScaFlawRecord> records)
    {
        var result = new Dictionary<string, ScaFlawCounts>(StringComparer.Ordinal);

        foreach (var group in records.GroupBy(r => r.EaiCode, StringComparer.Ordinal))
        {
            var open = group
                .Where(r => string.Equals(r.FindingStatus, "Open", StringComparison.OrdinalIgnoreCase))
                .ToList();

            result[group.Key] = new ScaFlawCounts
            {
                Critical = CountSeverity(open, "Critical"),
                High = CountSeverity(open, "High"),
                Medium = CountSeverity(open, "Medium"),
                Low = CountSeverity(open, "Low"),
            };
        }

        return result;
    }

    private static int CountSeverity(List<ScaFlawRecord> flaws, string severity)
        => flaws.Count(r => r.SeverityDescription.Contains(severity, StringComparison.OrdinalIgnoreCase));

    // -----------------------------------------------------------------------
    // Parseo de los formatos crudos de los archivos.
    // -----------------------------------------------------------------------

    /// <summary>Acepta las formas que aparecen en los archivos: Yes/No, Y/N, True/False, 1/0.</summary>
    internal static bool? ParseFlag(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "Y" or "YES" or "TRUE" or "1" or "SI" or "SÍ" => true,
        "N" or "NO" or "FALSE" or "0" => false,
        _ => null,
    };

    internal static ApplicationDisposition? ParseDisposition(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TOLERATE" => ApplicationDisposition.Tolerate,
        "INVEST" => ApplicationDisposition.Invest,
        "ELIMINATE" => ApplicationDisposition.Eliminate,
        _ => null,
    };

    internal static RoadmapType? ParseRoadmap(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized switch
        {
            "MAINTAIN(PATCH)" or "MAINTAINPATCH" or "PATCH" => RoadmapType.MaintainPatch,
            "MAINTAIN(EVOLVE)" or "MAINTAINEVOLVE" or "EVOLVE" => RoadmapType.MaintainEvolve,
            "REHOST" => RoadmapType.Rehost,
            "REFACTOR" => RoadmapType.Refactor,
            "REIMAGINE" or "REPLACE" => RoadmapType.Reimagine,
            "RETIRE" => RoadmapType.Retire,
            "CONSOLIDATE" => RoadmapType.Consolidate,
            _ => null,
        };
    }

    internal static ApiExposure? ParseApiExposure(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();

        return normalized switch
        {
            "APIM" => ApiExposure.Apim,
            "APIC" => ApiExposure.Apic,
            "SOAP" => ApiExposure.Soap,
            "DATAPOWER" or "DATAPOWER/SOAP LIBRARIES" => ApiExposure.DataPowerSoapLibraries,
            _ => null,
        };
    }

    // -----------------------------------------------------------------------

    private static Dictionary<string, TRecord> ByEai<TRecord>(
        IReadOnlyList<TRecord> records,
        Func<TRecord, string> keySelector)
    {
        var result = new Dictionary<string, TRecord>(records.Count, StringComparer.Ordinal);

        foreach (var record in records)
        {
            // La ultima fila gana; las duplicadas se reportan como huerfanas.
            result[keySelector(record)] = record;
        }

        return result;
    }

    private static TValue? Lookup<TValue>(
        Dictionary<string, TValue> index,
        string eaiCode,
        Dictionary<string, int> matched,
        string sourceName)
        where TValue : class
    {
        if (!index.TryGetValue(eaiCode, out var value))
        {
            return null;
        }

        matched[sourceName] = matched.GetValueOrDefault(sourceName) + 1;
        return value;
    }

    private static TValue? Count<TValue>(TValue? value, Dictionary<string, int> matched, string sourceName)
        where TValue : class
    {
        if (value is not null)
        {
            matched[sourceName] = matched.GetValueOrDefault(sourceName) + 1;
        }

        return value;
    }

    private static SourceStatus Status<TRecord>(
        SourceReadResult<TRecord> result,
        Dictionary<string, int> matched)
        => result.ToStatus(matched.GetValueOrDefault(result.SourceName));

    /// <summary>
    /// Agrega como rechazo cada fila cuyo EAI no existe en el maestro. Un archivo
    /// desalineado con el inventario es un problema real de datos y debe verse en
    /// la pantalla de linaje, no perderse en silencio.
    /// </summary>
    private static SourceStatus WithOrphans<TRecord>(
        SourceReadResult<TRecord> result,
        Dictionary<string, int> matched,
        HashSet<string> knownEais,
        Func<TRecord, string> keySelector)
    {
        var orphans = new List<SourceRejection>(result.Rejections);

        for (var i = 0; i < result.Records.Count; i++)
        {
            var key = keySelector(result.Records[i]);
            if (!knownEais.Contains(key))
            {
                orphans.Add(new SourceRejection(
                    i + 1,
                    Notice.Of(NoticeCodes.RowUnknownEai, ("key", key)),
                    key));
            }
        }

        return result.ToStatus(matched.GetValueOrDefault(result.SourceName)) with
        {
            Rejections = orphans,
            RejectedCount = orphans.Count,
        };
    }
}
