using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round263CellBorderConflictTests
{
    private readonly CellBorderConflicts _conflicts = new();

    [Fact]
    public void The_heavier_line_wins_whatever_the_order()
    {
        var thin = new BorderLine(0.5, 0xFF000000);
        var thick = new BorderLine(2, 0xFFFF0000);

        Assert.Same(thick, _conflicts.Winner(thin, thick));
        Assert.Same(thick, _conflicts.Winner(thick, thin));
    }

    [Fact]
    public void A_double_line_outweighs_a_single_line_of_the_same_width()
    {
        var single = new BorderLine(1, 0xFF000000);
        var twice = new BorderLine(1, 0xFF0000FF) { Style = BorderLineStyle.Double };

        Assert.Same(twice, _conflicts.Winner(single, twice));
    }

    [Fact]
    public void Between_equal_lines_the_darker_colour_wins()
    {
        var light = new BorderLine(1, 0xFFCCCCCC);
        var dark = new BorderLine(1, 0xFF202020);

        Assert.Same(dark, _conflicts.Winner(light, dark));
        Assert.Same(dark, _conflicts.Winner(dark, light));
    }

    [Fact]
    public void Adjacent_cells_share_the_winning_edge()
    {
        TableCell Cell(BoxBorders borders) => new(new BlockContainer([new ParagraphBlock("p", "")])) { Borders = borders };
        var heavy = new BorderLine(3, 0xFF000000);
        var light = new BorderLine(0.5, 0xFF888888);
        var table = new TableBlock("t", [new TableRow([Cell(new BoxBorders { Right = light }), Cell(new BoxBorders { Left = heavy })])], [new Twip(1000), new Twip(1000)]);

        var looks = new TableAppearance(new StyleResolver()).Cells(table, TableProperties.None);

        Assert.Equal(heavy, looks[0][0]!.Borders.Right);
        Assert.Equal(heavy, looks[0][1]!.Borders.Left);
    }
}
