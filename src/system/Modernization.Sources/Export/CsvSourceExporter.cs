using System.Globalization;
using System.Text;
using Modernization.Sources.Dummy;

namespace Modernization.Sources.Export;

/// <summary>
/// Escribe el portafolio sintetico como CSV usando los nombres de columna reales
/// de cada archivo observado.
/// </summary>
/// <remarks>
/// Es lo que hace barata la Fase 07: el adaptador de archivos se desarrolla y se
/// prueba contra estos CSV antes de que llegue el primer archivo productivo, y el
/// contract test compara que leer los CSV produzca exactamente el mismo score que
/// leer el generador en memoria.
/// </remarks>
public static class CsvSourceExporter
{
    /// <summary>Archivo generado, con el nombre del export real que replica.</summary>
    public sealed record ExportedFile(string FileName, string SourceName, int RowCount);

    public static IReadOnlyList<ExportedFile> Export(DummyPortfolio portfolio, string directory)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        return
        [
            Write(directory, "eai_master.csv", SourceNames.EaiMaster, EaiCsv(portfolio), portfolio.Eai.Count),
            Write(directory, "architectural_value_by_eai.csv", SourceNames.ArchitecturalValue, ArchValueCsv(portfolio), portfolio.ArchitecturalValue.Count),
            Write(directory, "product_strategy.csv", SourceNames.ProductStrategy, ProductStrategyCsv(portfolio), portfolio.ProductStrategy.Count),
            Write(directory, "tech_debt.csv", SourceNames.TechDebt, TechDebtCsv(portfolio), portfolio.TechDebt.Count),
            Write(directory, "veracode_sca_open_flaws.csv", SourceNames.ScaFlaws, ScaCsv(portfolio), portfolio.ScaFlaws.Count),
            Write(directory, "oss_score_by_eai.csv", SourceNames.OssScore, OssCsv(portfolio), portfolio.OssScore.Count),
            Write(directory, "monitoreo.csv", SourceNames.Monitoring, MonitoringCsv(portfolio), portfolio.Monitoring.Count),
            Write(directory, "apim_onboarded_eais.csv", SourceNames.ApimOnboarding, ApimCsv(portfolio), portfolio.ApimOnboarding.Count),
        ];
    }

    private static ExportedFile Write(string directory, string fileName, string sourceName, string content, int rowCount)
    {
        // UTF-8 sin BOM: es lo que produce un export de Excel a CSV.
        File.WriteAllText(Path.Combine(directory, fileName), content, new UTF8Encoding(false));
        return new ExportedFile(fileName, sourceName, rowCount);
    }

    // ---------------------------------------------------------------- archivos

    private static string EaiCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder(
            "EAI_Code", "APP_ID", "Application_Name", "Application Status", "Application_Disposition",
            "Roadmap", "CIO", "CRE", "LOB", "SUB_LOB", "Region Name", "Country Name",
            "Critical Application Indicator", "Internet_Facing", "Cloud_Indicator", "Cloud_Type",
            "Cloud_Vendor", "Coding_Language", "DR_In_Scope", "Does this Application Utilize MFA?",
            "Primary_Business_Capability", "L1_Business_Capability_Count",
            "Recovery_Point_Objective_Range", "Recovery_Time_Objective_Range", "User Count",
            "Kenna_Score", "Raw_Tech_Score", "Operational_Score");

        foreach (var r in p.Eai)
        {
            csv.Row(
                r.EaiCode, r.AppId, r.ApplicationName, r.ApplicationStatus, r.ApplicationDisposition,
                r.Roadmap, r.Cio, r.Cre, r.Lob, r.SubLob, r.RegionName, r.CountryName,
                r.CriticalApplicationIndicator, r.InternetFacing, r.CloudIndicator, r.CloudType,
                r.CloudVendor, r.CodingLanguage, r.DrInScope, r.UtilizesMfa,
                r.PrimaryBusinessCapability, Num(r.L1BusinessCapabilityCount),
                r.RecoveryPointObjectiveRange, r.RecoveryTimeObjectiveRange, Num(r.UserCount),
                Num(r.KennaScore), Num(r.RawTechScore), Num(r.OperationalScore));
        }

        return csv.Build();
    }

    private static string ArchValueCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder(
            "EAI_CODE", "Modern_Count", "Heritage_Count", "Obsolete_Count", "Not_Approved_for_Use_Count");

        foreach (var r in p.ArchitecturalValue)
        {
            csv.Row(r.EaiCode, Num(r.ModernCount), Num(r.HeritageCount), Num(r.ObsoleteCount), Num(r.NotApprovedForUseCount));
        }

        return csv.Build();
    }

    private static string ProductStrategyCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder(
            "EAI_CODE", "Eliminate_Count", "Invest_Count", "Migrate_Count", "Tolerate_Count");

        foreach (var r in p.ProductStrategy)
        {
            csv.Row(r.EaiCode, Num(r.EliminateCount), Num(r.InvestCount), Num(r.MigrateCount), Num(r.TolerateCount));
        }

        return csv.Build();
    }

    private static string TechDebtCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder("APP_ID", "COMBINED_APP_SCORE");

        foreach (var r in p.TechDebt)
        {
            csv.Row(r.AppId, Num(r.CombinedAppScore));
        }

        return csv.Build();
    }

    private static string ScaCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder(
            "FLAW_IDX_NUM", "APP_NM", "EAI_CD", "APP_ID", "SVR_DSC", "FND_STT", "FRST_FND_DT",
            "LST_FND_DT", "RSLV_DT", "FND_AGE", "SCAN_TY", "ROPEN_F", "CVE_ID", "CVSSV3_SCR",
            "VUL_NM", "LIB_NM", "LIB_VND", "PUB_DT", "ACCSBLT");

        foreach (var r in p.ScaFlaws)
        {
            csv.Row(
                Num<long>(r.FlawIndexNumber), r.ApplicationName, r.EaiCode, r.AppId, r.SeverityDescription,
                r.FindingStatus, Date(r.FirstFoundDate), Date(r.LastFoundDate), Date(r.ResolvedDate),
                Num(r.FindingAge), r.ScanType, r.ReopenFlag, r.CveId, Num(r.Cvssv3Score),
                r.VulnerabilityName, r.LibraryName, r.LibraryVendor, Date(r.PublishedDate), r.Accessibility);
        }

        return csv.Build();
    }

    private static string OssCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder("EAI Code", "OSS Score", "OPEN_SRC_SFTWR_HLTH_SCOR_UPDT_TS");

        foreach (var r in p.OssScore)
        {
            csv.Row(r.EaiCode, Num(r.OssScore), Date(r.UpdatedAt));
        }

        return csv.Build();
    }

    private static string MonitoringCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder("EAI", "Agent", "APM", "Monitoring", "Synthetics");

        foreach (var r in p.Monitoring)
        {
            csv.Row(r.EaiCode, Flag(r.Agent), Flag(r.Apm), Flag(r.Monitoring), Flag(r.Synthetics));
        }

        return csv.Build();
    }

    private static string ApimCsv(DummyPortfolio p)
    {
        var csv = new CsvBuilder("EAI", "Country", "Application Short Name", "Application Name", "API Type");

        foreach (var r in p.ApimOnboarding)
        {
            csv.Row(r.EaiCode, r.Country, r.ApplicationShortName, r.ApplicationName, r.ApiType);
        }

        return csv.Build();
    }

    // ----------------------------------------------------------------- formato

    private static string? Num<T>(T? value) where T : struct, IFormattable
        => value?.ToString(null, CultureInfo.InvariantCulture);

    /// <summary>Formato del archivo MONITOREO observado.</summary>
    private static string? Flag(bool? value) => value switch
    {
        true => "TRUE",
        false => "FALSE",
        null => null,
    };

    private static string? Date(DateTimeOffset? value)
        => value?.UtcDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// Escritor CSV minimo conforme a RFC 4180. Se escribe a mano en vez de traer una
/// dependencia: el exportador solo necesita comillas, escape y salto de linea.
/// La lectura, que si es dificil de hacer bien, usara una libreria madura en la
/// Fase 07.
/// </summary>
internal sealed class CsvBuilder
{
    private readonly StringBuilder _builder = new();
    private readonly int _columnCount;

    internal CsvBuilder(params string[] headers)
    {
        _columnCount = headers.Length;
        AppendRow(headers);
    }

    internal void Row(params string?[] values)
    {
        if (values.Length != _columnCount)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"La fila tiene {values.Length} columnas y la cabecera {_columnCount}."));
        }

        AppendRow(values);
    }

    internal string Build() => _builder.ToString();

    private void AppendRow(string?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                _builder.Append(',');
            }

            _builder.Append(Escape(values[i]));
        }

        _builder.Append('\n');
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.AsSpan().IndexOfAny(",\"\n\r") >= 0
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1]);

        return needsQuoting
            ? string.Concat("\"", value.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : value;
    }
}
