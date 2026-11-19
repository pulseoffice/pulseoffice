using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round238BidiTextTests
{
    private static string Shown(string text, IPdfBidiText? bidi)
    {
        var canvas = new PdfContentCanvas(new DocumentSize(new Twip(12000), new Twip(12000)), new StandardPdfFonts(new PdfFontMetrics()).Create(), bidi: bidi);
        canvas.DrawText(text, new DocumentRect(new Twip(0), new Twip(0), new Twip(10000), new Twip(400)), new PrintFont("Helvetica", 10), PrintColor.Black);
        var content = canvas.Content;
        return content[content.IndexOf("Tm", StringComparison.Ordinal)..];
    }

    [Fact]
    public void A_right_to_left_override_is_drawn_in_visual_order()
    {
        var shown = Shown("\u202EVWY\u202C", new PdfBidiText());

        Assert.True(shown.IndexOf('Y', StringComparison.Ordinal) < shown.IndexOf('V', StringComparison.Ordinal));
    }

    [Fact]
    public void Left_to_right_text_is_unchanged()
    {
        Assert.Equal(Shown("xyz 12", null), Shown("xyz 12", new PdfBidiText()));
    }
}
