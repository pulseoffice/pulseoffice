using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round275TableRegionAppearanceTests
{
    private readonly TableAppearance _appearance = new(new StyleResolver());

    private static TableBlock Grid(TableBanding banding) =>
        new("t", [.. Enumerable.Range(0, 3).Select(r => new TableRow([.. Enumerable.Range(0, 3).Select(c => new TableCell(new BlockContainer([new ParagraphBlock($"p{r}{c}", "x")])))]))],
            [new Twip(500), new Twip(500), new Twip(500)])
        { HeaderRows = 1, Banding = banding };

    private static readonly TableProperties Style = new()
    {
        HeaderBackground = 0xFF000001,
        BandBackground = 0xFF000002,
        TotalRow = new TableRegionFormat { Background = 0xFF000003, Bold = true },
        FirstColumn = new TableRegionFormat { Background = 0xFF000004, Ink = 0xFF000005 },
        LastColumn = new TableRegionFormat { Background = TableRegionFormat.NoColour },
        SouthWestCell = new TableRegionFormat { Background = 0xFF000006 },
    };

    [Fact]
    public void Regions_layer_columns_under_rows_and_corners_over_both()
    {
        var table = Grid(TableBanding.Standard with { FirstColumn = true, LastColumn = true, LastRow = true });

        Assert.Equal(0xFF000001u, _appearance.Cell(table, Style, 0, 0).Background);
        Assert.Equal(0xFF000005u, _appearance.Cell(table, Style, 0, 0).Ink);
        Assert.Equal(0xFF000004u, _appearance.Cell(table, Style, 1, 0).Background);
        Assert.Null(_appearance.Cell(table, Style, 1, 2).Background);
        Assert.Equal(0xFF000003u, _appearance.Cell(table, Style, 2, 1).Background);
        Assert.True(_appearance.Cell(table, Style, 2, 1).Bold);
        Assert.Equal(0xFF000006u, _appearance.Cell(table, Style, 2, 0).Background);
    }

    [Fact]
    public void Regions_apply_only_when_the_table_turns_them_on()
    {
        var table = Grid(TableBanding.Standard);

        Assert.Null(_appearance.Cell(table, Style, 1, 0).Background);
        Assert.Equal(0xFF000002u, _appearance.Cell(table, Style, 2, 0).Background);
        Assert.False(_appearance.Cell(table, Style, 2, 1).Bold);
    }

    [Fact]
    public void A_derived_style_clears_an_inherited_colour_to_none()
    {
        var parent = new TableProperties { Background = 0xFF00FF00, HeaderColor = 0xFFFF0000 };
        var derived = new TableProperties { Background = TableRegionFormat.NoColour, HeaderColor = TableRegionFormat.NoColour }.Over(parent);
        var table = Grid(TableBanding.None);

        Assert.Null(_appearance.Cell(table, derived, 1, 1).Background);
        Assert.Null(_appearance.Cell(table, derived, 0, 1).Ink);
    }
}
