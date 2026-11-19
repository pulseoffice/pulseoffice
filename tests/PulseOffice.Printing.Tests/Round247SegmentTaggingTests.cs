using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class Round247SegmentTaggingTests
{
    private sealed class Recorder : IPageCanvas
    {
        private PrintTag? _tag;

        public List<PrintTag?> Tags { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Tags.Add(_tag);

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void Tag(PrintTag? tag) => _tag = tag;
    }

    private sealed class Halves(PrintTag left, PrintTag right) : IPrintSegmentLocator
    {
        public PrintTag? At(double xPoints, double yPoints) => xPoints < 100 ? left : right;
    }

    private static DocumentRect At(double left) => new(Twip.FromPoints(left), Twip.Zero, Twip.FromPoints(20), Twip.FromPoints(10));

    [Fact]
    public void A_locator_places_runs_by_position_whatever_their_text()
    {
        var a = new PrintTag("a", PrintRole.TableCell);
        var b = new PrintTag("b", PrintRole.TableCell);
        var recorder = new Recorder();
        var canvas = new TextSegmentTagging().Wrap(recorder, new PrintTextSegments(
            [new PrintTextSegment(a, "1234.5"), new PrintTextSegment(b, "Nord")], AdvanceOnMiss: true) { Locator = new Halves(a, b) });

        canvas.DrawText("1.234", At(10), PrintFont.Default, PrintColor.Black);
        canvas.DrawText(",50", At(40), PrintFont.Default, PrintColor.Black);
        canvas.DrawText("Nord", At(150), PrintFont.Default, PrintColor.Black);

        Assert.Equal(new PrintTag?[] { a, a, b }, recorder.Tags.ToArray());
    }

    [Fact]
    public void Text_that_matches_no_paragraph_moves_on_once_the_current_one_is_drawn()
    {
        var first = new PrintTag("p1", PrintRole.Paragraph);
        var second = new PrintTag("p2", PrintRole.Paragraph);
        var recorder = new Recorder();
        var canvas = new TextSegmentTagging().Wrap(recorder, new PrintTextSegments(
            [new PrintTextSegment(first, "Titolo"), new PrintTextSegment(second, "Pagina <numero>")], AdvanceOnMiss: false));

        canvas.DrawText("Titolo", default, PrintFont.Default, PrintColor.Black);
        canvas.DrawText("Pagina 3", default, PrintFont.Default, PrintColor.Black);

        Assert.Equal(new PrintTag?[] { first, second }, recorder.Tags.ToArray());
    }
}
