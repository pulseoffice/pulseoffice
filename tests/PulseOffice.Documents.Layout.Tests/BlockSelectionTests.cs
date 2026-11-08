using PulseOffice.Documents.Layout.Measurement;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ProportionalMeasurer : ITextMeasurer
{
    public FontMetrics Metrics(FontKey font) => new(font.Size * 0.8, font.Size * 0.2, 0);

    public IReadOnlyList<double> Advances(FontKey font, string text) =>
        [.. text.Select(character => (character == 'i' ? 0.25 : 1.0) * font.Size)];
}

public sealed class BlockSelectionTests
{
    [Fact]
    public void Block_keeps_the_same_horizontal_band_on_lines_with_different_glyph_widths()
    {
        var fixture = new LayoutFixture(width: 400, height: 200, measurer: new ProportionalMeasurer());
        var narrow = fixture.Add("iiiiiiiiii");
        var wide = fixture.Add("WWWWWWWWWW");
        var layout = fixture.Layout();
        var lines = fixture.Lines(layout);
        var advance = lines[1].Line.Runs.First(run => run.SourceLength > 0).Advances[0];
        var thin = advance / 4;

        var ranges = fixture.Queries.Block(layout, 0, thin * 2.5, 0, advance * 1.5, 200);

        Assert.Equal(2, ranges.Count);
        Assert.Equal(narrow.Id, ranges[0].Start.ParagraphId);
        Assert.Equal(2, ranges[0].Start.Offset);
        Assert.Equal(6, ranges[0].End.Offset);
        Assert.Equal(wide.Id, ranges[1].Start.ParagraphId);
        Assert.Equal(0, ranges[1].Start.Offset);
        Assert.Equal(2, ranges[1].End.Offset);
    }

    [Fact]
    public void Block_skips_lines_that_end_before_the_band()
    {
        var fixture = new LayoutFixture(width: 400, height: 200, measurer: new ProportionalMeasurer());
        fixture.Add("ii");
        var wide = fixture.Add("WWWWWW");
        var layout = fixture.Layout();
        var advance = fixture.Lines(layout)[1].Line.Runs.First(run => run.SourceLength > 0).Advances[0];

        var ranges = fixture.Queries.Block(layout, 0, advance * 2.2, 0, advance * 3.1, 200);

        var only = Assert.Single(ranges);
        Assert.Equal(wide.Id, only.Start.ParagraphId);
        Assert.Equal(2, only.Start.Offset);
        Assert.Equal(4, only.End.Offset);
    }
}
