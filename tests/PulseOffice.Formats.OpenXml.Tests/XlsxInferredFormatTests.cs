using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using ModelValue = PulseOffice.Sheets.Values.CellValue;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxInferredFormatTests
{
    [Fact]
    public void IlFormatoDedottoDiUnaFormulaSiScriveSullaCellaComeFannoLeAltreSuite()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var date = workbook.NumberFormats.Register("dd/mm/yyyy");
        sheet[new CellAddress(0, 2)] = new PulseOffice.Sheets.Cell
        {
            Value = ModelValue.Number(42209),
            Formula = "=A1-A2",
            InferredFormatId = date,
        };

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);

        var part = document.WorkbookPart!;
        var cell = part.WorksheetParts.Single().Worksheet!.Descendants<Cell>().Single(candidate => candidate.CellReference == "C1");
        var format = part.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ElementAt((int)cell.StyleIndex!.Value);
        var code = part.WorkbookStylesPart.Stylesheet!.NumberingFormats?.Elements<NumberingFormat>()
            .FirstOrDefault(candidate => candidate.NumberFormatId!.Value == format.NumberFormatId!.Value)?.FormatCode?.Value;

        Assert.True(format.NumberFormatId!.Value == 14 || code == "dd/mm/yyyy");
    }
}
