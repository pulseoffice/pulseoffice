using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round206AutoFilterSortTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void A_left_to_right_autofilter_sort_follows_inserted_rows_and_ignores_inserted_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 3; column++)
                engine.SetValue(new SheetCell(0, new CellAddress(row, column)), CellValue.Number(row + column));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 2)));
        sheet.AutoFilter.Remember(new SortSettings { Fields = [new SortField(2)], Orientation = SortOrientation.LeftToRight });

        new SheetStructure().InsertRows(engine, 0, 1, 1);
        Assert.Equal(3, sheet.AutoFilter.Sort!.Fields[0].ColumnOffset);

        new SheetStructure().InsertColumns(engine, 0, 1, 1);
        Assert.Equal(3, sheet.AutoFilter.Sort!.Fields[0].ColumnOffset);
    }
}
