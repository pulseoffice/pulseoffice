using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Shaping;
using PulseOffice.Typography.Subsetting;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfShapingTests
{
    private static readonly Lazy<IndexedFontLocator> Installed = new(() =>
        new IndexedFontLocator(new SystemFontDirectories(new PulseOffice.Platform.SystemEnvironmentVariables(), ((PulseOffice.Platform.IHostSystem)new PulseOffice.Platform.RuntimeHostSystem()).Family), new OpenTypeReader(), new FontMetricsReader()));

    private sealed class Runs(IReadOnlyList<PrintGlyphRun> runs) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            foreach (var run in runs) canvas.DrawGlyphs(run);
        }
    }

    private static IPdfFontSet Fonts() => new PdfFontSetFactory(
        new PdfFaceLocator(Installed.Value, Installed.Value), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(),
        new PdfFontMetrics(), new HarfBuzzTextShaper(), new ComplexScriptSegmenter()).Create();

    private static string? Family(int codePoint, params string[] candidates) =>
        candidates.FirstOrDefault(family => Installed.Value.Find(family, false, false) is { HasTrueTypeOutlines: true } face && face.GlyphOf(codePoint) > 0);

    private static PrintGlyphRun Run(string text, string family, double y) =>
        new(text, 50, y, new PrintFont(family, 14), PrintColor.Black, [.. text.Select(_ => 7.0)]);

    [Fact]
    public void ArabicIsShapedAndSearchableInLogicalOrder()
    {
        const string text = "مرحبا بالعالم";
        if (Family('م', "Geeza Pro", "Noto Sans Arabic", "Arial Unicode MS", "DejaVu Sans") is not { } family) return;
        var face = Installed.Value.Find(family, false, false)!;

        var shown = Fonts().Show(text, new PrintFont(family, 14));

        var part = Assert.Single(shown);
        Assert.Equal(text, part.ActualText);

        using var stream = new MemoryStream();
        var result = new PdfExporter(new PdfFontSetFactory(
            new PdfFaceLocator(Installed.Value, Installed.Value), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(),
            new PdfFontMetrics(), new HarfBuzzTextShaper(), new ComplexScriptSegmenter())).Write(
            new Runs([Run("Prova " + text + " fine", family, 100)]), stream, new PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded);
        var file = stream.ToArray();
        var raw = Encoding.Latin1.GetString(file);

        Assert.Contains("/Span << /ActualText <FEFF0645", raw, StringComparison.Ordinal);
        Assert.Contains("EMC", raw, StringComparison.Ordinal);
        Assert.Contains(text, new PdfProbe(file).ExtractText(), StringComparison.Ordinal);
        Assert.NotEqual(text.Count(character => character != ' '), new HashSet<int>(text.Where(character => character != ' ').Select(character => face.GlyphOf(character)))
            .Intersect(new HarfBuzzTextShaper().Shape(face, text, new ShapingSpan(0, text.Length)).Glyphs.Select(glyph => glyph.Glyph)).Count());
    }

    [Fact]
    public void DevanagariConjunctUsesFewerGlyphs()
    {
        const string text = "क्षत्रिय";
        if (Family('क', "Kohinoor Devanagari", "Devanagari Sangam MN", "Noto Sans Devanagari", "Mangal") is not { } family) return;

        var shown = Fonts().Show(text, new PrintFont(family, 14));

        Assert.True(shown.Sum(part => part.Glyphs.Count) < text.Length);
        Assert.Equal(text, Assert.Single(shown).ActualText);
    }

    [Fact]
    public void LatinTextIsNotShapedNorMarked()
    {
        var shown = Fonts().Show("Hello", new PrintFont("Helvetica", 12));

        Assert.All(shown, part => Assert.Null(part.ActualText));
    }

    [Fact]
    public void ComposerWritesRiseAndPositionedGlyphs()
    {
        var resource = new StandardFontResource("F1", PdfStandardFont.Helvetica, new PdfFontMetrics());
        var base1 = resource.Use('a');
        var composer = new PdfTextComposer();
        composer.Add(new PdfShownText(resource, [base1, resource.Use('b') with { Rise = 200, Advance = 0 }]) { ActualText = "ab" });

        var content = composer.Compose(10, font => "/F1 10 Tf ");

        Assert.Contains("/Span << /ActualText <FEFF00610062> >> BDC", content, StringComparison.Ordinal);
        Assert.Contains("2 Ts", content, StringComparison.Ordinal);
        Assert.Contains("0 Ts", content, StringComparison.Ordinal);
        Assert.EndsWith("EMC 0 Ts ", content, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbeddedGlyphOffsetsBecomeArrayAdjustments()
    {
        if (Family('a', "Helvetica", "Arial", "DejaVu Sans", "Liberation Sans") is not { } family) return;
        var face = Installed.Value.Find(family, false, false)!;
        var resource = new EmbeddedFontResource("F1", face, EmbeddingMode.TrueTypeSubset, false, false, new TrueTypeSubsetter(), new ToUnicodeWriter());
        var a = resource.Use('a');

        var shown = resource.Show([a, resource.Use('b') with { Advance = 0, OffsetX = -300 }, a]);

        Assert.Matches(@"^<00010002> -?\d+(\.\d+)? <0001>$|^<0001> 300 <0002> -?\d+(\.\d+)? <0001>$", shown);
    }
}
