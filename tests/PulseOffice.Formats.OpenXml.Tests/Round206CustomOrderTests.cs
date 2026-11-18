using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round206CustomOrderTests
{
    private static Workbook Book()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, PulseOffice.Sheets.Values.CellValue.Text("City"));
        workbook.Sheets[0].AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 1)));
        workbook.Sheets[0].AutoFilter.Remember(new SortSettings
        {
            Fields = [new SortField(1) { CustomOrder = ["b", "a"] }, new SortField(0) { CustomOrder = ["Rome, Italy", "Paris"] }],
        });
        return workbook;
    }

    private static MemoryStream Written()
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(Book(), stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_custom_order_item_with_a_comma_survives_the_xlsx_round_trip()
    {
        using var stream = Written();

        var fields = new XlsxReader().Read(stream).Value!.Sheets[0].AutoFilter.Sort!.Fields;

        Assert.Equal(["b", "a"], fields[0].CustomOrder!);
        Assert.Equal(["Rome, Italy", "Paris"], fields[1].CustomOrder!);
    }

    [Fact]
    public void Other_readers_still_see_the_plain_comma_separated_list()
    {
        using var stream = Written();
        using var document = SpreadsheetDocument.Open(stream, false);

        var conditions = document.WorkbookPart!.WorksheetParts.First().Worksheet!
            .Descendants<DocumentFormat.OpenXml.Spreadsheet.SortCondition>().ToList();

        Assert.Equal("Rome, Italy,Paris", conditions[1].CustomList!.Value);
    }
}
