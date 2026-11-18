using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Tc = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round228XlsxMentionTests
{
    private static readonly CellAddress B2 = new(1, 1);

    [Fact]
    public void Mentions_cross_xlsx_as_person_links()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[B2] = new CellComment
        {
            Text = "Ask @Bob",
            Author = "Ada",
            Mentions = [new CommentMention(4, 4, "Bob")],
            Replies = [new CommentReply { Text = "Yes @Ada", Author = "Bob", Mentions = [new CommentMention(4, 4, "Ada")] }],
        };
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var entries = package.WorkbookPart!.WorksheetParts.Single().WorksheetThreadedCommentsParts.Single().ThreadedComments!.Elements<Tc.ThreadedComment>().ToList();
            var mention = Assert.Single(entries[0].ThreadedCommentMentions!.Elements<Tc.Mention>());
            Assert.Equal(4u, mention.StartIndex!.Value);
            var bob = package.WorkbookPart.WorkbookPersonParts.Single().PersonList!.Elements<Tc.Person>().Single(person => person.DisplayName == "Bob");
            Assert.Equal(bob.Id!.Value, mention.MentionpersonId!.Value);
        }
        stream.Position = 0;

        var comment = new XlsxReader().Read(stream).Value!.Sheets[0].Comments[B2];

        Assert.Equal(new CommentMention(4, 4, "Bob"), Assert.Single(comment.Mentions));
        Assert.Equal(new CommentMention(4, 4, "Ada"), Assert.Single(Assert.Single(comment.Replies).Mentions));
    }

    [Fact]
    public void A_mention_no_longer_matching_its_text_is_not_written()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[B2] = new CellComment { Text = "Ask Carl", Author = "Ada", Mentions = [new CommentMention(4, 4, "Bob")] };
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        using var package = SpreadsheetDocument.Open(stream, false);
        var entry = package.WorkbookPart!.WorksheetParts.Single().WorksheetThreadedCommentsParts.Single().ThreadedComments!.Elements<Tc.ThreadedComment>().Single();
        Assert.Null(entry.ThreadedCommentMentions);
    }
}
