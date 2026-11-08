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

public sealed class DocxParagraphMarkRevisionTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Read(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX150");
        return read.Value!;
    }

    private MemoryStream Write(TextDocument document, out IReadOnlyList<string> losses)
    {
        var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded);
        losses = [.. written.Diagnostics.Select(diagnostic => diagnostic.Code)];
        stream.Position = 0;
        return stream;
    }

    private const string Joined =
        "<w:p><w:pPr><w:jc w:val=\"center\"/><w:rPr><w:del w:id=\"1\" w:author=\"Ada\" w:date=\"2024-01-02T03:04:00Z\"/></w:rPr></w:pPr>"
        + "<w:r><w:t>ab</w:t></w:r><w:del w:id=\"2\" w:author=\"Ada\" w:date=\"2024-01-02T03:04:00Z\"><w:r><w:delText>cd</w:delText></w:r></w:del></w:p>"
        + "<w:p><w:del w:id=\"3\" w:author=\"Ada\" w:date=\"2024-01-02T03:04:00Z\"><w:r><w:delText>ef</w:delText></w:r></w:del><w:r><w:t>gh</w:t></w:r></w:p>";

    [Fact]
    public void A_deleted_paragraph_mark_joins_the_paragraphs_with_one_deletion_holding_both_sides()
    {
        var document = Read(Joined);

        var paragraph = Assert.Single(document.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal("abgh", paragraph.Text);
        Assert.Equal(TextAlign.Center, paragraph.Content.Align);
        var mark = Assert.IsType<ChangeMark>(Assert.Single(paragraph.Marks));
        Assert.Equal(2, mark.Offset);
        var deletion = Assert.IsType<Deletion>(Assert.Single(document.Changes.All));
        Assert.Equal("Ada", deletion.Author);
        Assert.Equal(["cd", "ef"], deletion.Removed.Blocks.OfType<ParagraphBlock>().Select(item => item.Text));
        Assert.Equal(TextAlign.Center, ((ParagraphBlock)deletion.Removed[0]).Content.Align);
    }

    [Fact]
    public void A_paragraph_mark_deletion_is_written_as_a_deleted_mark_and_read_back()
    {
        var document = Read(Joined);

        using var stream = Write(document, out var losses);

        Assert.DoesNotContain("DOCX001", losses);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToList();
            Assert.Equal(2, paragraphs.Count);
            Assert.NotNull(paragraphs[0].ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<W.Deleted>());
            Assert.Null(paragraphs[1].ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<W.Deleted>());
            Assert.Equal("cd", string.Concat(paragraphs[0].Descendants<W.DeletedText>().Select(text => text.Text)));
            Assert.Equal("ef", string.Concat(paragraphs[1].Descendants<W.DeletedText>().Select(text => text.Text)));
            Assert.Empty(package.MainDocumentPart.Document.Descendants<W.Break>());
        }
        stream.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        Assert.Equal("abgh", Assert.Single(again.Body.Blocks.OfType<ParagraphBlock>()).Text);
        Assert.Equal(["cd", "ef"], Assert.IsType<Deletion>(Assert.Single(again.Changes.All)).Removed.Blocks.OfType<ParagraphBlock>().Select(item => item.Text));
    }

    [Fact]
    public void A_deletion_spanning_three_paragraphs_is_written_as_three_paragraphs_with_two_deleted_marks()
    {
        var document = new TextDocument();
        var deletion = new Deletion("change1", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer(
        [
            new ParagraphBlock("r1", "x"),
            new ParagraphBlock("r2", "y"),
            new ParagraphBlock("r3", "z"),
        ]));
        document.Changes.Add(deletion);
        document.Body.Add(new ParagraphBlock("p1", "AB") { Marks = [new BookmarkMark(2, "end", MarkEdge.Point), new ChangeMark(1, deletion.Id, MarkEdge.Point)] });

        using var stream = Write(document, out var losses);

        Assert.DoesNotContain("DOCX001", losses);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var paragraphs = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToList();
            Assert.Equal(3, paragraphs.Count);
            Assert.Equal([true, true, false], paragraphs.Select(item => item.ParagraphProperties?.ParagraphMarkRunProperties?.GetFirstChild<W.Deleted>() is not null));
            Assert.Equal(["A", "", "B"], paragraphs.Select(item => string.Concat(item.Descendants<W.Text>().Select(text => text.Text))));
            Assert.Equal(["x", "y", "z"], paragraphs.Select(item => string.Concat(item.Descendants<W.DeletedText>().Select(text => text.Text))));
            Assert.NotNull(paragraphs[2].GetFirstChild<W.BookmarkStart>());
        }
        stream.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var paragraph = Assert.Single(again.Body.Blocks.OfType<ParagraphBlock>());
        Assert.Equal("AB", paragraph.Text);
        Assert.Equal(["x", "y", "z"], Assert.IsType<Deletion>(Assert.Single(again.Changes.All)).Removed.Blocks.OfType<ParagraphBlock>().Select(item => item.Text));
    }

    [Fact]
    public void Tracked_table_rows_are_read_as_row_changes_and_written_back()
    {
        var document = Read("<w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid>"
            + "<w:tr><w:tc><w:p><w:r><w:t>kept</w:t></w:r></w:p></w:tc></w:tr>"
            + "<w:tr><w:trPr><w:ins w:id=\"1\" w:author=\"Ada\" w:date=\"2024-01-02T03:04:00Z\"/></w:trPr><w:tc><w:p><w:ins w:id=\"2\" w:author=\"Ada\"><w:r><w:t>new</w:t></w:r></w:ins></w:p></w:tc></w:tr>"
            + "<w:tr><w:trPr><w:del w:id=\"3\" w:author=\"Bea\" w:date=\"2024-01-02T03:05:00Z\"/></w:trPr><w:tc><w:p><w:r><w:t>old</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/>");

        var table = Assert.Single(document.Body.Blocks.OfType<TableBlock>());
        Assert.Equal(3, table.Rows.Count);
        Assert.Null(table.Rows[0].ChangeId);
        var inserted = Assert.IsType<Insertion>(document.Changes.Find(table.Rows[1].ChangeId!));
        Assert.Equal("Ada", inserted.Author);
        var deleted = Assert.IsType<Deletion>(document.Changes.Find(table.Rows[2].ChangeId!));
        Assert.Equal("Bea", deleted.Author);
        Assert.Empty(deleted.Removed.Blocks);

        using var stream = Write(document, out _);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var rows = package.MainDocumentPart!.Document!.Descendants<W.TableRow>().ToList();
            Assert.Null(rows[0].TableRowProperties?.GetFirstChild<W.Inserted>());
            Assert.Equal("Ada", rows[1].TableRowProperties?.GetFirstChild<W.Inserted>()?.Author?.Value);
            Assert.Equal("Bea", rows[2].TableRowProperties?.GetFirstChild<W.Deleted>()?.Author?.Value);
        }
        stream.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(again.Diagnostics, diagnostic => diagnostic.Code == "DOCX121");
        var copy = Assert.Single(again.Value!.Body.Blocks.OfType<TableBlock>());
        Assert.IsType<Insertion>(again.Value.Changes.Find(copy.Rows[1].ChangeId!));
        Assert.IsType<Deletion>(again.Value.Changes.Find(copy.Rows[2].ChangeId!));
    }

    [Fact]
    public void Deleted_marks_at_the_end_of_a_cell_join_what_comes_before_and_the_last_one_has_no_effect()
    {
        const string deleted = "<w:pPr><w:rPr><w:del w:id=\"1\" w:author=\"a\"/></w:rPr></w:pPr>";
        var document = Read("<w:tbl><w:tblGrid><w:gridCol w:w=\"2000\"/></w:tblGrid><w:tr><w:tc>"
            + "<w:p>" + deleted + "<w:r><w:t>one</w:t></w:r></w:p><w:p>" + deleted + "<w:r><w:t>two</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/>");

        var cell = Assert.Single(document.Body.Blocks.OfType<TableBlock>()).Rows[0].Cells[0];
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(cell.Content.Blocks));
        Assert.Equal("onetwo", paragraph.Text);
        Assert.Equal(["", ""], Assert.IsType<Deletion>(Assert.Single(document.Changes.All)).Removed.Blocks.OfType<ParagraphBlock>().Select(item => item.Text));
    }

    [Fact]
    public void A_deleted_mark_before_a_table_stays_declared()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:pPr><w:rPr><w:del w:id=\"1\" w:author=\"a\"/></w:rPr></w:pPr><w:r><w:t>testo</w:t></w:r></w:p>"
                + "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>cella</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p/></w:body></w:document>");
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX150");
        Assert.Equal("testo", Assert.IsType<ParagraphBlock>(read.Value!.Body.Blocks.First()).Text);
    }

    [Fact]
    public void A_footnote_inside_a_pending_deletion_made_by_editing_crosses_docx()
    {
        var document = Read("<w:p><w:r><w:t>abh</w:t></w:r></w:p>");
        var paragraph = (ParagraphBlock)document.Body[0];
        document.Notes.Add(new Note("n9", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("pn9", "nota")])));
        document.Changes.Add(new Deletion("ct9", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("pd9", "cdefg") { Marks = [new NoteMark(2, "n9")] }])));
        paragraph.Marks = [new ChangeMark(2, "ct9", MarkEdge.Point)];

        var read = _composer.Reader(DocxFlavour.Document).Read(Write(document, out _));

        var deletion = Assert.IsType<Deletion>(Assert.Single(read.Value!.Changes.All));
        var note = Assert.Single(deletion.Removed.Blocks.OfType<ParagraphBlock>().SelectMany(item => item.Marks).OfType<NoteMark>());
        Assert.Equal("nota", read.Value.Notes.Find(note.NoteId)?.Content.Blocks.OfType<ParagraphBlock>().Single().Text);
    }

    [Fact]
    public void A_frame_and_a_comment_held_by_a_pending_deletion_cross_docx()
    {
        var document = Read("<w:p><w:r><w:t>unotre</w:t></w:r></w:p>");
        var paragraph = (ParagraphBlock)document.Body[0];
        document.Comments.Add(new Comment("c9", "Bea", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("pc9", "vedi")])));
        document.Changes.Add(new Deletion("ct9", "Ada", DateTimeOffset.UnixEpoch,
            new BlockContainer([new ParagraphBlock("pd9", "due") { Marks = [new CommentMark(0, "c9", MarkEdge.Start), new CommentMark(2, "c9", MarkEdge.End)] }])));
        document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("frame9", new PulseOffice.Documents.Objects.ParagraphAnchor("pd9"),
            new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer([new ParagraphBlock("pf9", "riquadro")])),
            new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2))));
        paragraph.Marks = [new ChangeMark(3, "ct9", MarkEdge.Point)];

        var read = _composer.Reader(DocxFlavour.Document).Read(Write(document, out _)).Value!;

        var deletion = Assert.IsType<Deletion>(Assert.Single(read.Changes.All));
        var removed = deletion.Removed.Blocks.OfType<ParagraphBlock>().ToList();
        var comment = Assert.Single(read.Comments.All);
        var edges = removed.SelectMany(item => item.Marks).OfType<CommentMark>().Where(mark => mark.CommentId == comment.Id).ToList();
        Assert.Equal(0, Assert.Single(edges, mark => mark.Edge == MarkEdge.Start).Offset);
        Assert.Equal(2, Assert.Single(edges, mark => mark.Edge == MarkEdge.End).Offset);
        var frame = Assert.Single(read.Objects.All);
        Assert.Contains(frame.Anchor.Accept(new PulseOffice.Documents.Objects.AnchorParagraph()), removed.Select(item => item.Id));
        Assert.Equal("unotre", ((ParagraphBlock)read.Body[0]).Text);
    }
}
