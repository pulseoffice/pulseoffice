using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;
using SortKey = PulseOffice.Sheets.Engine.Operations.SortKey;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round205SortTests
{
    [Fact]
    public void Ascending_sort_puts_numbers_then_text_then_logicals()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.True);
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(5));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("a"));
        sheet.SetValue(new CellAddress(3, 0), CellValue.False);
        sheet.SetValue(new CellAddress(4, 0), CellValue.Number(0.5));

        new SheetSorter().Sort(sheet, new CellRange(CellAddress.First, new CellAddress(4, 0)), [new SortKey(0)]);

        Assert.Equal(0.5, sheet.GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Equal(5, sheet.GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal("a", sheet.GetValue(new CellAddress(2, 0)).AsText());
        Assert.Equal(CellValue.False, sheet.GetValue(new CellAddress(3, 0)));
        Assert.Equal(CellValue.True, sheet.GetValue(new CellAddress(4, 0)));
    }

    [Fact]
    public void A_range_with_merged_data_cells_is_not_sorted()
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.Merges.Add(new CellRange(new CellAddress(0, 1), new CellAddress(0, 2)));
        var range = new CellRange(CellAddress.First, new CellAddress(1, 2));
        var sorter = new SheetSorter();

        Assert.True(sorter.BreaksMerges(sheet, range, hasHeader: false));
        Assert.False(sorter.BreaksMerges(sheet, range, hasHeader: true));
        sorter.Sort(sheet, range, [new SortKey(0)]);

        Assert.Equal(2, sheet.GetValue(CellAddress.First).AsNumber());
    }
}
