using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round269CellBorderSpanTests
{
    private static TableCell Cell(BoxBorders borders) => new(new BlockContainer([new ParagraphBlock("p", "")])) { Borders = borders };

    [Fact]
    public void A_cell_spanning_two_rows_resolves_its_edge_with_the_neighbour_of_the_second_row()
    {
        var heavy = new BorderLine(3, 0xFF000000);
        var light = new BorderLine(0.5, 0xFF888888);
        var merged = Cell(new BoxBorders { Right = heavy });
        merged.RowSpan = 2;
        var covered = Cell(new BoxBorders());
        covered.Covered = true;
        var table = new TableBlock("t",
            [new TableRow([merged, Cell(new BoxBorders { Left = light })]), new TableRow([covered, Cell(new BoxBorders { Left = light })])],
            [new Twip(1000), new Twip(1000)]);

        var looks = new TableAppearance(new StyleResolver()).Cells(table, TableProperties.None);

        Assert.Equal(heavy, looks[0][1]!.Borders.Left);
        Assert.Equal(heavy, looks[1][1]!.Borders.Left);
        Assert.Equal(heavy, looks[0][0]!.Borders.Right);
    }

    [Fact]
    public void A_cell_spanning_two_columns_resolves_its_bottom_with_both_cells_below()
    {
        var heavy = new BorderLine(3, 0xFF000000);
        var light = new BorderLine(0.5, 0xFF888888);
        var wide = Cell(new BoxBorders { Bottom = heavy });
        wide.ColumnSpan = 2;
        var covered = Cell(new BoxBorders());
        covered.Covered = true;
        var table = new TableBlock("t",
            [new TableRow([wide, covered]), new TableRow([Cell(new BoxBorders { Top = light }), Cell(new BoxBorders { Top = light })])],
            [new Twip(1000), new Twip(1000)]);

        var looks = new TableAppearance(new StyleResolver()).Cells(table, TableProperties.None);

        Assert.Equal(heavy, looks[1][0]!.Borders.Top);
        Assert.Equal(heavy, looks[1][1]!.Borders.Top);
    }
}
