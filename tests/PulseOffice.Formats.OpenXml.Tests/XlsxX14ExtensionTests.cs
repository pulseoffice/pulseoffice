using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;
using CellValue = PulseOffice.Sheets.Values.CellValue;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxX14ExtensionTests
{
    private const string Validations =
        "<ext xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" uri=\"{CCE6A557-97BC-4b89-ADB6-D9C93CAAB3DF}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\">"
        + "<x14:dataValidations count=\"2\" xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\">"
        + "<x14:dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\"><x14:formula1><xm:f>Foglio2!$A$2:$A$4</xm:f></x14:formula1><xm:sqref>A1</xm:sqref></x14:dataValidation>"
        + "<x14:dataValidation type=\"whole\" operator=\"between\" error=\"Fuori\"><x14:formula1><xm:f>Foglio2!$B$1</xm:f></x14:formula1><x14:formula2><xm:f>Foglio2!$B$2</xm:f></x14:formula2><xm:sqref>B1:B3</xm:sqref></x14:dataValidation>"
        + "</x14:dataValidations></ext>";

    private const string Conditions =
        "<ext xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" uri=\"{78C0D931-6437-407d-A8EE-F0AAD7539E65}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\">"
        + "<x14:conditionalFormattings>"
        + "<x14:conditionalFormatting xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\">"
        + "<x14:cfRule type=\"beginsWith\" priority=\"3\" operator=\"beginsWith\" id=\"{1}\"><xm:f>LEFT(A1,LEN(Foglio2!$A$1))=Foglio2!$A$1</xm:f><xm:f>Foglio2!$A$1</xm:f>"
        + "<x14:dxf><fill><patternFill><bgColor rgb=\"FFFF0000\"/></patternFill></fill></x14:dxf></x14:cfRule><xm:sqref>A1</xm:sqref></x14:conditionalFormatting>"
        + "<x14:conditionalFormatting xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\">"
        + "<x14:cfRule type=\"cellIs\" priority=\"2\" operator=\"greaterThan\" id=\"{2}\"><xm:f>Foglio2!$A$2</xm:f>"
        + "<x14:dxf><font><b/></font></x14:dxf></x14:cfRule>"
        + "<x14:cfRule type=\"cellIs\" priority=\"1\" operator=\"equal\" id=\"{3}\"><xm:f>5</xm:f><x14:dxf/></x14:cfRule>"
        + "<xm:sqref>B1:B4</xm:sqref></x14:conditionalFormatting>"
        + "</x14:conditionalFormattings></ext>";

    private static Workbook ReadWithExtensions(params string[] extensions)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Text("Nord"));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            var list = worksheet.GetFirstChild<WorksheetExtensionList>() ?? worksheet.AppendChild(new WorksheetExtensionList());
            foreach (var extension in extensions) list.AppendChild(new WorksheetExtension(extension));
            worksheet.Save();
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    [Fact]
    public void LeConvalideNellEstensioneX14SiLeggonoESopravvivono()
    {
        var workbook = ReadWithExtensions(Validations);

        foreach (var sheet in new[] { workbook.Sheets[0], RoundTrip(workbook).Sheets[0] })
        {
            Assert.Equal(2, sheet.Validations.Count);
            var list = sheet.Validations.Single(validation => validation.Kind == ValidationKind.List);
            Assert.Contains("Foglio2", list.FirstFormula, StringComparison.Ordinal);
            Assert.Contains("A$4", list.FirstFormula, StringComparison.Ordinal);

            var whole = sheet.Validations.Single(validation => validation.Kind == ValidationKind.WholeNumber);
            Assert.Equal("B1:B3", whole.Range.ToString());
            Assert.Contains("B$2", whole.SecondFormula, StringComparison.Ordinal);
            Assert.Equal("Fuori", whole.ErrorMessage);
        }
    }

    [Fact]
    public void LeFormattazioniCondizionaliNellEstensioneX14SiLeggonoESopravvivono()
    {
        var workbook = ReadWithExtensions(Conditions);

        foreach (var sheet in new[] { workbook.Sheets[0], RoundTrip(workbook).Sheets[0] })
        {
            Assert.Equal(3, sheet.ConditionalRules.Count);

            var begins = sheet.ConditionalRules.Single(rule => rule.Range.ToString() == "A1");
            Assert.Contains("LEFT(", begins.Formula, StringComparison.Ordinal);
            Assert.Contains("Foglio2", begins.Formula, StringComparison.Ordinal);
            Assert.Equal("#FF0000", begins.Style.BackgroundHex);

            var greater = sheet.ConditionalRules.Single(rule => rule.Range.ToString() == "B1:B4" && rule.Formula is not null);
            Assert.Contains("B1>", greater.Formula, StringComparison.Ordinal);
            Assert.True(greater.Style.Font.Bold);

            var equal = sheet.ConditionalRules.Single(rule => rule.Range.ToString() == "B1:B4" && rule.Formula is null);
            Assert.Equal(ConditionOperator.Equal, equal.Operator);
            Assert.Equal(["5"], equal.Operands);
            Assert.True(equal.Priority < greater.Priority);
        }
    }

    [Fact]
    public void IlTestoDiUnaRegolaInizia_ConSiScriveNellAttributoTextComeExcel()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)),
            Operator = ConditionOperator.BeginsWith,
            Operands = ["Nord \"Est\""],
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var rule = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<ConditionalFormattingRule>().Single();
            Assert.Equal("Nord \"Est\"", rule.Text?.Value);
            Assert.Equal("LEFT(A1,LEN(\"Nord \"\"Est\"\"\"))=\"Nord \"\"Est\"\"\"", rule.GetFirstChild<Formula>()!.Text);
        }

        var read = Assert.Single(RoundTrip(workbook).Sheets[0].ConditionalRules);
        Assert.Equal(ConditionOperator.BeginsWith, read.Operator);
        Assert.Equal(["Nord \"Est\""], read.Operands);
    }
}
