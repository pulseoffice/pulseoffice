using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using Xunit;
using CellStyle = PulseOffice.Sheets.Styling.CellStyle;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxDefaultCellFormatTests
{
    [Fact]
    public void The_first_cell_format_is_the_default_style_even_when_the_first_cell_is_bold()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Header"));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { Font = CellFont.Default with { Bold = true } }));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        using var document = SpreadsheetDocument.Open(stream, false);
        var stylesheet = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        var first = stylesheet.CellFormats!.Elements<CellFormat>().First();
        var font = stylesheet.Fonts!.Elements<Font>().ElementAt((int)first.FontId!.Value);

        Assert.Null(font.GetFirstChild<Bold>());
        var header = document.WorkbookPart.WorksheetParts.Single().Worksheet!.Descendants<Cell>().First(cell => cell.CellReference == "A1");
        Assert.NotEqual(0u, header.StyleIndex?.Value ?? 0u);
    }
}
