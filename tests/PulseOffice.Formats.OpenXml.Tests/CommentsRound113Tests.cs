using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class CommentsRound113Tests
{
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly DocxComposer _docx = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private PulseOffice.Documents.TextDocument ReadDocx(Stream source) => _docx.Reader(DocxFlavour.Document).Read(source).Value!;

    [Fact]
    public void A_comment_without_paragraphs_is_saved_to_docx_and_read_back()
    {
        var document = new PulseOffice.Documents.TextDocument();
        var paragraph = new ParagraphBlock("p1", new TextParagraph("testo"));
        document.Body.Add(paragraph);
        document.Comments.Add(new Comment("c1", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([])));
        paragraph.State = paragraph.State with { Marks = [new CommentMark(0, "c1", MarkEdge.Start), new CommentMark(5, "c1", MarkEdge.End)] };

        using var written = new MemoryStream();
        var result = _docx.Writer(DocxFlavour.Document).Write(document, written);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        written.Position = 0;
        Assert.Equal("Ada", Assert.Single(ReadDocx(written).Comments.All).Author);
    }

    [Fact]
    public void A_docx_comment_that_names_itself_as_parent_stays_anchored()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            Feed(main, $"<w:document xmlns:w=\"{Word}\"><w:body><w:p><w:commentRangeStart w:id=\"1\"/><w:r><w:t>testo</w:t></w:r><w:commentRangeEnd w:id=\"1\"/><w:r><w:commentReference w:id=\"1\"/></w:r></w:p></w:body></w:document>");
            Feed(main.AddNewPart<WordprocessingCommentsPart>(), $"<w:comments xmlns:w=\"{Word}\" xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"><w:comment w:id=\"1\" w:author=\"a\"><w:p w14:paraId=\"0000000A\"><w:r><w:t>uno</w:t></w:r></w:p></w:comment></w:comments>");
            Feed(main.AddNewPart<WordprocessingCommentsExPart>(), "<w15:commentsEx xmlns:w15=\"http://schemas.microsoft.com/office/word/2012/wordml\"><w15:commentEx w15:paraId=\"0000000A\" w15:paraIdParent=\"0000000A\" w15:done=\"1\"/></w15:commentsEx>");
        }
        source.Position = 0;

        var comment = Assert.Single(ReadDocx(source).Comments.All);

        Assert.Null(comment.ParentId);
        Assert.True(comment.Resolved);
    }

    [Fact]
    public void A_docx_reply_cycle_keeps_one_anchored_thread()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            Feed(main, $"<w:document xmlns:w=\"{Word}\"><w:body><w:p><w:commentRangeStart w:id=\"1\"/><w:commentRangeStart w:id=\"2\"/><w:r><w:t>testo</w:t></w:r><w:commentRangeEnd w:id=\"1\"/><w:commentRangeEnd w:id=\"2\"/><w:r><w:commentReference w:id=\"1\"/></w:r><w:r><w:commentReference w:id=\"2\"/></w:r></w:p></w:body></w:document>");
            Feed(main.AddNewPart<WordprocessingCommentsPart>(), $"<w:comments xmlns:w=\"{Word}\" xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"><w:comment w:id=\"1\" w:author=\"a\"><w:p w14:paraId=\"0000000A\"><w:r><w:t>uno</w:t></w:r></w:p></w:comment><w:comment w:id=\"2\" w:author=\"b\"><w:p w14:paraId=\"0000000B\"><w:r><w:t>due</w:t></w:r></w:p></w:comment></w:comments>");
            Feed(main.AddNewPart<WordprocessingCommentsExPart>(), "<w15:commentsEx xmlns:w15=\"http://schemas.microsoft.com/office/word/2012/wordml\"><w15:commentEx w15:paraId=\"0000000A\" w15:paraIdParent=\"0000000B\"/><w15:commentEx w15:paraId=\"0000000B\" w15:paraIdParent=\"0000000A\"/></w15:commentsEx>");
        }
        source.Position = 0;

        var comments = ReadDocx(source).Comments.All;

        Assert.Equal(2, comments.Count);
        Assert.Single(comments, comment => comment.ParentId is null);
    }

    [Fact]
    public void Threaded_xlsx_comments_keep_their_author_date_and_replies()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[new CellAddress(0, 0)] = new CellComment { Text = "[Threaded comment] placeholder", Author = "tc={00000000-0000-0000-0000-000000000001}" };
        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);

        using (var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(buffer, true))
        {
            var book = package.WorkbookPart!;
            Feed(book.AddNewPart<WorkbookPersonPart>(), "<personList xmlns=\"http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments\"><person displayName=\"Ada Lovelace\" id=\"{AAAAAAAA-0000-0000-0000-000000000001}\" userId=\"ada\" providerId=\"None\"/><person displayName=\"Charles\" id=\"{AAAAAAAA-0000-0000-0000-000000000002}\" userId=\"cb\" providerId=\"None\"/></personList>");
            Feed(book.WorksheetParts.Single().AddNewPart<WorksheetThreadedCommentsPart>(), "<ThreadedComments xmlns=\"http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments\">"
                + "<threadedComment ref=\"A1\" dT=\"2026-11-07T10:30:00.00\" personId=\"{AAAAAAAA-0000-0000-0000-000000000001}\" id=\"{BBBBBBBB-0000-0000-0000-000000000001}\" done=\"1\"><text>Domanda</text></threadedComment>"
                + "<threadedComment ref=\"A1\" dT=\"2026-11-07T11:00:00.00\" personId=\"{AAAAAAAA-0000-0000-0000-000000000002}\" id=\"{BBBBBBBB-0000-0000-0000-000000000002}\" parentId=\"{BBBBBBBB-0000-0000-0000-000000000001}\"><text>Risposta</text></threadedComment>"
                + "<threadedComment ref=\"A1\" personId=\"{AAAAAAAA-0000-0000-0000-000000000002}\" id=\"{BBBBBBBB-0000-0000-0000-000000000003}\" parentId=\"{BBBBBBBB-0000-0000-0000-000000000003}\"><text>Sé stesso</text></threadedComment>"
                + "</ThreadedComments>");
        }
        buffer.Position = 0;

        var result = new XlsxReader().Read(buffer);
        var comment = result.Value!.Sheets[0].Comments[new CellAddress(0, 0)];

        Assert.Equal("Ada Lovelace", comment.Author);
        Assert.Equal("Domanda\nCharles: Risposta\nCharles: Sé stesso", comment.Text);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 10, 30, 0, TimeSpan.Zero), comment.CreatedAt);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "XLSX235");
    }

    [Fact]
    public void Modern_powerpoint_comments_are_read_with_their_replies()
    {
        var themes = new ThemeCatalog();
        var master = new MasterFactory().CreateStandard(themes.Default);
        var deck = new Presentation { Theme = themes.Default, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleOnly));
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);

        using (var package = PresentationDocument.Open(buffer, true))
        {
            var presentation = package.PresentationPart!;
            Feed(presentation.AddNewPart<PowerPointAuthorsPart>(), "<p188:authorLst xmlns:p188=\"http://schemas.microsoft.com/office/powerpoint/2018/8/main\"><p188:author id=\"{A1}\" name=\"Ada\" initials=\"A\" userId=\"ada\" providerId=\"None\"/><p188:author id=\"{A2}\" name=\"Bob\" initials=\"B\" userId=\"bob\" providerId=\"None\"/></p188:authorLst>");
            Feed(presentation.SlideParts.Single().AddNewPart<PowerPointCommentPart>(), "<p188:cmLst xmlns:p188=\"http://schemas.microsoft.com/office/powerpoint/2018/8/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">"
                + "<p188:cm id=\"{C1}\" authorId=\"{A1}\" created=\"2026-11-07T09:00:00.000\" status=\"resolved\"><p188:pos x=\"0\" y=\"0\"/><p188:replyLst><p188:reply id=\"{R1}\" authorId=\"{A2}\" created=\"2026-11-07T09:05:00.000\"><p188:txBody><a:bodyPr/><a:p><a:r><a:t>Fatto</a:t></a:r></a:p></p188:txBody></p188:reply></p188:replyLst>"
                + "<p188:txBody><a:bodyPr/><a:p><a:r><a:t>Rivedi</a:t></a:r></a:p></p188:txBody></p188:cm></p188:cmLst>");
        }
        buffer.Position = 0;

        var result = new PptxReader(themes, new MasterFactory()).Read(buffer);
        var comment = Assert.Single(result.Value!.Slides.Single().Comments);

        Assert.Equal("Ada", comment.Author);
        Assert.Equal("Rivedi\nBob: Fatto", comment.Text);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), comment.CreatedAt);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PPTX223");
    }

    [Fact]
    public void A_slide_comment_with_control_characters_is_saved()
    {
        var themes = new ThemeCatalog();
        var master = new MasterFactory().CreateStandard(themes.Default);
        var deck = new Presentation { Theme = themes.Default, Master = master };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleOnly);
        slide.AddComment(new SlideComment { Id = "c1", Author = "A\u0001da", Text = "riga\u0002uno" });
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;

        var comment = Assert.Single(new PptxReader(themes, new MasterFactory()).Read(buffer).Value!.Slides.Single().Comments);
        Assert.Equal("rigauno", comment.Text);
        Assert.Equal("Ada", comment.Author);
    }

    private static void Feed(OpenXmlPart part, string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        part.FeedData(stream);
    }
}
