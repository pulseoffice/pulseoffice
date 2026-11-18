using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round219FrameNoteTests
{
    [Fact]
    public void A_footnote_in_a_frame_is_numbered_and_placed_on_the_page()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var block = f.Add("body");
        var inner = new ParagraphBlock("fp", "frame");
        inner.Marks = [new NoteMark(inner.Length, "n1")];
        f.Document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "note text")])));
        f.Document.Objects.Add(new AnchoredObject("frame", new ParagraphAnchor(block.Id), new TextBoxContent(new BlockContainer([inner])), new DocumentSize(Twip.FromPoints(80), Twip.FromPoints(20)))
        {
            Placement = new ObjectPlacement(Twip.FromPoints(10), Twip.Zero),
            Wrap = WrapMode.Through,
        });

        var page = f.Layout().Pages[0];

        Assert.NotNull(page.Footnotes);
        Assert.Contains(page.Footnotes.Lines, line => line.ParagraphId == "np");
    }
}
