using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round117FilterShiftTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Inserting_cells_down_across_the_whole_filter_band_moves_the_autofilter_and_the_database_range()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R("B3:C6"));
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = ">1" });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Db", SheetIndex = 0, Range = R("E3:F6"), Filters = [new ColumnFilter(0) { Criteria = "x" }] });
        var shift = new SheetCellShift(new SheetStructure());

        Assert.True(shift.KeepsBlocksWhole(workbook, 0, R("B1:F2"), CellShift.Down, insert: true));
        shift.Insert(engine, 0, R("B1:F2"), CellShift.Down);

        Assert.Equal(R("B5:C8"), sheet.AutoFilter.Range);
        Assert.Equal(">1", Assert.Single(sheet.AutoFilter.Filters).Criteria);
        Assert.Equal(R("E5:F8"), workbook.DatabaseRanges.Find("Db")!.Range);
    }

    [Fact]
    public void Deleting_cells_left_across_the_filter_band_moves_the_filter_and_its_columns()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(R("C1:E4"));
        sheet.AutoFilter.Set(new ColumnFilter(2) { Criteria = "<5" });
        var shift = new SheetCellShift(new SheetStructure());

        shift.Delete(engine, 0, R("A1:A4"), CellShift.Right);

        Assert.Equal(R("B1:D4"), sheet.AutoFilter.Range);
        Assert.Equal(2, Assert.Single(sheet.AutoFilter.Filters).ColumnOffset);
    }

    [Fact]
    public void A_partial_shift_that_would_split_a_filter_area_is_refused()
    {
        var (workbook, _) = fixture.CreateWorkbook();
        workbook.Sheets[0].AutoFilter.Activate(R("B3:C6"));

        Assert.False(new SheetCellShift(new SheetStructure()).KeepsBlocksWhole(workbook, 0, R("B1:B2"), CellShift.Down, insert: true));
    }
}
