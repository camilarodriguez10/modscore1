using System.Globalization;
using Modernization.Domain;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Por qué se rechazaron filas y qué valores quedaron sin dato, agrupado por problema. Es
/// lo que permite corregir un archivo confidencial sin mandárselo a nadie.
/// </summary>
public sealed class RejectionReasonTests
{
    private const string Header = "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count";

    private static SourceSchema ProductStrategy => SourceSchemaCatalog.Default.Require("product_strategy.csv");

    private static CsvSourceReader.ReadOutcome Read(params string[] rows)
    {
        using var reader = new StringReader(Header + "\n" + string.Join('\n', rows) + "\n");
        return new CsvSourceReader(ProductStrategy).Read(reader);
    }

    [Fact]
    public void EveryUnreadableColumnOfARowIsCountedAndTheRowStillLoads()
    {
        var outcome = Read(
            "1001,x,0,0,1",
            "1002,0,0,0,1",
            "1003,y,z,0,1");

        Assert.Equal(3, outcome.Rows.Count);
        Assert.Empty(outcome.Rejections);

        var eliminate = outcome.IgnoredValues.Single(r => r.Reason.Args["column"] == "Eliminate_Count");
        var invest = outcome.IgnoredValues.Single(r => r.Reason.Args["column"] == "Invest_Count");

        Assert.Equal(2, eliminate.Rows);
        Assert.Equal([2, 4], eliminate.SampleRows);
        Assert.Equal("x", eliminate.Reason.Args["value"]);
        Assert.Equal(1, invest.Rows);
        Assert.Equal([4], invest.SampleRows);
    }

    [Fact]
    public void ThousandsOfRowsWithTheSameProblemAreOneReasonWithAFewSampleRows()
    {
        var rows = Enumerable.Range(1, 3_000).Select(i => $"{1000 + i},100-500,0,0,1").ToArray();

        var outcome = Read(rows);

        Assert.Equal(3_000, outcome.Rows.Count);
        var reason = Assert.Single(outcome.IgnoredValues);
        Assert.Equal(NoticeCodes.RowInvalidValue, reason.Reason.Code);
        Assert.Equal("integer", reason.Reason.Args["expected"]);
        Assert.Equal(3_000, reason.Rows);
        Assert.Equal(CsvSourceReader.MaxSampleRows, reason.SampleRows.Count);
        Assert.Equal([2, 3, 4, 5, 6], reason.SampleRows);
    }

    /// <summary>
    /// Lo que los exports escriben en una celda sin dato es vacío, no un error: ni rechaza
    /// la fila ni se informa como valor ilegible.
    /// </summary>
    [Theory]
    [InlineData("N/A")]
    [InlineData("#N/A")]
    [InlineData("-")]
    [InlineData("(En blanco)")]
    [InlineData("(Blank)")]
    [InlineData("NULL")]
    public void ANoValueMarkerIsReadAsEmpty(string marker)
    {
        var outcome = Read($"1001,{marker},3,0,1");

        var row = Assert.Single(outcome.Rows);
        Assert.Null(row.Int("Eliminate_Count"));
        Assert.Equal(3, row.Int("Invest_Count"));
        Assert.Empty(outcome.Rejections);
        Assert.Empty(outcome.IgnoredValues);
    }

    [Fact]
    public void AMissingKeyRejectsTheRowAndTheMostFrequentReasonComesFirst()
    {
        var outcome = Read(
            ",0,0,0,1",
            "1002,a,0,0,1",
            ",0,0,0,1",
            "N/A,0,0,0,1");

        Assert.Single(outcome.Rows);
        var reason = Assert.Single(outcome.Reasons);
        Assert.Equal(NoticeCodes.RowMissingKey, reason.Reason.Code);
        Assert.Equal(3, reason.Rows);
    }

    [Fact]
    public void ACleanFileHasNoReasons()
    {
        var outcome = Read("1001,0,3,0,1", "1002,1,0,0,1");

        Assert.Empty(outcome.Rejections);
        Assert.Empty(outcome.Reasons);
        Assert.Empty(outcome.IgnoredValues);
    }

    [Fact]
    public void TheNumberOfReasonsIsBounded()
    {
        var outcome = Read("1001,a,b,c,d");

        Assert.True(outcome.IgnoredValues.Count <= CsvSourceReader.MaxReasons);
        Assert.Equal(4, outcome.IgnoredValues.Count);
    }

