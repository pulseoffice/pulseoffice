using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class Round243SegmentTaggingTests
{
    private sealed class Recorder : IPageCanvas
    {
        private PrintTag? _tag;
        private int _artifacts;

        public List<(string Text, PrintTag? Tag)> Texts { get; } = [];

        public List<bool> FillsAsArtifacts { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color) => FillsAsArtifacts.Add(_artifacts > 0);

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color,
            TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) => Texts.Add((text, _tag));

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void Tag(PrintTag? tag) => _tag = tag;

        public void BeginArtifact() => _artifacts++;

        public void EndArtifact() => _artifacts--;
    }

    [Fact]
    public void Drawn_text_follows_the_paragraphs_and_a_bullet_takes_the_next_label()
    {
        var first = new PrintTag("p1", PrintRole.Paragraph);
        var label = new PrintTag("lbl", PrintRole.ListLabel);
        var body = new PrintTag("lb", PrintRole.ListBody);
        var recorder = new Recorder();
        var canvas = new TextSegmentTagging().Wrap(recorder, new PrintTextSegments(
            [new PrintTextSegment(first, "Primo capoverso lungo"), new PrintTextSegment(body, "Voce") { Label = label }], AdvanceOnMiss: false));

        canvas.FillRectangle(default, PrintColor.Black);
        canvas.DrawText("Primo", default, PrintFont.Default, PrintColor.Black);
        canvas.DrawText("capoverso lungo", default, PrintFont.Default, PrintColor.Black);
        canvas.DrawText("•", default, PrintFont.Default, PrintColor.Black);
        canvas.DrawText("Voce", default, PrintFont.Default, PrintColor.Black);

        Assert.Equal(new PrintTag?[] { first, first, label, body }, recorder.Texts.Select(text => text.Tag).ToArray());
        Assert.Equal(new[] { true }, recorder.FillsAsArtifacts.ToArray());
    }

    [Fact]
    public void Table_cells_advance_on_text_that_matches_no_cell()
    {
        var a = new PrintTag("a", PrintRole.TableCell);
        var b = new PrintTag("b", PrintRole.TableCell);
        var recorder = new Recorder();
        var canvas = new TextSegmentTagging().Wrap(recorder, new PrintTextSegments(
            [new PrintTextSegment(a, "1234.5"), new PrintTextSegment(b, "Nord")], AdvanceOnMiss: true));

        canvas.DrawText("1.234,50", default, PrintFont.Default, PrintColor.Black);
        canvas.DrawText("Nord", default, PrintFont.Default, PrintColor.Black);

        Assert.Equal(new PrintTag?[] { a, b }, recorder.Texts.Select(text => text.Tag).ToArray());
    }
}
