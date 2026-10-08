using System.Globalization;
using Modernization.Sources.Dummy;
using Modernization.Sources.Export;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Los CSV exportados deben tener exactamente los nombres de columna de los
/// archivos reales. Es lo que permite desarrollar y probar el adaptador de
/// archivos de la Fase 07 antes de que llegue el primer archivo productivo.
/// </summary>
public sealed class CsvExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "modscore-csv-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private readonly DummyPortfolio _portfolio = DummyPortfolio.Generate(applicationCount: 40);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private IReadOnlyList<CsvSourceExporter.ExportedFile> Export()
        => CsvSourceExporter.Export(_portfolio, _directory);

    private string HeaderOf(string fileName)
        => File.ReadLines(Path.Combine(_directory, fileName)).First();

    [Theory]
    [InlineData("eai_master.csv")]
    [InlineData("architectural_value_by_eai.csv")]
    [InlineData("product_strategy.csv")]
    [InlineData("tech_debt.csv")]
    [InlineData("veracode_sca_open_flaws.csv")]
    [InlineData("oss_score_by_eai.csv")]
    [InlineData("monitoreo.csv")]
    [InlineData("apim_onboarded_eais.csv")]
    public void ExportsOneFilePerSource(string fileName)
    {
        Export();

        Assert.True(File.Exists(Path.Combine(_directory, fileName)), $"Falta {fileName}.");
    }

    /// <summary>
    /// Cabeceras transcritas de las capturas de los archivos reales. Si alguna
    /// cambia, el adaptador de archivos dejaria de encontrar la columna.
    /// </summary>
    [Theory]
    [InlineData("architectural_value_by_eai.csv", "EAI_CODE,Modern_Count,Heritage_Count,Obsolete_Count,Not_Approved_for_Use_Count")]
    [InlineData("product_strategy.csv", "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count")]
    [InlineData("tech_debt.csv", "APP_ID,COMBINED_APP_SCORE")]
    [InlineData("oss_score_by_eai.csv", "EAI Code,OSS Score,OPEN_SRC_SFTWR_HLTH_SCOR_UPDT_TS")]
    [InlineData("monitoreo.csv", "EAI,Agent,APM,Monitoring,Synthetics")]
    public void UsesTheRealColumnNames(string fileName, string expectedHeader)
    {
        Export();

        Assert.Equal(expectedHeader, HeaderOf(fileName));
    }

    [Fact]
    public void ScaFileKeepsTheVeracodeColumnOrder()
    {
        Export();

        Assert.StartsWith(
            "FLAW_IDX_NUM,APP_NM,EAI_CD,APP_ID,SVR_DSC,FND_STT,FRST_FND_DT,LST_FND_DT,RSLV_DT,FND_AGE,SCAN_TY",
            HeaderOf("veracode_sca_open_flaws.csv"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RowCountMatchesTheSourceRecords()
    {
        var files = Export();

        foreach (var file in files)
        {
            var dataRows = File.ReadLines(Path.Combine(_directory, file.FileName)).Skip(1).Count(l => l.Length > 0);
            Assert.Equal(file.RowCount, dataRows);
        }
    }

    [Fact]
    public void MonitoringFlagsUseTheObservedTrueFalseFormat()
    {
        Export();

        var firstRow = File.ReadLines(Path.Combine(_directory, "monitoreo.csv")).Skip(1).First();

        Assert.All(firstRow.Split(',').Skip(1), value => Assert.Contains(value, new[] { "TRUE", "FALSE", string.Empty }, StringComparer.Ordinal));
    }

    [Fact]
    public void ReferenceApplicationIsPresentInTheMasterFile()
    {
        Export();

        var rows = File.ReadLines(Path.Combine(_directory, "eai_master.csv")).Skip(1);

        Assert.Contains(rows, row => row.StartsWith(DummyPortfolio.ReferenceEaiCode + ",", StringComparison.Ordinal));
    }

    /// <summary>Un valor sin dato se escribe vacio, no como "null" ni como cero.</summary>
    [Fact]
    public void MissingValuesAreWrittenAsEmptyFields()
    {
        Export();

        var referenceRow = File.ReadLines(Path.Combine(_directory, "eai_master.csv"))
            .First(l => l.StartsWith(DummyPortfolio.ReferenceEaiCode + ",", StringComparison.Ordinal));

        // El EAI de referencia no tiene dato de DR: la columna debe quedar vacia.
        Assert.Contains(",,", referenceRow, StringComparison.Ordinal);
        Assert.DoesNotContain("null", referenceRow, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Escape CSV conforme a RFC 4180.</summary>
public sealed class CsvBuilderTests
{
    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("con,coma", "\"con,coma\"")]
    [InlineData("con\"comilla", "\"con\"\"comilla\"")]
    [InlineData(" espacio inicial", "\" espacio inicial\"")]
    [InlineData(null, "")]
    public void EscapesValuesThatNeedIt(string? value, string expected)
    {
        var csv = new CsvBuilder("col");
        csv.Row(value);

        var dataRow = csv.Build().Split('\n')[1];

        Assert.Equal(expected, dataRow);
    }

    /// <summary>
    /// Un valor con salto de linea se entrecomilla y ocupa dos lineas fisicas en
    /// el archivo, que es justamente por lo que un CSV no se puede leer con Split.
    /// </summary>
    [Fact]
    public void QuotesValuesContainingLineBreaks()
    {
        var csv = new CsvBuilder("col");
        csv.Row("con\nsalto");

        Assert.Equal("col\n\"con\nsalto\"\n", csv.Build());
    }

    [Fact]
    public void RejectsRowsThatDoNotMatchTheHeader()
    {
        var csv = new CsvBuilder("a", "b");

        Assert.Throws<InvalidOperationException>(() => csv.Row("solo-una"));
    }
}
