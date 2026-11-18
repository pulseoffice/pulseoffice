using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Printing;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round228CommentPrintWrapTests
{
    [Fact]
    public void Comments_at_the_end_wrap_to_the_page_width()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Comments[new CellAddress(0, 0)] = new CellComment
        {
            Text = string.Join(' ', Enumerable.Repeat("word", 60)),
            Replies = [new CommentReply { Text = string.Join(' ', Enumerable.Repeat("reply", 40)) }],
        };
        var content = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(200), Twip.FromPoints(2000));

        var lines = Assert.Single(new SheetCommentPrinter().EndPages(sheet, null, content));

        Assert.True(lines.Count > 6);
        Assert.All(lines, line => Assert.True(line.Length * 9 * 0.55 <= 200 + 0.01, line));
        Assert.Contains(lines, line => line.StartsWith("        reply", StringComparison.Ordinal));
        Assert.Contains(lines.Skip(2), line => line.StartsWith("    word", StringComparison.Ordinal));
    }
}
