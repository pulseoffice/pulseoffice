using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Printing;
using Xunit;

namespace PulseOffice.Sheets.Printing.Tests;

public sealed class Round232CommentPrintMeasureTests
{
    private sealed class WideLetters : IPrintTextMeasure
    {
        public Twip Measure(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints);
    }

    [Fact]
    public void Printed_comments_wrap_with_the_given_measure()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = string.Join(' ', Enumerable.Repeat("word", 30)) };
        var content = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(200), Twip.FromPoints(2000));

        var estimated = Assert.Single(new SheetCommentPrinter().EndPages(sheet, null, content));
        var measured = Assert.Single(new SheetCommentPrinter(new WideLetters()).EndPages(sheet, null, content));

        Assert.True(measured.Count > estimated.Count);
        Assert.All(measured, line => Assert.True(line.Length * 9 <= 200 + 0.01, line));
    }
}
