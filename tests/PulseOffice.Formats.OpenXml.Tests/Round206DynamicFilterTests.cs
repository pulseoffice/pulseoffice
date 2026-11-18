using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round206DynamicFilterTests
{
    private static MemoryStream WithDynamicFilter(DynamicFilterValues type)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Date"));
        DateTime[] dates = [new(2024, 2, 10), new(2025, 2, 3), new(2025, 5, 1), new(2023, 3, 31)];
        for (var index = 0; index < dates.Length; index++)
            sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Number(dates[index].ToOADate()));
        sheet.SetValue(new CellAddress(5, 0), CellValue.Text("none"));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(6, 0)));
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var filter = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<AutoFilter>().Single();
            filter.Append(new FilterColumn(new DynamicFilter { Type = type }) { ColumnId = 0 });
        }
        stream.Position = 0;
        return stream;
    }

    private static IEnumerable<int> Hidden(PulseOffice.Sheets.Worksheet sheet) =>
        new SheetFilterService().RowsToHide(sheet, sheet.AutoFilter.Range!.Value, sheet.AutoFilter.Filters).Order();

    [Fact]
    public void A_month_filter_keeps_that_month_of_every_year()
    {
        using var stream = WithDynamicFilter(DynamicFilterValues.February);

        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal([3, 4, 5, 6], Hidden(read));
    }

    [Fact]
    public void A_quarter_filter_survives_the_crossing_to_ods()
    {
        using var stream = WithDynamicFilter(DynamicFilterValues.Quarter1);
        var read = new XlsxReader().Read(stream).Value!;
        using var ods = new MemoryStream();
        new OdsWriter().Write(read, ods);
        ods.Position = 0;

        var crossed = new OdsReader().Read(ods).Value!.Sheets[0];

        Assert.Equal([3, 5, 6], Hidden(crossed));
    }
}
