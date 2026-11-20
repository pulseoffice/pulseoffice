using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round279TableRegionTextTests
{
    private readonly TableAppearance _appearance = new(new StyleResolver());

    private static readonly BorderLine Rule = new(1.5, 0xFF000009);

    private static readonly TableProperties Style = new()
    {
        BorderWidth = 0.5,
        BorderColor = 0xFF000000,
        BandBackground = 0xFF000002,
        HeaderRow = new TableRegionFormat { Italic = true, Font = "Serif", Borders = new BoxBorders { Bottom = Rule } },
        SecondBand = new TableRegionFormat { FontSize = 9 },
        TotalRow = new TableRegionFormat { Borders = new BoxBorders { Top = Rule } },
    };

    private static TableBlock Grid(TableBanding banding) =>
        new("t", [.. Enumerable.Range(0, 4).Select(r => new TableRow([.. Enumerable.Range(0, 2).Select(c => new TableCell(new BlockContainer([new ParagraphBlock($"p{r}{c}", "x")])))]))],
            [new Twip(500), new Twip(500)])
        { HeaderRows = 1, Banding = banding };

    [Fact]
    public void Header_and_band_formats_carry_italics_fonts_sizes_and_lines()
    {
        var table = Grid(TableBanding.Standard with { LastRow = true });

        var header = _appearance.Cell(table, Style, 0, 0);
        Assert.True(header.Italic);
        Assert.Equal("Serif", header.Font);
        Assert.Equal(Rule, header.Borders.Bottom);
        Assert.Equal(9, _appearance.Cell(table, Style, 2, 0).FontSize);
        Assert.Null(_appearance.Cell(table, Style, 1, 0).FontSize);
        Assert.Equal(Rule, _appearance.Cell(table, Style, 3, 1).Borders.Top);
        Assert.NotEqual(Rule, _appearance.Cell(table, Style, 1, 1).Borders.Top);
    }

    [Fact]
    public void Region_text_formats_merge_field_by_field_over_the_parent()
    {
        var parent = new TableProperties { HeaderRow = new TableRegionFormat { Italic = true, Borders = new BoxBorders { Top = Rule } } };
        var merged = new TableProperties { HeaderRow = new TableRegionFormat { Font = "Mono", Borders = new BoxBorders { Bottom = Rule } } }.Over(parent);

        Assert.Equal(new TableRegionFormat { Italic = true, Font = "Mono", Borders = new BoxBorders { Top = Rule, Bottom = Rule } }, merged.HeaderRow);
    }
}
