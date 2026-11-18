using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round216TableReferenceRuleTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private static Book Scene()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), PulseOffice.Sheets.Values.CellValue.Text("Amount"));
        sheet.SetValue(A("A2"), PulseOffice.Sheets.Values.CellValue.Number(5));
        sheet.SetValue(A("A3"), PulseOffice.Sheets.Values.CellValue.Number(7));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("A3")) });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = new CellRange(A("C1"), A("C2")), Formula = "=C1>MAX(Sales[Amount])" });
        sheet.Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(A("D1"), A("D1")), Kind = ValidationKind.List, FirstFormula = "Sales[Amount]",
        });
        return workbook;
    }

    [Fact]
    public void Conditional_formats_and_validations_are_written_flat_and_read_back_structured()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Scene(), stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            var written = worksheet.Descendants<ConditionalFormattingRule>().SelectMany(rule => rule.Elements<Formula>()).Select(formula => formula.Text)
                .Concat(worksheet.Descendants<Formula1>().Select(formula => formula.Text)).ToList();
            Assert.Equal(2, written.Count);
            Assert.DoesNotContain(written, formula => formula.Contains('[', StringComparison.Ordinal));
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];
        Assert.Equal("=C1>MAX(Sales[Amount])", Assert.Single(read.ConditionalRules).Formula);
        Assert.Equal("Sales[Amount]", Assert.Single(read.Validations).FirstFormula);
    }
}
