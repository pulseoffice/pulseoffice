using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class CanvasMarksForwardingTests
{
    private sealed class Recorder : IPageCanvas
    {
        public List<string> Marks { get; } = [];

        public DocumentSize PageSize => new(Twip.FromPoints(600), Twip.FromPoints(800));

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) { }

        public void DrawPath(PrintPath path) { }

        public void DrawImage(PageImage image, DocumentRect bounds) { }

        public void MarkHeading(int level, string title, double topPoints) => Marks.Add($"h:{title}:{topPoints}");

        public void MarkTarget(string name, double topPoints) => Marks.Add($"t:{name}:{topPoints}");

        public void MarkLink(DocumentRect area, string target) => Marks.Add($"l:{target}:{area.Left.ToPoints()}:{area.Width.ToPoints()}");

        public void MarkNote(DocumentRect area, PrintNote note) => Marks.Add($"n:{note.Text}");
    }

    [Fact]
    public void Le_tele_ridotta_e_ritagliata_inoltrano_titoli_collegamenti_e_note()
    {
        var recorder = new Recorder();
        IPageCanvas scaled = new ScaledPageCanvas(recorder, 0.5, new DocumentPoint(Twip.Zero, Twip.Zero), new DocumentPoint(Twip.Zero, Twip.Zero));
        IPageCanvas clipped = new ClippedPageCanvas(scaled, new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(600), Twip.FromPoints(800)));
        var area = new DocumentRect(Twip.FromPoints(100), Twip.FromPoints(100), Twip.FromPoints(40), Twip.FromPoints(10));

        clipped.MarkHeading(1, "Capitolo", 200);
        clipped.MarkTarget("ancora", 200);
        clipped.MarkLink(area, "https://example.org");
        clipped.MarkNote(area, new PrintNote("Anna", "nota"));

        Assert.Equal(["h:Capitolo:100", "t:ancora:100", "l:https://example.org:50:20", "n:nota"], recorder.Marks);
    }
}

public sealed class PrintColorHexTests
{
    [Theory]
    [InlineData("#GG0000")]
    [InlineData("zzzzzz")]
    [InlineData("#12345")]
    public void Un_colore_esadecimale_non_valido_diventa_nero_senza_eccezioni(string hex) =>
        Assert.Equal(PrintColor.Black, PrintColor.FromHex(hex));
}
