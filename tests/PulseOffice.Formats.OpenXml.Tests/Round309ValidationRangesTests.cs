using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round309ValidationRangesTests
{
    [Fact]
    public void A_relative_formula_over_several_areas_is_anchored_on_each_area()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)), Kind = ValidationKind.Custom, FirstFormula = "A1>0",
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var element = package.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.DataValidation>().Single();
            element.SequenceOfReferences = new ListValue<StringValue> { InnerText = "A1:A2 C3:C4" };
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0].Validations;

        Assert.Equal(2, read.Count);
        Assert.Contains("A1", read[0].FirstFormula, StringComparison.Ordinal);
        Assert.Equal(new CellAddress(2, 2), read[1].Range.Start);
        Assert.Contains("C3", read[1].FirstFormula, StringComparison.Ordinal);
        Assert.DoesNotContain("A1", read[1].FirstFormula, StringComparison.Ordinal);
    }
}
