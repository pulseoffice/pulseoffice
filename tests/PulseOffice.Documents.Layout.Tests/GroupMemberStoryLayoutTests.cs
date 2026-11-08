using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class GroupMemberStoryLayoutTests
{
    [Fact]
    public void LaStoriaDiUnMembroDelGruppoSiImpaginaDentroIlRiquadroDelMembro()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Add("testo");
        var top = new ShapeTextStyle { Vertical = TextVerticalPlacement.Top, Inset = Twip.Zero };
        var group = new ShapeGroupObject
        {
            Id = "g",
            Children =
            [
                new GroupChild(new ShapeObject { Id = "left", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = top } }, 0, 0, 0.5, 1),
                new GroupChild(new ShapeObject { Id = "right", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Text = top } }, 0.5, 0.5, 0.5, 0.5),
            ],
        };
        var stories = new Dictionary<string, BlockContainer>
        {
            ["right"] = new([new ParagraphBlock("rp", new TextParagraph("MAIUSCOLO") { Runs = [new TextRun("maiuscolo") { SmallCaps = true }] })]),
        };
        f.Document.Objects.Add(new AnchoredObject("o", new PageAnchor(1), new EmbeddedContent(group, null, stories), new DocumentSize(Twip.FromPoints(200), Twip.FromPoints(100)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(50)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page },
            Wrap = WrapMode.Parallel,
        });

        var box = f.Layout().Pages[0].Objects.Single();
        var line = Assert.Single(box.Lines);
        Assert.InRange(line.Left - box.Bounds.X, 99, 101);
        Assert.InRange(line.Top - box.Bounds.Y, 49, 51);
    }
}