    /// <summary>
    /// El EAI no puede repetirse: queda la última fila de cada uno y las anteriores se
    /// rechazan con su motivo. Antes entraban las dos y la base de datos fallaba entera.
    /// </summary>
    [Fact]
    public void ARepeatedEaiKeepsTheLastRowAndReportsTheOthers()
    {
        var master = SourceSchemaCatalog.Default.Require("eai_master.csv");
        using var reader = new StringReader(
            "EAI_Code,Application_Name,Kenna_Score\n"
            + "1001,Claims,700\n"
            + "1002,Billing,300\n"
            + "1001,Claims,650\n");

        var outcome = new CsvSourceReader(master).Read(reader);

        Assert.Equal(2, outcome.Rows.Count);
        Assert.Equal(650, outcome.Rows.Single(r => r.Text("EAI_Code") == "1001").Int("Kenna_Score"));

        var rejection = Assert.Single(outcome.Rejections);
        Assert.Equal(2, rejection.RowNumber);
        Assert.Equal(NoticeCodes.RowDuplicateKey, rejection.Reason.Code);
        Assert.Equal(NoticeCodes.RowDuplicateKey, Assert.Single(outcome.Reasons).Reason.Code);
    }
}

/// <summary>
/// Los números, fechas, sí/no e identificadores tal como los escriben los exports en
/// cualquier configuración regional.
/// </summary>
public sealed class RegionalValueTests
{
    private static ParseOutcome Parse(ColumnType type, string raw)
        => ColumnParser.Parse(new ColumnSchema { Name = "Value", Type = type, Required = false }, raw);

