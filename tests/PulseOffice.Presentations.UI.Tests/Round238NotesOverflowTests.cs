using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round238NotesOverflowTests
{
    private sealed class Blank : IPrintableSlide
    {
        public double Ratio => 0.5625;

        public void Paint(IPageCanvas canvas, DocumentRect area) { }
    }

    private sealed class Texts : IPageCanvas
    {
        public List<(string Text, PrintFont Font)> Drawn { get; } = [];

        public DocumentSize PageSize { get; } = new(Twip.FromPoints(600), Twip.FromPoints(800));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints * 0.5);

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
            Drawn.Add((text, font));

        public void DrawPath(PrintPath path) { }

        public void DrawImage(PageImage image, DocumentRect bounds) { }
    }

    private static List<(string Text, PrintFont Font)> Notes(string notes)
    {
        var setup = new PageSetup { Paper = new PaperCatalog().Default, Margins = Margins.Zero, HeaderHeight = Twip.Zero, FooterHeight = Twip.Zero };
        var printable = new DeckPrintable(setup, [new Blank()], [notes], DeckExportLayout.Notes, new NotesMaster { PageNumber = false });
        var canvas = new Texts();
        printable.PaintPage(0, canvas);
        return canvas.Drawn;
    }

    [Fact]
    public void Long_notes_shrink_then_end_with_an_ellipsis()
    {
        var drawn = Notes(string.Join('\n', Enumerable.Range(1, 200).Select(line => $"line {line}")));

        Assert.All(drawn, item => Assert.True(item.Font.SizePoints < 12));
        Assert.EndsWith("…", drawn[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Short_notes_keep_their_size()
    {
        var drawn = Notes("one line");

        Assert.Equal(12, Assert.Single(drawn).Font.SizePoints);
    }
}
