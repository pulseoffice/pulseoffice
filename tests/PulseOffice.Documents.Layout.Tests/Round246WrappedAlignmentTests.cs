using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round246WrappedAlignmentTests
{
    [Fact]
    public void A_bottom_aligned_object_sits_at_the_bottom_of_the_wrapped_paragraph()
    {
        var f = new LayoutFixture(width: 300, height: 600);
        var block = f.Add(string.Join(' ', Enumerable.Repeat("word", 60)));
        var content = new BlockContainer([new ParagraphBlock("fp", "box")]);
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(content), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(30)))
        {
            Wrap = WrapMode.Parallel,
            Placement = new PulseOffice.Documents.Objects.ObjectPlacement(Twip.Zero, Twip.Zero) { VerticalAlign = ObjectAlignment.End },
        });

        var page = f.Layout().Pages[0];

        var lines = page.Lines.Where(line => line.ParagraphId == block.Id).ToList();
        var bottom = lines.Max(line => line.Top + line.Line.Height);
        var frame = page.Objects.Single(item => item.ObjectId == "frame");
        Assert.InRange(frame.Bounds.Bottom, bottom - 2, bottom + 2);
    }
}
