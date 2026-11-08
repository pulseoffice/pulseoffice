using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using PulseOffice.Typography.Subsetting;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfEmbeddedFontTests
{
    private static readonly Lazy<IndexedFontLocator> Installed = new(() =>
        new IndexedFontLocator(new SystemFontDirectories(), new OpenTypeReader(), new FontMetricsReader()));

    private sealed class Lines(IReadOnlyList<(string Text, PrintFont Font)> lines, int pages = 1) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            var top = 60.0;
            foreach (var (text, font) in lines)
            {
                canvas.DrawText(text, new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(top), Twip.FromPoints(500), Twip.FromPoints(24)), font, PrintColor.Black);
                top += 28;
            }
        }
    }

    private static PdfExporter Exporter() => new(new PdfFontSetFactory(
        new PdfFaceLocator(Installed.Value, Installed.Value), new PdfFallbackFonts(), new TrueTypeSubsetter(), new ToUnicodeWriter(), new PdfFontMetrics(), new PulseOffice.Typography.Shaping.HarfBuzzTextShaper(), new PulseOffice.Typography.Shaping.ComplexScriptSegmenter()));

    private static string? Family(params string[] candidates) =>
        candidates.FirstOrDefault(family => Installed.Value.Find(family, false, false) is { HasTrueTypeOutlines: true });

    private static (byte[] File, OperationOutcome Outcome) Write(IPrintableDocument document, PdfExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = Exporter().Write(document, stream, options ?? new PdfExportOptions("Prova"));
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        if (Environment.GetEnvironmentVariable("PDF_FONTS_OUT") is { Length: > 0 } output) File.WriteAllBytes(output, stream.ToArray());
        return (stream.ToArray(), new OperationOutcome([.. result.Diagnostics.Select(diagnostic => diagnostic.Code)]));
    }

    private sealed record OperationOutcome(IReadOnlyList<string> Codes);

    [Fact]
    public void IlTestoInPiuScrittureSiIncorporaESiRilegge()
    {
        if (Family("Arial", "Helvetica", "DejaVu Sans", "Liberation Sans", "Noto Sans") is not { } family) return;
        var font = new PrintFont(family, 12);
        string[] lines =
        [
            "Perché così è già più «bello» — 10 €",
            "Größe über Straße, Ärger",
            "Ελληνικά: αβγδε ΑΒΓΔ",
            "Русский текст: съешь же ещё",
        ];

        var (file, outcome) = Write(new Lines([.. lines.Select(line => (line, font))]));
        var probe = new PdfProbe(file);

        Assert.NotEmpty(probe.EmbeddedFontNames);
        Assert.False(probe.UsesStandardFonts);
        Assert.All(probe.EmbeddedFontNames, name => Assert.Matches("^[A-Z]{6}\\+", name));
        var text = probe.ExtractText();
        foreach (var line in lines) Assert.Contains(line, text, StringComparison.Ordinal);
        Assert.DoesNotContain("PDF012", outcome.Codes);
    }

    [Fact]
    public void ICaratteriMancantiPassanoAUnAltroCarattereInstallato()
    {
        if (Family("Times New Roman", "Times", "Liberation Serif", "DejaVu Serif") is not { } family) return;
        var cjk = Family("Arial Unicode MS", "Noto Sans CJK SC", "PingFang SC", "Hiragino Sans GB", "STHeiti", "Microsoft YaHei");
        if (cjk is null) return;
        const string line = "Testo 中文文本 e 日本語";

        var (file, _) = Write(new Lines([(line, new PrintFont(family, 12))]));
        var probe = new PdfProbe(file);

        Assert.True(probe.EmbeddedFontNames.Count() >= 2);
        Assert.Contains(line, probe.ExtractText(), StringComparison.Ordinal);
    }

    [Fact]
    public void IlSottoinsiemeEPiccoloRispettoAlCarattereIntero()
    {
        if (Family("Arial Unicode MS", "Noto Sans", "DejaVu Sans") is not { } family) return;
        var whole = Installed.Value.Find(family, false, false)!.Tables.Size;

        var (file, _) = Write(new Lines([("Poche lettere", new PrintFont(family, 12))], pages: 1));

        Assert.True(file.Length < whole / 4, $"{file.Length} su {whole}");
    }

    [Fact]
    public void IlSottoinsiemeERiletto()
    {
        if (Family("Arial", "Helvetica", "DejaVu Sans", "Liberation Sans") is not { } family) return;
        var face = Installed.Value.Find(family, false, false)!;
        var glyphs = "Àbc".Select(character => face.GlyphOf(character)).ToList();

        var subset = new TrueTypeSubsetter().Subset(face.Tables, glyphs);
        var reread = new FontMetricsReader().Read(subset.Data).Single();

        Assert.Equal(subset.Glyphs.Count, reread.GlyphCount);
        Assert.Equal(face.AdvanceOfGlyph(glyphs[1]), reread.AdvanceOfGlyph(2));
        Assert.Equal(0, subset.Glyphs[0]);
    }

    [Fact]
    public void SenzaCaratteriInstallatiSiRipiegaSuiQuattordiciEDichiaraIMancanti()
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(
            new Lines([("Русский", new PrintFont("Helvetica", 12))]), stream);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDF012");
    }

    [Fact]
    public void IlDocumentoLungoSiScriveInTempoRagionevole()
    {
        if (Family("Arial", "Helvetica", "DejaVu Sans", "Liberation Sans") is not { } family) return;
        var font = new PrintFont(family, 11);
        var lines = Enumerable.Range(0, 25).Select(row => ($"Riga {row}: il testo lungo si ripete, perché così è più facile — ΑΒΓ, ДЖЗ.", font)).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var (file, _) = Write(new Lines(lines, pages: 300));

        Assert.True(watch.ElapsedMilliseconds < 15000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Single(new PdfProbe(file).EmbeddedFontNames.Distinct());
    }
}