    [Theory]
    [InlineData("1.234,5", "1234.5")]
    [InlineData("1,234.5", "1234.5")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("97,67", "97.67")]
    [InlineData("97.67", "97.67")]
    [InlineData("1,234,567.5", "1234567.5")]
    [InlineData("92 %", "92")]
    [InlineData("1 234,5", "1234.5")]
    [InlineData("-3,5", "-3.5")]
    public void DecimalsInAnyRegionalFormat(string raw, string expected)
    {
        var outcome = Parse(ColumnType.Decimal, raw);

        Assert.True(outcome.Ok, raw);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), outcome.Value);
    }

    /// <summary>
    /// Un entero acepta decimales en cero, que es como Power BI exporta "Suma de". Antes
    /// "650,00" se leía con el formato invariante y daba 65000 sin aviso.
    /// </summary>
    [Theory]
    [InlineData("650", 650)]
    [InlineData("650,00", 650)]
    [InlineData("650.0", 650)]
    [InlineData("1.234", 1234)]
    [InlineData("1,234", 1234)]
    [InlineData("12.345.678", 12345678)]
    [InlineData("1 234", 1234)]
    [InlineData("1.234,00", 1234)]
    public void IntegersInAnyRegionalFormat(string raw, int expected)
    {
        var outcome = Parse(ColumnType.Int, raw);

        Assert.True(outcome.Ok, raw);
        Assert.Equal(expected, outcome.Value);
    }

    [Theory]
    [InlineData("712,5")]
    [InlineData("100-500")]
    [InlineData("1,23,4")]
    [InlineData("abc")]
    public void WhatIsNotAWholeNumberIsNotReadAsOne(string raw)
        => Assert.False(Parse(ColumnType.Int, raw).Ok, raw);

    [Theory]
    [InlineData("VERDADERO", true)]
    [InlineData("FALSO", false)]
    [InlineData("Sí", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    public void BooleansInSpanishAndEnglish(string raw, bool expected)
        => Assert.Equal(expected, Parse(ColumnType.Bool, raw).Value);

    [Theory]
    [InlineData("28/09/2026")]
    [InlineData("8/9/2026")]
    [InlineData("28-09-2026")]
    [InlineData("2026/09/28")]
    [InlineData("2026-09-28T10:30:00")]
    public void DatesInTheUsualExportFormats(string raw)
        => Assert.True(Parse(ColumnType.DateTime, raw).Ok, raw);

    /// <summary>Un EAI que un export trató como número vuelve a ser el mismo código que en los otros archivos.</summary>
    [Theory]
    [InlineData("14381", "14381")]
    [InlineData("14,381", "14381")]
    [InlineData("14.381", "14381")]
    [InlineData("14381.0", "14381")]
    [InlineData("=\"00123\"", "00123")]
    [InlineData("APP-0042", "APP-0042")]
    [InlineData("1.5", "1.5")]
    public void IdentifiersComeBackToTheirDigits(string raw, string expected)
        => Assert.Equal(expected, Parse(ColumnType.Code, raw).Value);
}

/// <summary>
/// El encabezado del EAI Master tal como llega de un export de Power BI: con "Suma de"
/// en los campos resumidos, "MFA" junto al nombre completo, la jerarquía de fecha y sin
/// APP_ID.
/// </summary>
public sealed class PowerBiExportTests
{
    private static readonly string[] Header =
    [
        "Application Status", "Application_Disposition", "Application_Name", "CIO", "Cloud_Indicator", "Cloud_Type",
        "Cloud_Vendor", "Coding_Language", "Country Name", "CRE", "Critical Application Indicator", "MFA",
        "Does this Application Utilize MFA?", "DR_In_Scope", "EAI_Code", "Internet_Facing", "Suma de Kenna_Score",
        "Suma de L1_Business_Capability_Count", "LOB", "Suma de Operational_Score", "Primary_Business_Capability",
        "Suma de Raw_Tech_Score", "Recovery_Point_Objective_Range", "Recovery_Time_Objective_Range", "Region Name",
        "SUB_LOB", "Year", "Quarter", "Month", "Day", "User Count",
    ];

    private static string Csv(params string[][] rows)
        => string.Join(',', Header) + "\n" + string.Join('\n', rows.Select(r => string.Join(',', r))) + "\n";

    private static string[] Row(string eai, string mfa, string utilizesMfa, string kenna, string l1, string operational, string raw, string users)
        =>
        [
            "Active", "Tolerate", "Claims", "A. Moreno", "No", "", "On-Premise", "Java", "Chile", "B. Cardoso", "Y", mfa,
            utilizesMfa, "Yes", eai, "N", kenna, l1, "Individual", operational, "Policy Servicing", raw, "4-24 hrs.",
            "48-72 hrs.", "LATAM", "Life", "2026", "Trimestre 3", "septiembre", "28", users,
        ];

    [Fact]
    public void EveryFieldIsRecognizedAndTheFullNameWinsOverTheMfaAlias()
    {
        var master = SourceSchemaCatalog.Default.Require("eai_master.csv");
        using var reader = new StringReader(Csv(
            Row("14381", "Other", "Yes", "\"120,00\"", "2", "92", "\"97,67\"", "350"),
            Row("\"14,382\"", "Other", "No", "N/A", "(En blanco)", "-", "\"80,5\"", "1.234")));

        var outcome = new CsvSourceReader(master).Read(reader);

        Assert.Empty(outcome.Rejections);
        Assert.Empty(outcome.IgnoredValues);
        Assert.Equal(2, outcome.Rows.Count);

        var first = outcome.Rows[0];
        Assert.Equal("Yes", first.Text("Does this Application Utilize MFA?"));
        Assert.Equal(120, first.Int("Kenna_Score"));
        Assert.Equal(2, first.Int("L1_Business_Capability_Count"));
        Assert.Equal(92m, first.Decimal("Operational_Score"));
        Assert.Equal(97.67m, first.Decimal("Raw_Tech_Score"));
        Assert.Equal(350, first.Int("User Count"));
        Assert.Null(first.Text("APP_ID"));

        var second = outcome.Rows[1];
        Assert.Equal("14382", second.Text("EAI_Code"));
        Assert.Null(second.Int("Kenna_Score"));
        Assert.Null(second.Int("L1_Business_Capability_Count"));
        Assert.Equal(1234, second.Int("User Count"));
    }

    [Fact]
    public void TheMappingReviewProposesTheSameColumnsTheReaderLoads()
    {
        var master = SourceSchemaCatalog.Default.Require("eai_master.csv");

        var resolution = ColumnMapping.Resolve(master, Header);
        var byColumn = resolution.Columns.ToDictionary(c => c.Column.Name);

        Assert.True(resolution.IsValid);
        Assert.Equal("Does this Application Utilize MFA?", byColumn["Does this Application Utilize MFA?"].Header);
        Assert.Equal(ColumnMatch.Name, byColumn["Does this Application Utilize MFA?"].How);
        Assert.Equal("Suma de L1_Business_Capability_Count", byColumn["L1_Business_Capability_Count"].Header);
        Assert.Equal(ColumnMatch.Missing, byColumn["APP_ID"].How);
        Assert.Equal(["MFA", "Year", "Quarter", "Month", "Day"], resolution.UnusedHeaders);
    }

    /// <summary>
    /// En la organización el APP_ID es el mismo código que el EAI: sin la columna en el
    /// maestro, la deuda técnica se une por el EAI. Con la columna, manda la columna.
    /// </summary>
    [Fact]
    public async Task WithoutAppIdInTheMasterTechDebtIsLinkedByTheEaiCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "modscore-pbi-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            using var service = new UploadService(root);

            static Domain.Sources.IncomingSourceFile File(string name, string content)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                return new Domain.Sources.IncomingSourceFile(name, bytes.Length, () => new MemoryStream(bytes));
            }

            var outcome = await service.PublishAsync(
                [
                    File("eai.csv", "EAI_Code,Application_Name,Suma de Kenna_Score\n1001,Claims,\"120,00\"\n\"1,002\",Billing,300\n1003,Payments,\n"),
                    File("deuda.csv", "APP_ID,COMBINED_APP_SCORE\n1001,1.5\n1002.0,4\n"),
                ],
                "sin APP_ID",
                "tests",
                DateTimeOffset.UtcNow);

            Assert.True(outcome.Accepted, string.Join(" | ", outcome.Errors));

            var snapshot = await new FileSnapshotProvider(root).GetAllAsync();

            Assert.Equal(1.5m, snapshot.Find("1001")?.TechDebtScore);

            // Los dos códigos llegaron escritos como número ("1,002" y "1002.0") y se unen igual.
            Assert.Equal(4m, snapshot.Find("1002")?.TechDebtScore);
            Assert.Null(snapshot.Find("1003")?.TechDebtScore);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Sum of Kenna_Score")]
    [InlineData("Promedio de Kenna_Score")]
    [InlineData("Average of Kenna Score")]
    [InlineData("Máx. de Kenna_Score")]
    public void ASummaryPrefixStillNamesTheField(string header)
    {
        var kenna = SourceSchemaCatalog.Default.Require("eai_master.csv").Columns.Single(c => c.Name == "Kenna_Score");

        Assert.True(kenna.Matches(header));
        Assert.False(kenna.Matches("Recuento de Kenna_Score"));
    }
}

