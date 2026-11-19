using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round269TableAutoFitTests
{
    private static TableItem Item()
    {
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("a", "a")])), new TableCell(new BlockContainer([new ParagraphBlock("b", "b")]))]);
        var table = new TableBlock("t", [row], [new Twip(1000), new Twip(1000)]) { AutoWidth = true };
        return new TableItem(table, [50, 50], [], new TableStyleValues(0, 0, 0, null, null, null, null))
        {
            AutoMinimum = [20, 40],
            AutoMaximum = [100, 300],
        };
    }

    [Fact]
    public void Content_that_fits_takes_its_unwrapped_width()
    {
        var item = Item();

        Assert.Equal(400, item.Width(500), 6);
        Assert.Equal(100, item.ColumnLeft(1, 500), 6);
    }

    [Fact]
    public void Content_too_wide_shares_the_room_above_the_minimum_in_proportion()
    {
        var item = Item();

        Assert.Equal(230, item.Width(230), 6);
        Assert.Equal(20 + 80 * 0.5, item.ColumnWidth(0, 1, 230), 6);
        Assert.Equal(40 + 260 * 0.5, item.ColumnWidth(1, 1, 230), 6);
    }

    [Fact]
    public void Content_wider_than_its_minimum_room_keeps_the_minimum()
    {
        Assert.Equal(60, Item().Width(30), 6);
    }
}
