using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round271CellInheritedEdgesTests
{
    private static readonly BorderLine Inside = new(0.5, 0xFF000000);

    private static TableCell Cell(BoxBorders borders) => new(new BlockContainer([new ParagraphBlock("p", "")])) { Borders = borders };

    [Fact]
    public void A_cell_nil_side_suppresses_the_table_line_its_neighbour_inherited()
    {
        var nil = Cell(new BoxBorders { Left = Inside });
        var neighbour = Cell(new BoxBorders { Left = Inside, Right = Inside });
        var table = new TableBlock("t", [new TableRow([nil, neighbour])], [new Twip(1000), new Twip(1000)]);

        new CellInheritedEdges().SuppressInherited(table, (cell, side) => ReferenceEquals(cell, nil) && side == BorderSide.Right);

        Assert.Null(neighbour.Borders!.Left);
        Assert.Equal(Inside, neighbour.Borders.Right);
    }

    [Fact]
    public void An_explicit_line_on_the_neighbour_is_kept()
    {
        var nil = Cell(new BoxBorders());
        var neighbour = Cell(new BoxBorders { Left = Inside });
        var table = new TableBlock("t", [new TableRow([nil, neighbour])], [new Twip(1000), new Twip(1000)]);

        new CellInheritedEdges().SuppressInherited(table, (_, _) => true);

        Assert.Equal(Inside, neighbour.Borders!.Left);
    }
}
