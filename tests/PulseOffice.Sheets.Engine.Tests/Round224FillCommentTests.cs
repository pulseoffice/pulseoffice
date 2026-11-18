using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round224FillCommentTests
{
    [Fact]
    public void The_fill_handle_repeats_the_seed_comments_and_clears_the_others()
    {
        var sheet = new Worksheet("Sheet1");
        var note = new CellComment { Text = "seed" };
        sheet.Comments[new CellAddress(0, 0)] = note;
        sheet.Comments[new CellAddress(3, 0)] = new CellComment { Text = "old" };
        var source = new CellRange(new CellAddress(0, 0), new CellAddress(1, 0));
        var target = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0));

        var comments = new SheetFill().Comments(sheet, source, target, FillDirection.Down);

        (CellAddress Address, CellComment? Comment)[] expected =
            [(new CellAddress(2, 0), note), (new CellAddress(3, 0), null), (new CellAddress(4, 0), note)];
        Assert.Equal(expected, comments);
    }
}
