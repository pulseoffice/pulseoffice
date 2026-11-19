using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round242RotatedCharacterHitTests
{
    [Fact]
    public void A_point_over_a_glyph_of_a_rotated_text_frame_hits_its_character()
    {
        var f = new LayoutFixture(width: 400, height: 400, measurer: new ProportionalMeasurer());
        var block = f.Add("x");
        var content = new BlockContainer([new ParagraphBlock("fp", "WWWW")]);
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(content), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(60)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(100)),
            Wrap = WrapMode.Through,
            RotationDegrees = 90,
        });
        var layout = f.Layout();
        var frame = Assert.Single(layout.Pages[0].TurnedFrames);
        var line = Assert.Single(frame.Lines);
        var advance = line.Line.Runs.First(run => run.SourceLength > 0).Advances[0];
        var (cx, cy) = (frame.Bounds.X + frame.Bounds.Width / 2, frame.Bounds.Y + frame.Bounds.Height / 2);
        var (dx, dy) = (line.Left + advance * 1.5 - cx, line.Top + line.Line.Height / 2 - cy);

        var hit = f.Queries.CharacterAt(layout, 0, cx - dy, cy + dx);

        Assert.Equal("fp", hit?.ParagraphId);
        Assert.Equal(1, hit?.Offset);
    }
}