/// <summary>
/// El export real de Veracode SCA de la organización (2026-10-05): 28 columnas, ocho campos
/// con otro nombre que el contrato (SVR_DSCR, FND_STTS_DSCR…) y nueve columnas que el
/// cálculo no usa. Antes se rechazaba el archivo entero por las dos obligatorias.
/// </summary>
public sealed class VeracodeExportTests
{
    private static readonly string[] Header =
    [
        "FLAW_IDX_NUM", "APP_NM", "EAI_CD", "APP_ID", "SVR_DSCR", "FND_STTS_DSCR", "FRST_FND_DT",
        "LIB_FRST_FND_ACT_SCAN_DT", "ROPEN_DT", "LST_FND_DT", "RSLV_DT", "FND_AGE_DSCR", "SCAN_TYP_DSCR",
        "ROPEN_FND_DSCR", "CVE_ID", "CVSSV3_SCOR", "SUM_DSCR", "VUL_NM", "CMPNT_NM", "CMPNT_VER_NUM", "LIB_NM",
        "LIB_DSCR", "LIB_VND_NM", "PUB_DT", "ACCSBLTY_DSCR", "SCA_ID", "CRT_DT", "LST_UPDT_DT",
    ];

    private static SourceSchema Sca => SourceSchemaCatalog.Default.Require("veracode_sca_open_flaws.csv");

    [Fact]
    public void EveryFieldIsRecognizedAndTheExtraColumnsAreLeftOut()
    {
        var resolution = ColumnMapping.Resolve(Sca, Header);

        Assert.True(resolution.IsValid);
        Assert.DoesNotContain(resolution.Columns, c => c.How == ColumnMatch.Missing);

        var byColumn = resolution.Columns.ToDictionary(c => c.Column.Name);
        Assert.Equal(ColumnMatch.Alias, byColumn["SVR_DSC"].How);
        Assert.Equal("SVR_DSCR", byColumn["SVR_DSC"].Header);
        Assert.Equal("FND_STTS_DSCR", byColumn["FND_STT"].Header);
        Assert.Equal(
            ["LIB_FRST_FND_ACT_SCAN_DT", "ROPEN_DT", "SUM_DSCR", "CMPNT_NM", "CMPNT_VER_NUM", "LIB_DSCR", "SCA_ID", "CRT_DT", "LST_UPDT_DT"],
            resolution.UnusedHeaders);
    }

