using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round191DocxMoveRoundTripTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_tracked_move_is_written_as_word_move_ranges_and_read_back_linked()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Changes.Add(new Deletion("from", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("moved") { Runs = [new TextRun("moved")] })])) { MoveId = "m1" });
        document.Changes.Add(new Insertion("to", "Ada", DateTimeOffset.UnixEpoch) { MoveId = "m1" });
        document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("stays") { Runs = [new TextRun("stays")] }) { Marks = [new ChangeMark(0, "from", MarkEdge.Point)] },
            new ParagraphBlock("p2", new TextParagraph("end moved") { Runs = [new TextRun("end moved")] }) { Marks = [new ChangeMark(4, "to", MarkEdge.Start), new ChangeMark(9, "to", MarkEdge.End)] },
        ]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var body = package.MainDocumentPart!.Document!.Body!;
            Assert.Single(body.Descendants<W.MoveFromRun>());
            Assert.Single(body.Descendants<W.MoveToRun>());
            Assert.Equal(body.Descendants<W.MoveFromRangeStart>().Single().Name!.Value, body.Descendants<W.MoveToRangeStart>().Single().Name!.Value);
            Assert.Empty(body.Descendants<W.InsertedRun>());
            Assert.Empty(body.Descendants<W.DeletedRun>());
        }
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        var deletion = Assert.Single(read.Value!.Changes.All.OfType<Deletion>());
        var insertion = Assert.Single(read.Value.Changes.All.OfType<Insertion>());
        Assert.NotNull(deletion.MoveId);
        Assert.Equal(deletion.MoveId, insertion.MoveId);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX224");
    }
}
