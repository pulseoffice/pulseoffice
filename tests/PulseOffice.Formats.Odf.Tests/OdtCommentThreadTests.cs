using System.IO.Compression;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtCommentThreadTests
{
    private static string Annotation(string name, string author, string text, string? parent = null) =>
        $"<office:annotation office:name=\"{name}\"{(parent is null ? string.Empty : $" loext:parent-name=\"{parent}\"")}><dc:creator>{author}</dc:creator><text:p>{text}</text:p></office:annotation>";

    [Fact]
    public void A_reply_whose_parent_is_missing_becomes_an_anchored_comment()
    {
        var document = new OdtReader().Read(Odt($"<text:p>{Annotation("r", "Bea", "orfana", "assente")}testo</text:p>")).Value!;

        var comment = Assert.Single(document.Comments.All);
        Assert.Null(comment.ParentId);
        Assert.Contains(Marks(document), mark => mark.CommentId == comment.Id);
    }

    [Fact]
    public void A_reply_written_before_its_parent_still_joins_the_thread()
    {
        var document = new OdtReader().Read(Odt($"<text:p>{Annotation("r", "Bea", "risposta", "p")}{Annotation("p", "Ada", "domanda")}testo<office:annotation-end office:name=\"p\"/></text:p>")).Value!;

        var reply = Assert.Single(document.Comments.All, comment => comment.Author == "Bea");
        Assert.Equal("p", reply.ParentId);
        Assert.DoesNotContain(Marks(document), mark => mark.CommentId == reply.Id);
    }

    [Fact]
    public void Duplicate_annotation_names_keep_each_range_and_reply_on_its_own_comment()
    {
        var document = new OdtReader().Read(Odt($"<text:p>{Annotation("x", "Ada", "uno")}ab<office:annotation-end office:name=\"x\"/>{Annotation("x", "Bea", "due")}cd<office:annotation-end office:name=\"x\"/>{Annotation("y", "Cia", "tre", "x")}</text:p>")).Value!;

        var second = Assert.Single(document.Comments.All, comment => comment.Author == "Bea");
        var reply = Assert.Single(document.Comments.All, comment => comment.Author == "Cia");
        Assert.NotEqual("x", second.Id);
        Assert.Equal(second.Id, reply.ParentId);
        Assert.Contains(Marks(document), mark => mark.CommentId == second.Id && mark.Edge == MarkEdge.End && mark.Offset == 4);
        Assert.Contains(Marks(document), mark => mark.CommentId == "x" && mark.Edge == MarkEdge.End && mark.Offset == 2);
    }

    [Fact]
    public void A_reply_cycle_keeps_one_root()
    {
        var document = new OdtReader().Read(Odt($"<text:p>{Annotation("a", "Ada", "uno", "b")}{Annotation("b", "Bea", "due", "a")}testo</text:p>")).Value!;

        var root = Assert.Single(document.Comments.All, comment => comment.ParentId is null);
        Assert.Contains(Marks(document), mark => mark.CommentId == root.Id);
    }

    private static IEnumerable<CommentMark> Marks(PulseOffice.Documents.TextDocument document) =>
        document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<CommentMark>();

    private static MemoryStream Odt(string body)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
                + "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:loext=\"urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0\" office:version=\"1.3\"><office:body><office:text>" + body + "</office:text></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
