using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round205FilterTests
{
    private static MemoryStream WithDynamicFilter(DynamicFilter dynamic)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("N"));
        double[] values = [1, 2, 6];
        for (var index = 0; index < values.Length; index++) sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Number(values[index]));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var filter = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<AutoFilter>().Single();
            filter.Append(new FilterColumn(dynamic) { ColumnId = 0 });
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void An_above_average_dynamic_filter_is_read_as_a_condition_on_its_stored_average()
    {
        using var stream = WithDynamicFilter(new DynamicFilter { Type = DynamicFilterValues.AboveAverage, Val = 3 });

        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal([1, 2], new SheetFilterService().RowsToHide(read, read.AutoFilter.Range!.Value, read.AutoFilter.Filters).Order());
    }

    [Fact]
    public void A_date_period_dynamic_filter_is_read_as_its_stored_bounds()
    {
        using var stream = WithDynamicFilter(new DynamicFilter { Type = DynamicFilterValues.ThisMonth, Val = 2, MaxVal = 6 });

        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal([1, 3], new SheetFilterService().RowsToHide(read, read.AutoFilter.Range!.Value, read.AutoFilter.Filters).Order());
    }
}
