using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class PrintTextLayoutRound132Tests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<(string Text, DocumentRect Bounds, PrintFont Font)> Texts { get; } = [];
        public List<(DocumentPoint From, DocumentPoint To)> Lines { get; } = [];
        public List<PrintGlyphRun> Runs { get; } = [];
        public int Clips { get; private set; }
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) => Lines.Add((from, to));
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add((text, bounds, font));
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
        public IDisposable Clip(DocumentRect area)
        {
            Clips++;
            return new PageClipScope(null);
        }
        public void DrawGlyphs(PrintGlyphRun run) => Runs.Add(run);
    }

    private sealed class Fallback : IPageCanvas
    {
        public List<PrintFont> Fonts { get; } = [];
        public int Lines { get; private set; }
        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));
        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints((text?.Length ?? 0) * 5.0);
        public void FillRectangle(DocumentRect rect, PrintColor color) { }
        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) => Lines++;
        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Fonts.Add(font);
        public void DrawPath(PrintPath path) { }
        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private static readonly DocumentRect Cell = new(Twip.FromPoints(0), Twip.FromPoints(0), Twip.FromPoints(45), Twip.FromPoints(100));

    [Fact]
    public void A_block_wraps_at_word_boundaries_and_stacks_the_lines()
    {
        var canvas = new Recorder();
        new PrintTextBlock().Draw(canvas, new PrintTextBlockRequest("one two three four", Cell, new PrintFont("Helvetica", 10), PrintColor.Black));

        Assert.Equal(["one two", "three", "four"], canvas.Texts.Select(text => text.Text));
        Assert.Equal(Twip.FromPoints(12).Value, canvas.Texts[1].Bounds.Top.Value - canvas.Texts[0].Bounds.Top.Value);
        Assert.Equal(0, canvas.Clips);
    }

    [Fact]
    public void A_block_taller_than_its_box_clips_to_it()
    {
        var canvas = new Recorder();
        var low = Cell with { Height = Twip.FromPoints(15) };
        new PrintTextBlock().Draw(canvas, new PrintTextBlockRequest("one two three four", low, new PrintFont("Helvetica", 10), PrintColor.Black, Baseline: TextBaseline.Top));

        Assert.Equal(1, canvas.Clips);
        Assert.Equal(["one two", "three"], canvas.Texts.Select(text => text.Text));
    }

    [Fact]
    public void Turned_text_is_one_glyph_run_centred_on_the_requested_point()
    {
        var canvas = new Recorder();
        new PrintRotatedText().Draw(canvas, new PrintRotatedTextRequest("abcd", 100, 200, new PrintFont("Helvetica", 10), PrintColor.Black, 90));

        var run = Assert.Single(canvas.Runs);
        Assert.Equal(90, run.RotationDegrees);
        Assert.Equal(20, run.Width, 6);
        Assert.Equal(200 + 10, run.Baseline, 6);
    }

    [Fact]
    public void The_decoration_painter_draws_under_and_through_lines_along_the_turned_baseline()
    {
        var canvas = new Recorder();
        new TextDecorationPainter().Paint(canvas, new PrintTextSpan(10, 100, 40,
            new PrintFont("Helvetica", 10) { Underline = PrintUnderline.Double, Strikethrough = true }, PrintColor.Black, 90));

        Assert.Equal(3, canvas.Lines.Count);
        Assert.All(canvas.Lines, line => Assert.InRange(line.From.Y.Value - line.To.Y.Value, Twip.FromPoints(40).Value - 1, Twip.FromPoints(40).Value + 1));
    }

    [Fact]
    public void The_fallback_glyph_drawing_underlines_the_run_once_not_each_glyph()
    {
        var canvas = new Fallback();
        ((IPageCanvas)canvas).DrawGlyphs(new PrintGlyphRun("abc", 10, 50, new PrintFont("Helvetica", 10) { Underline = PrintUnderline.Single }, PrintColor.Black, [5, 5, 5]));

        Assert.Equal(3, canvas.Fonts.Count);
        Assert.All(canvas.Fonts, font => Assert.False(font.IsDecorated));
        Assert.Equal(1, canvas.Lines);
    }
}
