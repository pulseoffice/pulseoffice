using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round250BrokenParagraphAlignmentTests
{
    [Fact]
    public void A_bottom_aligned_object_of_a_paragraph_broken_across_pages_sits_on_its_last_line_on_the_page()
    {
        var f = new LayoutFixture(width: 300, height: 200);
        var block = f.Add(string.Join(' ', Enumerable.Repeat("word", 400)));
        var content = new BlockContainer([new ParagraphBlock("fp", "box")]);
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(content), new DocumentSize(Twip.FromPoints(120), Twip.FromPoints(30)))
        {
            Wrap = WrapMode.Parallel,
            Placement = new ObjectPlacement(Twip.Zero, Twip.Zero) { VerticalAlign = ObjectAlignment.End },
        });

        var pages = f.Layout().Pages;
        var page = pages[0];

        Assert.True(pages.Count > 1);
        var bottom = page.Lines.Where(line => line.ParagraphId == block.Id).Max(line => line.Top + line.Line.Height);
        var frame = page.Objects.Single(item => item.ObjectId == "frame");
        Assert.InRange(frame.Bounds.Bottom, bottom - 2, bottom + 2);
    }
}
