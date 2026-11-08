using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class ClippedPageCanvasTests
{
    private sealed class GlyphRecorder : IPageCanvas
    {
        public DocumentSize PageSize { get; } = new(Twip.FromPoints(800), Twip.FromPoints(800));

        public List<PrintGlyphRun> Runs { get; } = [];

        public Twip MeasureText(string text, PrintFont font) => Twip.Zero;

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public List<string> Texts { get; } = [];

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) =>
            Texts.Add(text);

        public void DrawPath(PrintPath path)
        {
        }

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void DrawGlyphs(PrintGlyphRun run) => Runs.Add(run);
    }

    private static DocumentRect Points(double left, double top, double width, double height) =>
        new(Twip.FromPoints(left), Twip.FromPoints(top), Twip.FromPoints(width), Twip.FromPoints(height));

    private static PrintGlyphRun Run(string text, double x, double baseline, double rotation = 0) =>
        new(text, x, baseline, new PrintFont("Helvetica", 20), PrintColor.Black, [.. text.Select(_ => 10.0)]) { RotationDegrees = rotation };

    [Fact]
    public void Una_riga_che_sborda_perde_solo_le_lettere_fuori_dal_ritaglio()
    {
        var recorder = new GlyphRecorder();
        new ClippedPageCanvas(recorder, Points(0, 0, 100, 100)).DrawGlyphs(Run("abcdefghijkl", 10, 50));

        var drawn = Assert.Single(recorder.Runs);
        Assert.Equal("abcdefghi", drawn.Text);
        Assert.Equal(10, drawn.X, 3);
    }

    [Fact]
    public void Il_testo_ruotato_dentro_il_ritaglio_non_si_perde()
    {
        var recorder = new GlyphRecorder();
        new ClippedPageCanvas(recorder, Points(0, 0, 100, 200)).DrawGlyphs(Run("abcdefghij", 50, 150, 90));

        Assert.Equal("abcdefghij", Assert.Single(recorder.Runs).Text);
    }

    [Fact]
    public void Il_testo_ruotato_che_esce_tiene_la_parte_visibile_al_suo_posto()
    {
        var recorder = new GlyphRecorder();
        new ClippedPageCanvas(recorder, Points(0, 0, 100, 100)).DrawGlyphs(Run("abcdefghij", 50, 80, 90));

        var drawn = Assert.Single(recorder.Runs);
        Assert.Equal("abcdefgh", drawn.Text);
        Assert.Equal(50, drawn.X, 3);
    }

    [Fact]
    public void Un_testo_poco_piu_alto_della_cella_unita_si_stampa_lo_stesso()
    {
        var recorder = new GlyphRecorder();
        new ClippedPageCanvas(recorder, Points(0, 0, 400, 15)).DrawText("Bilancio annuale 2026", Points(0, 0, 400, 15), new PrintFont("Calibri", 16), PrintColor.Black, TextAlignment.Center);

        Assert.Equal(["Bilancio annuale 2026"], recorder.Texts);
    }

    [Fact]
    public void Un_testo_con_il_centro_fuori_dal_ritaglio_non_si_stampa()
    {
        var recorder = new GlyphRecorder();
        new ClippedPageCanvas(recorder, Points(0, 0, 400, 15)).DrawText("Sotto", Points(0, 20, 400, 15), new PrintFont("Calibri", 10), PrintColor.Black);

        Assert.Empty(recorder.Texts);
    }
}
