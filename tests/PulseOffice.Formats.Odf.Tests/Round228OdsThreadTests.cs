using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round228OdsThreadTests
{
    private static readonly CellAddress A1 = new(0, 0);

    private static Workbook Threaded()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[A1] = new CellComment
        {
            Text = "Question @Bob",
            Author = "Ada",
            Mentions = [new CommentMention(9, 4, "Bob")],
            Replies =
            [
                new CommentReply { Text = "First\nsecond line", Author = "Bob", CreatedAt = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero) },
                new CommentReply { Text = "Thanks @Ada", Author = "Cy", Mentions = [new CommentMention(7, 4, "Ada")] },
            ],
        };
        return workbook;
    }

    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_thread_is_one_annotation_whose_paragraphs_hold_the_replies()
    {
        using var stream = Written(Threaded());
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        var content = reader.ReadToEnd();

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(content, "<office:annotation[ >]"));
        Assert.Contains("Bob, 2026-11-18 09:00:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void The_thread_and_its_mentions_read_back()
    {
        using var stream = Written(Threaded());
        var comment = new OdsReader().Read(stream).Value!.Sheets[0].Comments[A1];

        Assert.Equal(("Question @Bob", "Ada"), (comment.Text, comment.Author));
        Assert.Equal(new CommentMention(9, 4, "Bob"), Assert.Single(comment.Mentions));
        Assert.Equal(2, comment.Replies.Count);
        Assert.Equal(("First\nsecond line", "Bob"), (comment.Replies[0].Text, comment.Replies[0].Author));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero), comment.Replies[0].CreatedAt);
        Assert.Equal(new CommentMention(7, 4, "Ada"), Assert.Single(comment.Replies[1].Mentions));
    }
}
