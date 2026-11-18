using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round206SortGateTests
{
    private static (Workbook Workbook, Worksheet Sheet) Book(CellRange merge)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 3; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row * 3 + column));
        sheet.Merges.Add(merge);
        return (workbook, sheet);
    }

    private static readonly CellRange Data = new(CellAddress.First, new CellAddress(3, 2));

    [Fact]
    public void A_merged_header_row_is_refused_only_when_the_sort_has_no_header()
    {
        var (workbook, sheet) = Book(new CellRange(CellAddress.First, new CellAddress(0, 1)));
        var status = new Round206Status();
        var gate = new SheetSortGate(new SheetEditGuard(), status, Round206Scene.Texts, new Engine.Operations.SheetSorter());

        Assert.False(gate.Refuses(workbook, sheet, Data, hasHeader: true));
        Assert.Empty(status.Messages);
        Assert.True(gate.Refuses(workbook, sheet, Data, hasHeader: false));
        Assert.Equal([Round206Scene.Texts["status.sort.merged"]], status.Messages);
    }

    [Fact]
    public void A_left_to_right_sort_checks_merges_across_columns()
    {
        var (workbook, sheet) = Book(new CellRange(new CellAddress(1, 0), new CellAddress(2, 0)));
        var gate = new SheetSortGate(new SheetEditGuard(), new Round206Status(), Round206Scene.Texts, new Engine.Operations.SheetSorter());

        Assert.False(gate.Refuses(workbook, sheet, Data, hasHeader: true, SortOrientation.LeftToRight));
        Assert.True(gate.Refuses(workbook, sheet, Data, hasHeader: true));
        Assert.False(gate.RefusesBeforeChoice(workbook, sheet, Data));
    }
}
