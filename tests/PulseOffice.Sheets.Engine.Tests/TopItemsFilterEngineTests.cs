using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class TopItemsFilterEngineTests
{
    private static (Worksheet Sheet, CellRange Range) Numbers()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Numeri");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Valore"));
        for (var row = 1; row <= 10; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        return (sheet, new CellRange(new CellAddress(0, 0), new CellAddress(10, 0)));
    }

    [Fact]
    public void IPrimiQuattroValoriRestanoVisibili()
    {
        var (sheet, range) = Numbers();
        var hidden = new SheetFilterService().RowsToHide(sheet, range, [new ColumnFilter(0) { Top = new TopItemsCriterion(false, false, 4) }]);

        Assert.Equal([1, 2, 3, 4, 5, 6], hidden.Order());
    }

    [Fact]
    public void GliUltimiVentiPerCentoRestanoVisibili()
    {
        var (sheet, range) = Numbers();
        var hidden = new SheetFilterService().RowsToHide(sheet, range, [new ColumnFilter(0) { Top = new TopItemsCriterion(true, true, 20) }]);

        Assert.Equal([3, 4, 5, 6, 7, 8, 9, 10], hidden.Order());
    }
}
