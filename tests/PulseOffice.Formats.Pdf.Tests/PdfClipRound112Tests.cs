using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public class PdfClipRound112Tests
{
    private PdfContentCanvas Canvas() => new(
        new DocumentSize(Twip.FromPoints(600), Twip.FromPoints(800)),
        new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics()).Create());

    [Fact]
    public void A_clip_saves_the_state_sets_the_clip_path_and_restores_it()
    {
        var canvas = Canvas();

        using (canvas.Clip(new DocumentRect(Twip.FromPoints(10), Twip.FromPoints(20), Twip.FromPoints(100), Twip.FromPoints(50))))
            canvas.DrawImage(new PageImage(1, 1, [0, 0, 0]), new DocumentRect(Twip.FromPoints(0), Twip.FromPoints(0), Twip.FromPoints(200), Twip.FromPoints(200)));

        var content = canvas.Content;
        var clip = content.IndexOf("10 730 100 50 re W n", StringComparison.Ordinal);
        Assert.True(clip > 0, content);
        Assert.Equal('q', content[content.LastIndexOf('\n', clip - 1) - 1]);
        Assert.True(content.IndexOf("Do", StringComparison.Ordinal) > clip);
        Assert.EndsWith("Q\nQ\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Disposing_a_clip_twice_restores_once()
    {
        var canvas = Canvas();

        var scope = canvas.Clip(new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10)));
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, canvas.Content.Split('\n').Count(line => line == "Q"));
    }
}
