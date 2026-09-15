namespace Modernization.Sources.Files;

/// <summary>
/// Convierte una fila leida del CSV al registro con forma de fuente. Es la ultima
/// pieza especifica de archivos: de aqui en adelante el ensamblador trata los
/// registros exactamente igual que los del adaptador Dummy.
/// </summary>
internal static class FileRecordMappers
{
    internal static EaiRecord? ToEai(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI_Code");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new EaiRecord
            {
                EaiCode = eaiCode,
                AppId = row.Text("APP_ID"),
                ApplicationName = row.Text("Application_Name"),
                ApplicationStatus = row.Text("Application Status"),
                ApplicationDisposition = row.Text("Application_Disposition"),
                Roadmap = row.Text("Roadmap"),
                Cio = row.Text("CIO"),
                Cre = row.Text("CRE"),
                Lob = row.Text("LOB"),
                SubLob = row.Text("SUB_LOB"),
                RegionName = row.Text("Region Name"),
                CountryName = row.Text("Country Name"),
                CriticalApplicationIndicator = row.Text("Critical Application Indicator"),
                InternetFacing = row.Text("Internet_Facing"),
                CloudIndicator = row.Text("Cloud_Indicator"),
                CloudType = row.Text("Cloud_Type"),
                CloudVendor = row.Text("Cloud_Vendor"),
                CodingLanguage = row.Text("Coding_Language"),
                DrInScope = row.Text("DR_In_Scope"),
                UtilizesMfa = row.Text("Does this Application Utilize MFA?"),
                PrimaryBusinessCapability = row.Text("Primary_Business_Capability"),
                L1BusinessCapabilityCount = row.Int("L1_Business_Capability_Count"),
                RecoveryPointObjectiveRange = row.Text("Recovery_Point_Objective_Range"),
                RecoveryTimeObjectiveRange = row.Text("Recovery_Time_Objective_Range"),
                UserCount = row.Int("User Count"),
                KennaScore = row.Int("Kenna_Score"),
                RawTechScore = row.Decimal("Raw_Tech_Score"),
                OperationalScore = row.Decimal("Operational_Score"),
            };
    }

    internal static ArchitecturalValueRecord? ToArchitecturalValue(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI_CODE");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new ArchitecturalValueRecord
            {
                EaiCode = eaiCode,
                ModernCount = row.Int("Modern_Count"),
                HeritageCount = row.Int("Heritage_Count"),
                ObsoleteCount = row.Int("Obsolete_Count"),
                NotApprovedForUseCount = row.Int("Not_Approved_for_Use_Count"),
            };
    }

    internal static ProductStrategyRecord? ToProductStrategy(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI_CODE");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new ProductStrategyRecord
            {
                EaiCode = eaiCode,
                EliminateCount = row.Int("Eliminate_Count"),
                InvestCount = row.Int("Invest_Count"),
                MigrateCount = row.Int("Migrate_Count"),
                TolerateCount = row.Int("Tolerate_Count"),
            };
    }

    internal static TechDebtRecord? ToTechDebt(CsvSourceReader.Row row)
    {
        var appId = row.Text("APP_ID");

        return string.IsNullOrWhiteSpace(appId)
            ? null
            : new TechDebtRecord
            {
                AppId = appId,
                CombinedDenialScore = row.Decimal("COMBINED_DENIAL_SCORE"),
                CombinedAcceptedScore = row.Decimal("COMBINED_ACCEPTED_SCORE"),
                CombinedAppScore = row.Decimal("COMBINED_APP_SCORE"),
                LastUpdatedDate = row.Date("LAST_UPDATED_DATE"),
            };
    }

    internal static ScaFlawRecord? ToScaFlaw(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI_CD");
        var severity = row.Text("SVR_DSC");
        var status = row.Text("FND_STT");
        var index = row.Long("FLAW_IDX_NUM");

        if (string.IsNullOrWhiteSpace(eaiCode)
            || string.IsNullOrWhiteSpace(severity)
            || string.IsNullOrWhiteSpace(status)
            || index is null)
        {
            return null;
        }

        return new ScaFlawRecord
        {
            FlawIndexNumber = index.Value,
            EaiCode = eaiCode,
            ApplicationName = row.Text("APP_NM"),
            AppId = row.Text("APP_ID"),
            SeverityDescription = severity,
            FindingStatus = status,
            FirstFoundDate = row.Date("FRST_FND_DT"),
            LastFoundDate = row.Date("LST_FND_DT"),
            ResolvedDate = row.Date("RSLV_DT"),
            FindingAge = row.Int("FND_AGE"),
            ScanType = row.Text("SCAN_TY"),
            ReopenFlag = row.Text("ROPEN_F"),
            CveId = row.Text("CVE_ID"),
            Cvssv3Score = row.Decimal("CVSSV3_SCR"),
            VulnerabilityName = row.Text("VUL_NM"),
            LibraryName = row.Text("LIB_NM"),
            LibraryVendor = row.Text("LIB_VND"),
            PublishedDate = row.Date("PUB_DT"),
            Accessibility = row.Text("ACCSBLT"),
        };
    }

    internal static OssScoreRecord? ToOssScore(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI Code");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new OssScoreRecord
            {
                EaiCode = eaiCode,
                OssScore = row.Decimal("OSS Score"),
                UpdatedAt = row.Date("OPEN_SRC_SFTWR_HLTH_SCOR_UPDT_TS"),
            };
    }

    internal static MonitoringRecord? ToMonitoring(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new MonitoringRecord
            {
                EaiCode = eaiCode,
                Agent = row.Bool("Agent"),
                Apm = row.Bool("APM"),
                Monitoring = row.Bool("Monitoring"),
                Synthetics = row.Bool("Synthetics"),
            };
    }

    internal static ApimOnboardingRecord? ToApimOnboarding(CsvSourceReader.Row row)
    {
        var eaiCode = row.Text("EAI");

        return string.IsNullOrWhiteSpace(eaiCode)
            ? null
            : new ApimOnboardingRecord
            {
                EaiCode = eaiCode,
                Country = row.Text("Country"),
                ApplicationShortName = row.Text("Application Short Name"),
                ApplicationName = row.Text("Application Name"),
                // Sin columna de tipo, estar en el archivo implica APIM: es lo que
                // significa el export "APIM_Onboarded_EAIs".
                ApiType = row.Text("API Type") ?? "APIM",
            };
    }
}
