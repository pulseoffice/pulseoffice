using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round263TablePercentWidthTests
{
    private static TableItem Item(double? percent)
    {
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("a", "a")])), new TableCell(new BlockContainer([new ParagraphBlock("b", "b")]))]);
        var table = new TableBlock("t", [row], [new Twip(1000), new Twip(3000)]) { WidthPercent = percent };
        return new TableItem(table, [50, 150], [], new TableStyleValues(0, 0, 0, null, null, null, null));
    }

    [Fact]
    public void A_percent_table_takes_its_share_of_the_text_width_in_proportion()
    {
        var item = Item(100);

        Assert.Equal(400, item.Width(400), 6);
        Assert.Equal(100, item.ColumnWidth(0, 1, 400), 6);
        Assert.Equal(100, item.ColumnLeft(1, 400), 6);
        Assert.Equal(300, item.ColumnWidth(1, 1, 400), 6);
    }

    [Fact]
    public void A_table_without_percent_keeps_its_columns()
    {
        Assert.Equal(200, Item(null).Width(1000), 6);
    }
}
