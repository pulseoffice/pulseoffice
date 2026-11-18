using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round224XlsxCommentTests
{
    private static readonly CellAddress B2 = new(1, 1);

    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_thread_is_written_as_threaded_comments_with_a_legacy_note()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[B2] = new CellComment
        {
            Text = "Question",
            Author = "Ada",
            IsResolved = true,
            Replies = [new CommentReply { Text = "Answer @Ada", Author = "Bob", CreatedAt = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero) }],
        };
        using var stream = Written(workbook);

        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var sheet = package.WorkbookPart!.WorksheetParts.Single();
            Assert.Equal(2, sheet.WorksheetThreadedCommentsParts.Single().ThreadedComments!.ChildElements.Count);
            Assert.Equal(2, package.WorkbookPart.WorkbookPersonParts.Single().PersonList!.ChildElements.Count);
            Assert.Contains("Answer @Ada", sheet.WorksheetCommentsPart!.Comments!.InnerText, StringComparison.Ordinal);
        }
        stream.Position = 0;

        var read = new XlsxReader().Read(stream);
        var comment = read.Value!.Sheets[0].Comments[B2];

        Assert.Equal(("Question", "Ada", true), (comment.Text, comment.Author, comment.IsResolved));
        var reply = Assert.Single(comment.Replies);
        Assert.Equal(("Answer @Ada", "Bob"), (reply.Text, reply.Author));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero), reply.CreatedAt);
    }

    [Fact]
    public void A_plain_note_stays_a_legacy_note()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[B2] = new CellComment { Text = "Note", Author = "Ada" };
        using var stream = Written(workbook);

        using var package = SpreadsheetDocument.Open(stream, false);
        Assert.Empty(package.WorkbookPart!.WorksheetParts.Single().WorksheetThreadedCommentsParts);
    }

    [Fact]
    public void A_note_box_crosses_xlsx_through_the_vml_anchor()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[B2] = new CellComment { Text = "Note", Box = new CommentBox(30, 12, 144, 90) };
        using var stream = Written(workbook);

        var box = new XlsxReader().Read(stream).Value!.Sheets[0].Comments[B2].Box!;

        Assert.Equal(30, box.OffsetX, 0);
        Assert.Equal(12, box.OffsetY, 0);
        Assert.Equal(144, box.Width, 0);
        Assert.Equal(90, box.Height, 0);
    }
}
