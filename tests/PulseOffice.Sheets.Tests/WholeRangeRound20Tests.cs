using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class WholeRangeRound20Tests
{
    private static readonly CellRange WholeFirstColumn = new(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 0));

    [Fact]
    public void Shuffling_a_whole_column_moves_only_the_used_rows()
    {
        var sheet = new Worksheet("S");
        for (var row = 2; row < 7; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));

        var plan = new SheetRandomFill(new SelectionWorkArea()).Shuffle(sheet, WholeFirstColumn, 5);

        Assert.Equal(5, plan.Count);
        Assert.All(plan, entry => Assert.InRange(entry.Address.Row, 2, 6));
        Assert.Equal([2.0, 3, 4, 5, 6], plan.Select(entry => entry.Value.AsNumber()).Order());
    }

    [Fact]
    public void Shuffling_a_whole_column_of_an_empty_sheet_plans_nothing()
    {
        Assert.Empty(new SheetRandomFill(new SelectionWorkArea()).Shuffle(new Worksheet("S"), WholeFirstColumn, 5));
    }
}
