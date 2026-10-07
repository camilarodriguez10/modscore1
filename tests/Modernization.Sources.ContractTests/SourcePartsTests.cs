using Modernization.Domain;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// ADR 0034: una fuente puede llegar en varios archivos —uno por región— y se lee como una
/// sola, sin perder de qué archivo y de qué fila sale cada cosa.
/// </summary>
public sealed class SourcePartsTests
{
    private static readonly SourceSchema Apim = SourceSchemaCatalog.Default.Require("apim_onboarded_eais.csv");

    /// <summary>La misma fuente con la clave única, como el maestro EAI.</summary>
    private static readonly SourceSchema UniqueApim = Apim with { UniqueKey = true };

    [Fact]
    public void TheFilesOfOneSourceAreReadAsOneWithTheirOwnRowNumbers()
    {
        var read = new CsvSourceReader(Apim).ReadParts(
        [
            Part("EMEA.csv", "EAI,Country\n101,Spain\n102,France\n"),
            Part("US.csv", "Country,EAI\nUSA,201\n"),
        ]);

        Assert.Null(read.FirstError);
        Assert.Equal(["101", "102", "201"], read.Merged.Rows.Select(r => r.Text("EAI")));
        Assert.Equal([2, 3, 2], read.Merged.Rows.Select(r => r.Number));
        Assert.Equal([0, 0, 1], read.Merged.Rows.Select(r => r.Part));
        Assert.Equal([2, 1], read.Parts.Select(p => p.Outcome!.Rows.Count));
    }

    [Fact]
    public void WithoutAUniqueKeyRepeatedKeysAcrossFilesAreAllKept()
    {
        var read = new CsvSourceReader(Apim).ReadParts(
        [
            Part("EMEA.csv", "EAI,Country\n101,Spain\n"),
            Part("US.csv", "EAI,Country\n101,USA\n"),
        ]);

        Assert.Equal(2, read.Merged.Rows.Count);
        Assert.Empty(read.Merged.Rejections);
    }

    [Fact]
    public void AUniqueKeyThatReturnsInALaterFileStaysWithThatFile()
    {
        var read = new CsvSourceReader(UniqueApim).ReadParts(
        [
            Part("EMEA.csv", "EAI,Country\n101,Spain\n102,France\n"),
            Part("LATAM.csv", "EAI,Country\n101,Chile\n"),
        ]);

        Assert.Equal(["102", "101"], read.Merged.Rows.Select(r => r.Text("EAI")));
        Assert.Equal("Chile", read.Merged.Rows.Single(r => r.Text("EAI") == "101").Text("Country"));

        var emea = read.Parts[0].Outcome!;
        Assert.Single(emea.Rows);
        var rejection = Assert.Single(emea.Rejections);
        Assert.Equal(2, rejection.RowNumber);
        Assert.Equal(NoticeCodes.RowDuplicateKeyInFile, rejection.Reason.Code);
        Assert.Equal("LATAM.csv", rejection.Reason.Args["file"]);

        var reason = Assert.Single(emea.Reasons);
        Assert.Equal(1, reason.Rows);
        Assert.Equal([2], reason.SampleRows);

        Assert.Empty(read.Parts[1].Outcome!.Rejections);
    }

    [Fact]
    public void EachRejectionOfTheSetNamesItsFile()
    {
        var read = new CsvSourceReader(Apim).ReadParts(
        [
            Part("EMEA.csv", "EAI,Country\n101,Spain\n,France\n"),
            Part("US.csv", "EAI,Country\n,USA\n"),
        ]);

        Assert.Equal(["EMEA.csv", "US.csv"], read.Merged.Rejections.Select(r => r.Reason.Args["part"]));
        Assert.All(read.Parts.SelectMany(p => p.Outcome!.Rejections), r => Assert.False(r.Reason.Args.ContainsKey("part")));
    }

    [Fact]
    public void ASingleFileReadsExactlyAsBefore()
    {
        const string Csv = "EAI,Country\n101,Spain\n,France\n";

        var read = new CsvSourceReader(Apim).ReadParts([Part("EMEA.csv", Csv)]);
        var alone = new CsvSourceReader(Apim).Read(new StringReader(Csv));

        Assert.Equal(alone.Rows.Count, read.Merged.Rows.Count);
        Assert.Equal(
            alone.Rejections.Select(r => (r.RowNumber, r.Reason.Code, r.Key)),
            read.Merged.Rejections.Select(r => (r.RowNumber, r.Reason.Code, r.Key)));
    }

    [Fact]
    public void AFileThatCannotBeReadIsReportedAndTheOthersStillAre()
    {
        var read = new CsvSourceReader(Apim).ReadParts(
        [
            Part("EMEA.csv", "EAI,Country\n101,Spain\n"),
            Part("US.csv", "Country,Region\nUSA,North America\n"),
        ]);

        Assert.NotNull(read.Parts[0].Outcome);
        Assert.Equal(NoticeCodes.CsvMissingColumns, read.Parts[1].Error?.Code);
        Assert.Equal(NoticeCodes.CsvMissingColumns, read.FirstError?.Code);
        Assert.Empty(read.Merged.Rows);
    }

    private static CsvSourceReader.SourcePart Part(string label, string content)
        => new(() => new StringReader(content), label);
}
