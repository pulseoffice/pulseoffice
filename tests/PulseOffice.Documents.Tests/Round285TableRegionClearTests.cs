using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round285TableRegionClearTests
{
    private readonly TableAppearance _appearance = new(new StyleResolver());

    private static readonly BorderLine Rule = new(1.5, 0xFF000009);

    private static readonly BorderLine None = new(0, 0xFF000009);

    private static TableBlock Grid() =>
        new("t", [.. Enumerable.Range(0, 2).Select(r => new TableRow([new TableCell(new BlockContainer([new ParagraphBlock($"p{r}", "x")]))]))],
            [new Twip(500)])
        { HeaderRows = 1, Banding = TableBanding.Standard };

    [Fact]
    public void A_zero_width_region_line_and_cleared_font_override_the_parent_style()
    {
        var parent = new TableProperties { HeaderRow = new TableRegionFormat { Font = "Serif", FontSize = 14, Borders = new BoxBorders { Bottom = Rule } } };
        var derived = new TableProperties
        {
            HeaderRow = new TableRegionFormat { Font = string.Empty, FontSize = 0, Borders = new BoxBorders { Bottom = None } },
        }.Over(parent);

        var header = _appearance.Cell(Grid(), derived, 0, 0);

        Assert.Null(header.Font);
        Assert.Null(header.FontSize);
        Assert.Null(header.Borders.Bottom);
    }

    [Fact]
    public void A_zero_width_region_line_also_removes_the_table_line_on_that_side()
    {
        var style = new TableProperties { BorderWidth = 0.5, HeaderRow = new TableRegionFormat { Borders = new BoxBorders { Bottom = None } } };

        var header = _appearance.Cell(Grid(), style, 0, 0);

        Assert.Null(header.Borders.Bottom);
        Assert.NotNull(header.Borders.Top);
    }
}
