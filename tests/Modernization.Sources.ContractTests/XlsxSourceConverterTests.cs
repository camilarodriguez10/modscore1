using System.Globalization;
using Modernization.Domain;
using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// ADR 0034: un libro de Excel se convierte en el mismo CSV que lee el resto de la carga, y
/// cada defensa contra un ZIP o un XML fabricado se ejercita con topes pequeños.
/// </summary>
public sealed class XlsxSourceConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "modscore-xlsx-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public XlsxSourceConverterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void TheFirstSheetBecomesTheCsvTheReaderAlreadyReads()
    {
        var workbook = TestWorkbooks.Single(
            ["EAI", "Country", "Application Short Name", "Application Name"],
            [14381, "Chile", "RA", "Reference, \"BRD\" example"],
            [10969, "Mexico", "CX", "Customer Portal"]);

        var csv = Convert(workbook, out var conversion);

        Assert.Equal(
            "EAI,Country,Application Short Name,Application Name\n"
            + "14381,Chile,RA,\"Reference, \"\"BRD\"\" example\"\n"
            + "10969,Mexico,CX,Customer Portal\n",
            csv);
        Assert.Equal("Sheet1", conversion.SheetName);
        Assert.Equal(2, conversion.Rows);
        Assert.Equal(4, conversion.Columns);
    }

    [Fact]
    public void DateCellsUseTheirStyleAndPlainNumbersStayNumbers()
    {
        var workbook = TestWorkbooks.Single(
            ["EAI", "Scanned", "Updated", "Score", "Ratio"],
            [1, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 12, 30, 0, DateTimeKind.Unspecified), 45935, new TestWorkbooks.Styled(0.5, TestWorkbooks.DecimalStyle)]);

        var csv = Convert(workbook, out _);

        Assert.Equal("EAI,Scanned,Updated,Score,Ratio\n1,2026-10-05,2026-10-05T12:30:00,45935,0.5\n", csv);
    }

    [Fact]
    public void EmptyRowsAreSkippedAndMissingCellsAreEmpty()
    {
        var workbook = TestWorkbooks.Single(
            [],
            ["EAI", "Country", "Application Name"],
            [14381, null, "Reference"],
            [],
            [10969, "Mexico", null],
            [null, null, null]);

        var csv = Convert(workbook, out var conversion);

        Assert.Equal("EAI,Country,Application Name\n14381,,Reference\n10969,Mexico,\n", csv);
        Assert.Equal(2, conversion.Rows);
    }

    [Fact]
    public void CellsBeyondTheHeaderAreDropped()
    {
        var workbook = TestWorkbooks.Single(
            ["EAI", "Country"],
            [14381, "Chile", "a note nobody loads"]);

        Assert.Equal("EAI,Country\n14381,Chile\n", Convert(workbook, out _));
    }

    [Fact]
    public void TheFirstVisibleSheetIsTheOneRead()
    {
        var workbook = TestWorkbooks.Build(
        [
            new TestWorkbooks.Sheet("Lookups", [["Code", "Meaning"], ["A", "Active"]], Hidden: true),
            new TestWorkbooks.Sheet("APIM", [["EAI", "Country"], [14381, "Chile"]]),
            new TestWorkbooks.Sheet("Notes", [["Text"], ["ignored"]]),
        ]);

        var csv = Convert(workbook, out var conversion);

        Assert.Equal("EAI,Country\n14381,Chile\n", csv);
        Assert.Equal("APIM", conversion.SheetName);
    }

    [Fact]
    public void InlineTextBooleansFormulasErrorsAndExcelEscapesAreRead()
    {
        var workbook = TestWorkbooks.Single(
            ["EAI", "Name", "Critical", "Total", "Lookup", "Note"],
            [
                14381,
                new TestWorkbooks.Raw("<c r=\"B2\" t=\"inlineStr\"><is><r><t>Reference </t></r><r><t>App</t></r><rPh><t>phonetic</t></rPh></is></c>"),
                true,
                new TestWorkbooks.Raw("<c r=\"D2\"><f>SUM(1,2)</f><v>3</v></c>"),
                new TestWorkbooks.Raw("<c r=\"E2\" t=\"e\"><f>VLOOKUP(A2,X:Y,2,0)</f><v>#N/A</v></c>"),
                new TestWorkbooks.Raw("<c r=\"F2\" t=\"str\"><f>A1</f><v>line_x000D_break_x005F_x0041_</v></c>"),
            ]);

        var csv = Convert(workbook, out _);

        Assert.Equal("EAI,Name,Critical,Total,Lookup,Note\n14381,Reference App,TRUE,3,,\"line\rbreak_x0041_\"\n", csv);
    }

    [Fact]
    public void AStrictWorkbookWithPrefixedElementsIsRead()
    {
        var workbook = TestWorkbooks.Build([new TestWorkbooks.Sheet("Sheet1", [["EAI", "Country"], [14381, "Chile"]])], strict: true);

        Assert.Equal("EAI,Country\n14381,Chile\n", Convert(workbook, out _));
    }

    [Fact]
    public void A1904WorkbookCountsDaysFromItsOwnOrigin()
    {
        var workbook = TestWorkbooks.Build(
            [new TestWorkbooks.Sheet("Sheet1", [["EAI", "Scanned"], [1, new TestWorkbooks.Styled(0, TestWorkbooks.DateStyle)]])],
            date1904: true);

        Assert.Equal("EAI,Scanned\n1,1904-01-01\n", Convert(workbook, out _));
    }

    [Theory]
    [InlineData("37", "37")]
    [InlineData("0.1", "0.1")]
    [InlineData("73.489999999999995", "73.49")]
    [InlineData("1E-3", "0.0010")]
    [InlineData("1.234", "1.2340")]
    [InlineData("-12.345", "-12.3450")]
    [InlineData("1234.5", "1234.5")]
    [InlineData("14381", "14381")]
    [InlineData("1.5E+20", "150000000000000000000")]
    [InlineData("not a number", "not a number")]
    public void NumbersAreInvariantWithoutExponentAndNeverLookLikeThousands(string raw, string expected)
        => Assert.Equal(expected, XlsxSourceConverter.FormatNumber(raw));

    [Theory]
    [InlineData("45935", false, "2025-10-05")]
    [InlineData("45935.5", false, "2025-10-05T12:00:00")]
    [InlineData("1", false, "1900-01-01")]
    [InlineData("61", false, "1900-03-01")]
    [InlineData("60", false, "60")]
    [InlineData("0", true, "1904-01-01")]
    [InlineData("-3", false, "-3")]
    public void ExcelDaysBecomeIsoDates(string raw, bool date1904, string expected)
        => Assert.Equal(expected, XlsxSourceConverter.FormatDate(raw, date1904));

    [Theory]
    [InlineData(14, null, true)]
    [InlineData(22, null, true)]
    [InlineData(0, null, false)]
    [InlineData(2, null, false)]
    [InlineData(164, "yyyy-mm-dd", true)]
    [InlineData(165, "[$-409]d-mmm-yy;@", true)]
    [InlineData(166, "h:mm AM/PM", true)]
    [InlineData(167, "0.00", false)]
    [InlineData(168, "[Red]#,##0.00", false)]
    [InlineData(169, "\"days\" 0", false)]
    [InlineData(170, "0.00E+00", false)]
    [InlineData(171, "General", false)]
    public void DateFormatsAreRecognized(int id, string? code, bool expected)
        => Assert.Equal(expected, XlsxSourceConverter.IsDateFormat(id, code));

    // ------------------------------------------------------------------ defensas

    [Fact]
    public void ADoctypeIsRefusedInsteadOfResolved()
    {
        var parts = TestWorkbooks.PartsOf(TestWorkbooks.Single(["EAI"], [1]));
        parts["xl/worksheets/sheet1.xml"] =
            "<?xml version=\"1.0\"?><!DOCTYPE worksheet [<!ENTITY xxe SYSTEM \"file:///c:/windows/win.ini\">]>"
            + $"<worksheet xmlns=\"{TestWorkbooks.MainNamespace}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&xxe;</t></is></c></row></sheetData></worksheet>";

        Assert.Equal(NoticeCodes.XlsxUnreadable, Fails(TestWorkbooks.Zip(parts)));
    }

    [Fact]
    public void AHighlyCompressedSheetIsCutAtItsLimit()
    {
        var rows = new List<object?[]> { new object?[] { "EAI", "Country" } };
        rows.AddRange(Enumerable.Range(1, 5_000).Select(i => new object?[] { i, "Chile" }));

        var limits = XlsxSourceConverter.Limits.Default with { SheetBytes = 64 * 1024 };

        Assert.Equal(NoticeCodes.XlsxTooLarge, Fails(TestWorkbooks.Single([.. rows]), limits));
    }

    [Fact]
    public void TooManyEntriesAreRefusedBeforeTheArchiveIsListed()
    {
        var limits = XlsxSourceConverter.Limits.Default with { Entries = 3 };

        Assert.Equal(NoticeCodes.XlsxTooLarge, Fails(TestWorkbooks.Single(["EAI"], [1]), limits));
    }

    [Fact]
    public void SharedTextHasALimit()
    {
        var limits = XlsxSourceConverter.Limits.Default with { SharedStringChars = 10 };

        Assert.Equal(NoticeCodes.XlsxTooLarge, Fails(TestWorkbooks.Single(["EAI", "Application Name"], [1, "A name longer than ten characters"]), limits));
    }

    [Fact]
    public void TheConvertedCsvHasALimit()
    {
        var rows = new List<object?[]> { new object?[] { "EAI", "Country" } };
        rows.AddRange(Enumerable.Range(1, 2_000).Select(i => new object?[] { i, "Chile" }));

        var limits = XlsxSourceConverter.Limits.Default with { CsvBytes = 1024 };

        Assert.Equal(NoticeCodes.XlsxTooLarge, Fails(TestWorkbooks.Single([.. rows]), limits));
    }

    [Fact]
    public void AHeaderWiderThanTheLimitIsRefused()
    {
        var workbook = TestWorkbooks.Single(
            ["EAI", new TestWorkbooks.Raw($"<c r=\"{TestWorkbooks.ColumnName(XlsxSourceConverter.MaxColumns)}1\" t=\"inlineStr\"><is><t>Far away</t></is></c>")],
            [1]);

        Assert.Equal(NoticeCodes.XlsxTooManyColumns, Fails(workbook));
    }

    [Fact]
    public void ACompoundDocumentIsAProtectedOrOldWorkbook()
    {
        byte[] compound = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

        Assert.Equal(NoticeCodes.XlsxProtected, Fails(compound));
        Assert.Equal(IncomingFileKind.Compound, XlsxSourceConverter.Detect(Write(compound)));
    }

    [Fact]
    public void AZipThatIsNotAWorkbookIsUnreadable()
    {
        var zip = TestWorkbooks.Zip(new Dictionary<string, string> { ["readme.txt"] = "hello" });

        Assert.Equal(IncomingFileKind.Workbook, XlsxSourceConverter.Detect(Write(zip)));
        Assert.Equal(NoticeCodes.XlsxUnreadable, Fails(zip));
    }

    [Fact]
    public void ATruncatedWorkbookIsUnreadable()
    {
        var workbook = TestWorkbooks.Single(["EAI"], [1]);

        Assert.Equal(NoticeCodes.XlsxUnreadable, Fails(workbook[..(workbook.Length / 2)]));
    }

    [Fact]
    public void AWorkbookWithoutRowsSaysSo()
        => Assert.Equal(NoticeCodes.XlsxNoSheet, Fails(TestWorkbooks.Single([], [null, null])));

    [Fact]
    public void ACsvIsText()
        => Assert.Equal(IncomingFileKind.Text, XlsxSourceConverter.Detect(Write("EAI,Country\n1,Chile\n"u8.ToArray())));

    // ------------------------------------------------------------------ apoyo

    private string Convert(byte[] workbook, out XlsxConversion conversion)
    {
        var csv = Path.Combine(_root, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".csv");
        conversion = XlsxSourceConverter.Convert(Write(workbook), csv);
        return File.ReadAllText(csv);
    }

    private string? Fails(byte[] workbook, XlsxSourceConverter.Limits? limits = null)
    {
        var csv = Path.Combine(_root, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".csv");
        var ex = Assert.Throws<SourceSchemaException>(
            () => XlsxSourceConverter.Convert(Write(workbook), csv, limits ?? XlsxSourceConverter.Limits.Default));

        return ex.Notice?.Code;
    }

    private string Write(byte[] content)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".xlsx");
        File.WriteAllBytes(path, content);
        return path;
    }
}
