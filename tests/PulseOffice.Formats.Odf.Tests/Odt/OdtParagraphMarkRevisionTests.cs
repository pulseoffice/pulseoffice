using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtParagraphMarkRevisionTests
{
    [Fact]
    public void A_deletion_that_joined_two_paragraphs_crosses_ODT_with_both_paragraphs()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var date = new DateTimeOffset(2024, 1, 2, 3, 4, 0, TimeSpan.Zero);
        document.Changes.Add(new Deletion("change1", "Ada", date, new BlockContainer(
        [
            new ParagraphBlock("r1", new TextParagraph("cd") { Align = TextAlign.Center, Explicit = ParagraphOverrides.Align }),
            new ParagraphBlock("r2", new TextParagraph("ef")),
        ])));
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("abgh") { Align = TextAlign.Center, Explicit = ParagraphOverrides.Align }) { Marks = [new ChangeMark(2, "change1", MarkEdge.Point)] }]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        var copy = read.Value!;
        var paragraph = Assert.Single(copy.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal("abgh", paragraph.Text);
        var mark = Assert.IsType<ChangeMark>(Assert.Single(paragraph.Marks));
        Assert.Equal(2, mark.Offset);
        var deletion = Assert.IsType<Deletion>(Assert.Single(copy.Changes.All));
        Assert.Equal(["cd", "ef"], deletion.Removed.Blocks.OfType<ParagraphBlock>().Select(item => item.Text));
        Assert.Equal(TextAlign.Center, ((ParagraphBlock)deletion.Removed[0]).Content.Align);
    }

    [Fact]
    public void Tracked_table_rows_cross_ODT_with_their_change()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var date = new DateTimeOffset(2024, 1, 2, 3, 4, 0, TimeSpan.Zero);
        document.Changes.Add(new Insertion("row1", "Ada", date));
        document.Changes.Add(new Deletion("row2", "Bea", date, new BlockContainer()));
        TableRow Row(string text, string? change) => new([new TableCell(new BlockContainer([new ParagraphBlock(text, text)]))]) { ChangeId = change };
        document.Body.Replace([
            new TableBlock("t1", [Row("kept", null), Row("new", "row1"), Row("old", "row2")], [PulseOffice.Foundation.Units.Twip.FromCentimeters(3)]),
            new ParagraphBlock("p1", string.Empty)]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var copy = new OdtReader().Read(stream).Value!;

        var table = Assert.Single(copy.Body.Blocks.OfType<TableBlock>());
        Assert.Null(table.Rows[0].ChangeId);
        Assert.Equal("Ada", Assert.IsType<Insertion>(copy.Changes.Find(table.Rows[1].ChangeId!)).Author);
        Assert.Equal("Bea", Assert.IsType<Deletion>(copy.Changes.Find(table.Rows[2].ChangeId!)).Author);
    }

    [Fact]
    public void A_frame_and_a_comment_held_by_a_pending_deletion_cross_ODT()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Comments.Add(new Comment("c9", "Bea", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("pc9", "vedi")])));
        document.Changes.Add(new Deletion("ct9", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer(
        [
            new ParagraphBlock("pd8", string.Empty),
            new ParagraphBlock("pd9", "due") { Marks = [new CommentMark(0, "c9", MarkEdge.Start), new CommentMark(2, "c9", MarkEdge.End)] },
        ])));
        document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("frame9", new PulseOffice.Documents.Objects.ParagraphAnchor("pd9"),
            new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer([new ParagraphBlock("pf9", "riquadro")])),
            new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2))));
        document.Body.Replace([new ParagraphBlock("p1", "unotre") { Marks = [new ChangeMark(3, "ct9", MarkEdge.Point)] }]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var copy = new OdtReader().Read(stream).Value!;

        var deletion = Assert.IsType<Deletion>(Assert.Single(copy.Changes.All));
        var removed = deletion.Removed.Blocks.OfType<ParagraphBlock>().ToList();
        var comment = Assert.Single(copy.Comments.All);
        var edges = removed.SelectMany(item => item.Marks).OfType<CommentMark>().Where(mark => mark.CommentId == comment.Id).ToList();
        Assert.Equal(0, Assert.Single(edges, mark => mark.Edge == MarkEdge.Start).Offset);
        Assert.Equal(2, Assert.Single(edges, mark => mark.Edge == MarkEdge.End).Offset);
        var frame = Assert.Single(copy.Objects.All);
        Assert.Equal(removed[1].Id, frame.Anchor.Accept(new PulseOffice.Documents.Objects.AnchorParagraph()));
        Assert.Equal("unotre", Assert.Single(copy.Body.Blocks.OfType<ParagraphBlock>()).Text);
    }
}
