using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round236CharacterHitTests
{
    [Fact]
    public void Only_a_point_over_a_glyph_hits_a_character()
    {
        var fixture = new LayoutFixture(width: 400, height: 200, measurer: new ProportionalMeasurer());
        var block = fixture.Add("WWWW");
        var layout = fixture.Layout();
        var line = fixture.Lines(layout)[0];
        var advance = line.Line.Runs.First(run => run.SourceLength > 0).Advances[0];
        var middle = line.Top + line.Line.Height / 2;

        var inside = fixture.Queries.CharacterAt(layout, 0, line.Left + advance * 2.5, middle);

        Assert.Equal(block.Id, inside?.ParagraphId);
        Assert.Equal(2, inside?.Offset);
        Assert.Null(fixture.Queries.CharacterAt(layout, 0, line.Left + advance * 6, middle));
        Assert.Null(fixture.Queries.CharacterAt(layout, 0, line.Left + advance, line.Top + line.Line.Height + 40));
    }
}
