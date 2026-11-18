using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round220TableReferenceRuleIdTests
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
        var range = new CellRange(A("C1"), A("C2"));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 1, Formula = "=C1>MAX(Sales[Amount])" });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Priority = 0, Formula = "=C1>MAX(Sales[[#Data],[Amount]])" });
        sheet.Validations.Add(new PulseOffice.Sheets.DataValidation { Range = new CellRange(A("D1"), A("D1")), Kind = ValidationKind.List, FirstFormula = "Sales[Amount]" });
        sheet.Validations.Add(new PulseOffice.Sheets.DataValidation { Range = new CellRange(A("D1"), A("D1")), Kind = ValidationKind.List, FirstFormula = "Sales[[#Data],[Amount]]" });
        return workbook;
    }

    private static MemoryStream Written()
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(Scene(), stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Identical_flat_rules_keep_their_own_structured_forms()
    {
        using var stream = Written();
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal("=C1>MAX(Sales[Amount])", read.ConditionalRules.Single(rule => rule.Priority == 1).Formula);
        Assert.Equal("=C1>MAX(Sales[[#Data],[Amount]])", read.ConditionalRules.Single(rule => rule.Priority == 0).Formula);
        Assert.Equal(["Sales[Amount]", "Sales[[#Data],[Amount]]"], read.Validations.Select(validation => validation.FirstFormula));
    }

    [Fact]
    public void A_validation_edited_elsewhere_keeps_its_flat_form_and_the_other_keeps_its_own()
    {
        using var stream = Written();
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = package.WorkbookPart!.WorksheetParts.Single().Worksheet!;
            worksheet.Descendants<Formula1>().First().Text = "$A$2:$A$9";
            worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.DoesNotContain("Sales", read.Validations[0].FirstFormula, StringComparison.Ordinal);
        Assert.Equal("Sales[[#Data],[Amount]]", read.Validations[1].FirstFormula);
    }
}
