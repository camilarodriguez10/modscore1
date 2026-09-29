using Modernization.Domain;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Por qué se rechazaron filas, agrupado por problema. Es lo que permite corregir un
/// archivo confidencial sin mandárselo a nadie.
/// </summary>
public sealed class RejectionReasonTests
{
    private const string Header = "EAI_CODE,Eliminate_Count,Invest_Count,Migrate_Count,Tolerate_Count";

    private static SourceSchema ProductStrategy
        => SourceSchemaCatalog.Default.Files.Single(f => f.FileName == "product_strategy.csv");

    private static CsvSourceReader.ReadOutcome Read(params string[] rows)
    {
        using var reader = new StringReader(Header + "\n" + string.Join('\n', rows) + "\n");
        return new CsvSourceReader(ProductStrategy).Read(reader);
    }

    [Fact]
    public void EveryBadColumnOfARowCountsNotOnlyTheFirst()
    {
        var outcome = Read(
            "1001,x,0,0,1",
            "1002,0,0,0,1",
            "1003,y,z,0,1");

        Assert.Equal(2, outcome.Rejections.Count);

        var eliminate = outcome.Reasons.Single(r => r.Reason.Args["column"] == "Eliminate_Count");
        var invest = outcome.Reasons.Single(r => r.Reason.Args["column"] == "Invest_Count");

        Assert.Equal(2, eliminate.Rows);
        Assert.Equal([2, 4], eliminate.SampleRows);
        Assert.Equal("x", eliminate.Reason.Args["value"]);

        // La fila 4 también tenía Invest_Count mal: el lector lo cuenta aunque el rechazo
        // de la fila solo nombre el primero.
        Assert.Equal(1, invest.Rows);
        Assert.Equal([4], invest.SampleRows);
    }

    [Fact]
    public void ThousandsOfRowsWithTheSameProblemAreOneReasonWithAFewSampleRows()
    {
        var rows = Enumerable.Range(1, 3_000).Select(i => $"{1000 + i},N/A,0,0,1").ToArray();

        var outcome = Read(rows);

        var reason = Assert.Single(outcome.Reasons);
        Assert.Equal(NoticeCodes.RowInvalidValue, reason.Reason.Code);
        Assert.Equal("integer", reason.Reason.Args["expected"]);
        Assert.Equal(3_000, reason.Rows);
        Assert.Equal(CsvSourceReader.MaxSampleRows, reason.SampleRows.Count);
        Assert.Equal([2, 3, 4, 5, 6], reason.SampleRows);
    }

    [Fact]
    public void AMissingKeyIsItsOwnReasonAndTheMostFrequentComesFirst()
    {
        var outcome = Read(
            ",0,0,0,1",
            "1002,a,0,0,1",
            ",0,0,0,1",
            ",0,0,0,1");

        Assert.Equal(2, outcome.Reasons.Count);
        Assert.Equal(NoticeCodes.RowMissingKey, outcome.Reasons[0].Reason.Code);
        Assert.Equal(3, outcome.Reasons[0].Rows);
        Assert.Equal(NoticeCodes.RowInvalidValue, outcome.Reasons[1].Reason.Code);
    }

    [Fact]
    public void ACleanFileHasNoReasons()
    {
        var outcome = Read("1001,0,3,0,1", "1002,1,0,0,1");

        Assert.Empty(outcome.Rejections);
        Assert.Empty(outcome.Reasons);
    }

    [Fact]
    public void TheNumberOfReasonsIsBounded()
    {
        // Cada fila con una columna distinta no alcanza: el contrato tiene pocas. Se
        // comprueba el tope con el máximo que el contrato permite, que debe caber.
        var outcome = Read("1001,a,b,c,d");

        Assert.True(outcome.Reasons.Count <= CsvSourceReader.MaxReasons);
        Assert.Equal(4, outcome.Reasons.Count);
    }

    /// <summary>
    /// Con los dos separadores, el último es el decimal. Antes "1.234,5" se leía como
    /// 1,2345 sin ningún aviso.
    /// </summary>
    [Theory]
    [InlineData("1.234,5", "1234.5")]
    [InlineData("1,234.5", "1234.5")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("97,67", "97.67")]
    [InlineData("97.67", "97.67")]
    [InlineData("1,234,567.5", "1234567.5")]
    public void DecimalsWithBothSeparatorsUseTheLastOneAsTheDecimalPoint(string raw, string expected)
    {
        var column = new ColumnSchema { Name = "Raw_Tech_Score", Type = ColumnType.Decimal, Required = false };

        var outcome = ColumnParser.Parse(column, raw);

        Assert.True(outcome.Ok);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), outcome.Value);
    }
}
