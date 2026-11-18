using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round224OdsCommentTests
{
    private static readonly CellAddress A1 = new(0, 0);

    private static Worksheet RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!.Sheets[0];
    }

    private static string Content(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Replies_and_the_resolved_state_cross_ods()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[A1] = new CellComment
        {
            Text = "Question @Bob",
            Author = "Ada",
            IsResolved = true,
            Replies = [new CommentReply { Text = "Answer", Author = "Bob" }],
        };

        var comment = RoundTrip(workbook).Comments[A1];

        Assert.Equal("Question @Bob", comment.Text);
        Assert.True(comment.IsResolved);
        var reply = Assert.Single(comment.Replies);
        Assert.Equal(("Answer", "Bob"), (reply.Text, reply.Author));
    }

    [Fact]
    public void A_note_box_crosses_ods_as_svg_geometry()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Comments[A1] = new CellComment { Text = "n", Box = new CommentBox(20, 5, 150, 80) };

        Assert.Contains("svg:width=\"150pt\"", Content(workbook), StringComparison.Ordinal);
        var box = RoundTrip(workbook).Comments[A1].Box!;
        Assert.Equal(20, box.OffsetX, 1);
        Assert.Equal(5, box.OffsetY, 1);
        Assert.Equal(150, box.Width, 1);
        Assert.Equal(80, box.Height, 1);
    }

    [Fact]
    public void An_annotated_text_range_crosses_ods_with_annotation_end()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(A1, CellValue.Text("Hello world"));
        workbook.Sheets[0].Comments[A1] = new CellComment { Text = "n", AnnotatedText = new CommentTextSpan(6, 5) };

        Assert.Contains("annotation-end", Content(workbook), StringComparison.Ordinal);
        var read = RoundTrip(workbook);
        Assert.Equal(new CommentTextSpan(6, 5), read.Comments[A1].AnnotatedText);
        Assert.Equal("Hello world", read[A1].Value.AsText());
    }
}
