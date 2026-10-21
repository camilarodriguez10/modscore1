using System.Globalization;

namespace Modernization.Domain;

/// <summary>
/// Un mensaje para personas expresado como codigo estable y sus datos.
/// </summary>
/// <remarks>
/// Desde la Fase 11 el backend no redacta frases para quien usa el portal: devuelve
/// <c>{ code, args }</c> y la interfaz arma la frase en su idioma (ADR 0017). Asi el
/// idioma no queda repartido entre dos capas, las pruebas comparan codigos en vez de
/// frases, y un segundo idioma es un catalogo mas en la interfaz.
/// <para>
/// Los argumentos van como texto invariante —<c>79.67</c>, no <c>79,67</c>—: darles
/// formato de numero, fecha o miles es trabajo de quien conoce el idioma de la pantalla.
/// </para>
/// </remarks>
public sealed record Notice
{
    private static readonly IReadOnlyDictionary<string, string> NoArgs =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public Notice(string code, IReadOnlyDictionary<string, string>? args = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
        Args = args ?? NoArgs;
    }

    /// <summary>Codigo estable, en minusculas con puntos: <c>csv.missingColumns</c>.</summary>
    public string Code { get; }

    /// <summary>Datos que acompanan al codigo, siempre como texto invariante.</summary>
    public IReadOnlyDictionary<string, string> Args { get; }

    public static Notice Of(string code) => new(code);

