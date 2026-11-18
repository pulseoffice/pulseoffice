using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round196OdtMovedParagraphMarkTests
{
    [Fact]
    public void A_moved_paragraph_break_keeps_its_move_through_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var removed = new BlockContainer(
        [
            new ParagraphBlock("r1", new TextParagraph("moved") { Runs = [new TextRun("moved")] }),
            new ParagraphBlock("r2", new TextParagraph(string.Empty)),
        ]);
        document.Changes.Add(new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, removed) { MoveId = "m1" });
        document.Changes.Add(new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = "m1" });
        document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays") { Runs = [new TextRun("stays")] }) { Marks = [new ChangeMark(0, "from", MarkEdge.Point)] },
            new ParagraphBlock("p2", new TextParagraph("moved") { Runs = [new TextRun("moved")] }) { Marks = [new ChangeMark(0, "to", MarkEdge.Start)] },
            new ParagraphBlock("p3", new TextParagraph("end") { Runs = [new TextRun("end")] }) { Marks = [new ChangeMark(0, "to", MarkEdge.End)] },
        ]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        var deletion = read.Changes.All.OfType<Deletion>().Single();
        Assert.Equal("m1", deletion.MoveId);
        Assert.Equal(2, deletion.Removed.Blocks.Count);
        Assert.All(read.Changes.All.OfType<Insertion>(), insertion => Assert.Equal("m1", insertion.MoveId));
    }
}