    [Fact]
    public void ARowOfTheExportBecomesAFlawWithItsSeverityAndStatus()
    {
        var row = string.Join(
            ',',
            "300001", "Chile - Agent Commissions", "14982", "14982", "1-Critical", "Open", "24/08/2023 21:24",
            "24/08/2023 21:24", "", "05/09/2026 21:24", "", "1111", "SCA", "No", "CVE-2018-7489", "9.8", "Summary",
            "Insecure deserialization", "jackson-databind", "2.9.8", "jackson-databind", "JSON library", "FasterXML",
            "05/11/2022 21:24", "External", "77", "24/08/2023 21:24", "05/09/2026 21:24");
        using var reader = new StringReader(string.Join(',', Header) + "\n" + row + "\n");

        var outcome = new CsvSourceReader(Sca).Read(reader);

        Assert.Empty(outcome.Rejections);
        Assert.Empty(outcome.IgnoredValues);

        var flaw = FileRecordMappers.ToScaFlaw(Assert.Single(outcome.Rows));
        Assert.NotNull(flaw);
        Assert.Equal("14982", flaw.EaiCode);
        Assert.Equal("1-Critical", flaw.SeverityDescription);
        Assert.Equal("Open", flaw.FindingStatus);
        Assert.Equal(1111, flaw.FindingAge);
        Assert.Equal(9.8m, flaw.Cvssv3Score);
        Assert.Equal("FasterXML", flaw.LibraryVendor);
        Assert.Equal("External", flaw.Accessibility);
    }

    /// <summary>
    /// La descripción de una vulnerabilidad (SUM_DSCR) puede pasar de 4 096 caracteres, entre
    /// comillas y con saltos de línea. El cálculo no la usa, y el archivo entra.
    /// </summary>
    [Fact]
    public void ALongQuotedDescriptionDoesNotRejectTheFile()
    {
        var summary = "\"" + string.Concat(Enumerable.Repeat("A crafted \"\"payload\"\" may cause a denial of service,\nsee the advisory. ", 120)) + "\"";
        string[] cells = [.. Enumerable.Repeat(string.Empty, Header.Length)];
        cells[0] = "300001";
        cells[2] = "14982";
        cells[4] = "1 - Critical";
        cells[5] = "Open";
        cells[Array.IndexOf(Header, "SUM_DSCR")] = summary;

        using var reader = new StringReader(string.Join(',', Header) + "\n" + string.Join(',', cells) + "\n");

        var outcome = new CsvSourceReader(Sca).Read(reader);

        Assert.True(summary.Length > 4096);
        Assert.Empty(outcome.Rejections);
        Assert.Equal("1 - Critical", Assert.Single(outcome.Rows).Text("SVR_DSC"));
    }

    /// <summary>
    /// Los valores reales, confirmados el 2026-10-05: severidad «1 - Critical» a «4 - Low», con
    /// espacios alrededor del guion, y estado «Open». Se cuentan por la palabra, igual que
    /// «1-Critical»; una aplicación que no está en el archivo no se considera escaneada.
    /// </summary>
    [Fact]
    public async Task TheRealSeverityAndStatusValuesAreCountedPerApplication()
    {
        var root = Path.Combine(Path.GetTempPath(), "modscore-sca-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        try
        {
            using var service = new UploadService(root);

            static Domain.Sources.IncomingSourceFile File(string name, string content)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                return new Domain.Sources.IncomingSourceFile(name, bytes.Length, () => new MemoryStream(bytes));
            }

            static string Flaw(int index, string eai, string severity)
                => string.Join(',', [index.ToString(CultureInfo.InvariantCulture), "App", eai, eai, severity, "Open", .. Enumerable.Repeat(string.Empty, Header.Length - 6)]);

            var sca = string.Join('\n', [
                string.Join(',', Header),
                Flaw(1, "1001", "1 - Critical"),
                Flaw(2, "1001", "2 - High"),
                Flaw(3, "1001", "2 - High"),
                Flaw(4, "1001", "3 - Medium"),
                Flaw(5, "1001", "4 - Low"),
                Flaw(6, "1002", "4 - Low"),
            ]) + "\n";

            var outcome = await service.PublishAsync(
                [
                    File("eai.csv", "EAI_Code,Application_Name,Kenna_Score\n1001,Claims,120\n1002,Billing,300\n1003,Payments,\n"),
                    File("0809 veracode_sca_open_flaws_6.csv", sca),
                ],
                "export de Veracode",
                "tests",
                DateTimeOffset.UtcNow);

            Assert.True(outcome.Accepted, string.Join(" | ", outcome.Errors));

            var snapshot = await new FileSnapshotProvider(root).GetAllAsync();

            var claims = snapshot.Find("1001")?.ScaFlaws;
            Assert.NotNull(claims);
            Assert.Equal((1, 2, 1, 1), (claims.Critical, claims.High, claims.Medium, claims.Low));

            var billing = snapshot.Find("1002")?.ScaFlaws;
            Assert.NotNull(billing);
            Assert.Equal((0, 0, 0, 1), (billing.Critical, billing.High, billing.Medium, billing.Low));

            Assert.Null(snapshot.Find("1003")?.ScaFlaws);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
