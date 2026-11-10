using System.Text.RegularExpressions;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfTextDecorationRound132Tests
{
    private static PdfContentCanvas Canvas() =>
        new(new DocumentSize(Twip.FromPoints(600), Twip.FromPoints(800)), new StandardPdfFonts(new PdfFontMetrics()).Create());

    private static int Strokes(string content) => Regex.Matches(content, @" l S\n").Count;

    private static readonly DocumentRect Box = new(Twip.FromPoints(10), Twip.FromPoints(10), Twip.FromPoints(200), Twip.FromPoints(20));

    [Fact]
    public void Plain_text_draws_no_decoration_line()
    {
        var canvas = Canvas();
        canvas.DrawText("Total", Box, new PrintFont("Helvetica", 10), PrintColor.Black);
        Assert.Equal(0, Strokes(canvas.Content));
    }

    [Fact]
    public void Underlined_struck_and_double_underlined_text_draw_their_lines_after_the_text()
    {
        var single = Canvas();
        single.DrawText("Total", Box, new PrintFont("Helvetica", 10) { Underline = PrintUnderline.Single }, PrintColor.Black);
        var twice = Canvas();
        twice.DrawText("Total", Box, new PrintFont("Helvetica", 10) { Underline = PrintUnderline.Double, Strikethrough = true }, PrintColor.Black);

        Assert.Equal(1, Strokes(single.Content));
        Assert.Equal(3, Strokes(twice.Content));
        Assert.True(single.Content.IndexOf("ET", StringComparison.Ordinal) < single.Content.IndexOf(" l S", StringComparison.Ordinal));
    }

    [Fact]
    public void A_turned_glyph_run_turns_its_underline_with_it()
    {
        var canvas = Canvas();
        canvas.DrawGlyphs(new PrintGlyphRun("ab", 100, 300, new PrintFont("Helvetica", 10) { Underline = PrintUnderline.Single }, PrintColor.Black, [6, 6])
        {
            RotationDegrees = 90,
        });

        var line = Regex.Match(canvas.Content, @"([\d.]+) ([\d.]+) m ([\d.]+) ([\d.]+) l S");
        Assert.True(line.Success);
        Assert.Equal(line.Groups[1].Value, line.Groups[3].Value);
        Assert.NotEqual(line.Groups[2].Value, line.Groups[4].Value);
    }
}
