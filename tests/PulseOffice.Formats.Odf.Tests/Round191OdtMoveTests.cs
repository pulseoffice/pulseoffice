using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round191OdtMoveTests
{
    [Fact]
    public void A_tracked_move_keeps_its_link_through_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Changes.Add(new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("moved") { Runs = [new TextRun("moved")] })])) { MoveId = "m1" });
        document.Changes.Add(new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = "m1" });
        document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays") { Runs = [new TextRun("stays")] }) { Marks = [new ChangeMark(0, "from", MarkEdge.Point)] },
            new ParagraphBlock("p2", new TextParagraph("end moved") { Runs = [new TextRun("end moved")] }) { Marks = [new ChangeMark(4, "to", MarkEdge.Start), new ChangeMark(9, "to", MarkEdge.End)] },
        ]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        Assert.Equal("m1", read.Changes.All.OfType<Deletion>().Single().MoveId);
        Assert.Equal("m1", read.Changes.All.OfType<Insertion>().Single().MoveId);
    }
}
