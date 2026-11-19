using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round249ValidationBlankTests
{
    [Fact]
    public void A_validation_without_allow_blank_does_not_ignore_blank_cells()
    {
        var workbook = Book.CreateDefault();
        workbook.Sheets[0].Validations.Add(new PulseOffice.Sheets.DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)), Kind = ValidationKind.List, FirstFormula = "$B$1:$B$2",
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            foreach (var element in package.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.DataValidation>())
                element.AllowBlank = null;
        }

        stream.Position = 0;
        var read = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Validations);

        Assert.False(read.AllowBlank);
    }
}
