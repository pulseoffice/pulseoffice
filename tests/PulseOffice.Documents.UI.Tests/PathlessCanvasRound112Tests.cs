using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public class PathlessCanvasRound112Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Events { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(100), Twip.FromPoints(100));

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
            Events.Add("text " + text);

        public void DrawPath(PrintPath path) => Events.Add("path");

        public void DrawImage(PageImage image, DocumentRect bounds) { }

        public void DrawGlyphs(PrintGlyphRun run) => Events.Add("glyphs " + run.Text);

        public IDisposable Clip(DocumentRect area)
        {
            Events.Add("clip");
            return new PageClipScope(null);
        }

        public void MarkLink(DocumentRect area, string target) => Events.Add("link " + target);
    }

    [Fact]
    public void Shape_text_keeps_its_glyph_runs_links_and_clips()
    {
        var recorder = new Recorder();
        var canvas = new PathlessCanvas(recorder);

        canvas.DrawGlyphs(new PrintGlyphRun("ab", 0, 10, PrintFont.Default, PrintColor.Black, [5, 5]));
        canvas.MarkLink(new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10)), "https://example.org");
        using (canvas.Clip(new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10))))
        {
        }

        Assert.Equal(["glyphs ab", "link https://example.org", "clip"], recorder.Events);
    }
}
