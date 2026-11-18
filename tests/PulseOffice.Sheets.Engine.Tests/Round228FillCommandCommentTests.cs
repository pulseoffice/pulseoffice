using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round228FillCommandCommentTests
{
    [Fact]
    public void Fill_right_from_the_first_column_of_the_selection_copies_its_comment()
    {
        var sheet = new Worksheet("Sheet1");
        var note = new CellComment { Text = "seed" };
        sheet.Comments[new CellAddress(0, 0)] = note;
        var selection = new CellRange(new CellAddress(0, 0), new CellAddress(0, 2));
        var source = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0));

        var comments = new SheetFill().Comments(sheet, source, selection, FillDirection.Right);

        (CellAddress Address, CellComment? Comment)[] expected = [(new CellAddress(0, 1), note), (new CellAddress(0, 2), note)];
        Assert.Equal(expected, comments);
    }

    [Fact]
    public void Fill_up_from_the_last_row_clears_comments_without_a_seed()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "old" };
        var selection = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0));
        var source = new CellRange(new CellAddress(2, 0), new CellAddress(2, 0));

        var comments = new SheetFill().Comments(sheet, source, selection, FillDirection.Up);

        Assert.Contains((new CellAddress(0, 0), (CellComment?)null), comments);
    }
}