    public static Notice Of(string code, params (string Name, object? Value)[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var values = new Dictionary<string, string>(args.Length, StringComparer.Ordinal);

        foreach (var (name, value) in args)
        {
            values[name] = Format(value);
        }

        return new Notice(code, values);
    }

    /// <summary>El mismo aviso con un dato mas, p. ej. el archivo al que se refiere.</summary>
    public Notice With(string name, object? value)
    {
        var values = new Dictionary<string, string>(Args, StringComparer.Ordinal)
        {
            [name] = Format(value),
        };

        return new Notice(Code, values);
    }

    /// <summary>Para logs y mensajes de prueba: nunca se muestra a una persona.</summary>
    public override string ToString()
        => Args.Count == 0
            ? Code
            : Code + " {" + string.Join(", ", Args.Select(a => a.Key + "=" + a.Value)) + "}";

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        decimal number => number.ToString("0.####", CultureInfo.InvariantCulture),
        double number => number.ToString("0.####", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>
/// Catalogo de codigos. Cada uno tiene su frase en la interfaz: una prueba recorre
/// estas constantes y falla si alguna no esta en el catalogo de la SPA.
/// </summary>
public static class NoticeCodes
{
    // ---------------------------------------------------- valor crudo de metrica
    public const string ValuePlain = "value.plain";
    public const string ValueNoData = "value.noData";
    public const string ValueScaFlaws = "value.scaFlaws";
    public const string ValueMfa = "value.mfa";
    public const string ValueMonitoring = "value.monitoring";
    public const string ValueMonitoringNone = "value.monitoringNone";
    public const string ValueCompositeProvided = "value.compositeProvided";
    public const string ValueCompositeCounts = "value.compositeCounts";

    // --------------------------------------------------- exclusion de metrica
    public const string ExclusionNotReported = "exclusion.notReported";
    public const string ExclusionScaSeverity = "exclusion.scaSeverityNotReported";
    public const string ExclusionContainerizedNo = "exclusion.containerizedNo";
    public const string ExclusionMonitoring = "exclusion.monitoringNotReported";
    public const string ExclusionCompositeEmpty = "exclusion.compositeNoProducts";
    public const string ExclusionZeroValue = "exclusion.zeroValue";
    public const string ExclusionNotCritical = "exclusion.notCritical";

    // ------------------------------------------------------------- what-if
    public const string LeverCompositeProvided = "lever.compositeProvided";
    public const string LeverNotCritical = "lever.notCritical";
    public const string ScenarioWeightAdded = "scenario.weightAdded";
    public const string ScenarioInteraction = "scenario.interaction";
    public const string TargetAlreadyMet = "target.alreadyMet";
    public const string TargetUnreachable = "target.unreachable";
    public const string TargetReachable = "target.reachable";

    // ------------------------------------------------------------- roadmap
    public const string AlertRoadmapMissing = "alert.roadmapMissing";
    public const string AlertPairInvalid = "alert.pairInvalid";
    public const string AlertInvestProducts = "alert.investProducts";
    public const string AlertManyUsers = "alert.manyUsers";
    public const string AlertCriticalFlaws = "alert.criticalFlaws";
    public const string AlertUsersWithoutMfa = "alert.usersWithoutMfa";
    public const string AlertHealthyScore = "alert.healthyScore";
    public const string AlertCriticalWithoutDr = "alert.criticalWithoutDr";
    public const string OutlookNoRoadmap = "outlook.noRoadmap";
    public const string OutlookLeavesPortfolio = "outlook.leavesPortfolio";
    public const string OutlookNoScoreEffect = "outlook.noScoreEffect";
    public const string OutlookProjected = "outlook.projected";
    public const string OutlookAutomaticPlan = "outlook.automaticPlan";
    public const string OutlookAutomaticPlanShort = "outlook.automaticPlanShort";
    public const string OutlookAutomaticPlanMet = "outlook.automaticPlanMet";

    // Acciones del future plan, paso por paso (2026-10-21). La interfaz redacta la frase.
    public const string PlanMfa = "plan.mfa";
    public const string PlanDisasterRecovery = "plan.dr";
    public const string PlanScaRemediate = "plan.sca.remediate";
    public const string PlanScaFinding = "plan.sca.finding";
    public const string PlanScaMore = "plan.sca.more";
    public const string PlanKenna = "plan.kenna";
    public const string PlanOss = "plan.oss";
    public const string PlanRawTech = "plan.rawTech";
    public const string PlanRawTechComponents = "plan.rawTech.components";
    public const string PlanApi = "plan.api";
    public const string PlanTechDebt = "plan.techDebt";
    public const string PlanArchValue = "plan.archValue";
    public const string PlanProductStrategy = "plan.productStrategy";
    public const string PlanMonitoring = "plan.monitoring";
    public const string PlanOperational = "plan.operational";
    public const string PlanGeneric = "plan.generic";

    // ------------------------------------------------------------- formula
    public const string FormulaNumerator = "formula.numerator";
    public const string FormulaDenominator = "formula.denominator";
    public const string FormulaExcluded = "formula.excluded";
    public const string FormulaQuotient = "formula.quotient";
    public const string FormulaNormalization = "formula.normalization";

    // --------------------------------------------------------------- carga
    public const string UploadNoFiles = "upload.noFiles";
    public const string UploadTooLarge = "upload.tooLarge";
    public const string UploadBusy = "upload.busy";
    public const string UploadMissingMaster = "upload.missingMaster";
    public const string UploadEmpty = "upload.empty";
    public const string UploadTooManyFiles = "upload.tooManyFiles";
    public const string UploadAssignmentMisaligned = "upload.assignmentMisaligned";
    public const string UploadNotSupported = "upload.notSupported";
    public const string UploadLabelInvalid = "upload.labelInvalid";
    public const string UploadDisabledAdapter = "upload.disabledAdapter";
    public const string UploadDisabledPermission = "upload.disabledPermission";
    public const string UploadPublished = "upload.published";
    public const string UploadRejected = "upload.rejected";
    public const string UploadRejectedUnidentified = "upload.rejectedUnidentified";

    public const string FileTooLarge = "file.tooLarge";
    public const string FileIdentical = "file.identical";
    public const string FileNoName = "file.noName";
    public const string FileExtension = "file.extension";
    public const string FileContentType = "file.contentType";
    public const string FileAssignmentShape = "file.assignmentShape";

    public const string MatchAssignmentUnknown = "match.assignmentUnknown";
    public const string MatchAmbiguous = "match.ambiguous";
    public const string MatchUnrecognized = "match.unrecognized";

    public const string CsvNoHeader = "csv.noHeader";
    public const string CsvFieldTooLong = "csv.fieldTooLong";
    public const string CsvUnreadable = "csv.unreadable";
    public const string CsvTooManyRows = "csv.tooManyRows";
    public const string CsvMissingColumns = "csv.missingColumns";

    // ------------------------------------------- libros de Excel (.xlsx)
    public const string XlsxUnreadable = "xlsx.unreadable";
    public const string XlsxProtected = "xlsx.protected";
    public const string XlsxNoSheet = "xlsx.noSheet";
    public const string XlsxTooLarge = "xlsx.tooLarge";
    public const string XlsxTooManyColumns = "xlsx.tooManyColumns";
    public const string ContractUnmapped = "contract.unmapped";

    public const string RowMissingKey = "row.missingKey";
    public const string RowInvalidValue = "row.invalidValue";
    public const string RowNotConvertible = "row.notConvertible";
    public const string RowUnknownEai = "row.unknownEai";
    public const string RowDuplicateKey = "row.duplicateKey";
    public const string RowDuplicateKeyInFile = "row.duplicateKeyInFile";

    // ------------------------------------------- mapping de columnas antes de cargar
    public const string MappingInvalid = "mapping.invalid";
    public const string MappingNeedsSource = "mapping.needsSource";
    public const string MappingUnknownColumn = "mapping.unknownColumn";
    public const string MappingHeaderNotInFile = "mapping.headerNotInFile";
    public const string MappingHeaderTwice = "mapping.headerTwice";
    public const string MappingRequiredExcluded = "mapping.requiredExcluded";
    public const string MappingHeaderMultiline = "mapping.headerMultiline";
    public const string MappingHeaderTooLarge = "mapping.headerTooLarge";

    // ------------------------------------------- administracion de reglas (Fase 12)
    public const string RulesMetricUnknown = "rules.metricUnknown";
    public const string RulesDuplicate = "rules.duplicate";
    public const string RulesKindMismatch = "rules.kindMismatch";
    public const string RulesWeightInvalid = "rules.weightInvalid";
    public const string RulesWeightTotal = "rules.weightTotal";
    public const string RulesScoreInvalid = "rules.scoreInvalid";
    public const string RulesBandCount = "rules.bandCount";
    public const string RulesBandBounds = "rules.bandBounds";
    public const string RulesBandOrder = "rules.bandOrder";
    public const string RulesBandLabel = "rules.bandLabel";
    public const string RulesCaseKeys = "rules.caseKeys";
    public const string RulesCategoryKeys = "rules.categoryKeys";
    public const string RulesLeverUnknown = "rules.leverUnknown";
    public const string RulesLeverParameter = "rules.leverParameter";
    public const string RulesRoadmapUnknown = "rules.roadmapUnknown";
    public const string RulesRoadmapLever = "rules.roadmapLever";
    public const string RulesRoadmapLeaves = "rules.roadmapLeaves";
    public const string RulesMilestones = "rules.milestones";
    public const string RulesHealthyScore = "rules.healthyScore";
    public const string RulesUserBase = "rules.userBase";
    public const string RulesTooLarge = "rules.tooLarge";
    public const string RulesReasonInvalid = "rules.reasonInvalid";
    public const string RulesNoChanges = "rules.noChanges";
    public const string RulesStale = "rules.stale";
    public const string RulesBusy = "rules.busy";
    public const string RulesInvalid = "rules.invalid";
    public const string RulesStoreUnavailable = "rules.storeUnavailable";

    // Definiciones de las seis preguntas (ADR 0025). "parameter", "area" y "rule" son
    // claves del catalogo; la interfaz las nombra.
    public const string RulesAnalyticsUnknown = "rules.analyticsUnknown";
    public const string RulesAnalyticsValue = "rules.analyticsValue";
    public const string RulesAnalyticsInconsistent = "rules.analyticsInconsistent";
    public const string RulesAnalyticsArea = "rules.analyticsArea";
    public const string RulesAnalyticsStatus = "rules.analyticsStatus";
    public const string RulesAnalyticsOwner = "rules.analyticsOwner";

    public const string ChangeWeight = "change.weight";
    public const string ChangeBands = "change.bands";
    public const string ChangeCase = "change.case";
    public const string ChangeCategory = "change.category";
    public const string ChangeLeverEnabled = "change.leverEnabled";
    public const string ChangeLeverParameter = "change.leverParameter";
    public const string ChangeRoadmapLevers = "change.roadmapLevers";
    public const string ChangeMilestoneWeeks = "change.milestoneWeeks";
    public const string ChangeHealthyScore = "change.healthyScore";
    public const string ChangeUserBase = "change.userBase";
    public const string ChangeAnalytics = "change.analytics";
    public const string ChangeAnalyticsStatus = "change.analyticsStatus";
    public const string ChangeAnalyticsOwner = "change.analyticsOwner";
}
