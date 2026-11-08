using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfRound63Tests
{
    private sealed class Painted(int pages, Action<int, IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas) => paint(index, canvas);
    }

    private readonly StrictPdfStructure _structure = new();

    private static byte[] Write(IPrintableDocument document, PdfExportOptions options)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, options);
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return stream.ToArray();
    }

    private static void Rich(int index, IPageCanvas canvas)
    {
        var area = new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14));
        canvas.DrawText($"Capitolo {index + 1} è così", area, PrintFont.Default, PrintColor.Black);
        canvas.MarkHeading(1, $"Capitolo {index + 1}", 80);
        canvas.MarkHeading(2, $"Paragrafo {index + 1}.1", 200);
        canvas.MarkHeading(3, $"Punto {index + 1}.1.1", 300);
        canvas.MarkTarget($"segno{index}", 200);
        canvas.MarkLink(area, index == 0 ? "https://example.org/è" : "#segno0");
        canvas.MarkNote(area, new PrintNote("Anna", "Da rivedere", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(-5))));
        canvas.DrawImage(new PageImage(2, 1, [255, 0, 0, 0, 0, 255]) { Alpha = [255, 128] }, area);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IlFileReggeUnLettoreRigoroso(bool compress, bool archive)
    {
        var file = Write(new Painted(3, Rich), new PdfExportOptions("Prova", "Anna", Compress: compress) { Archive = archive, Language = "it-IT" });

        _structure.Validate(file);
    }

    [Fact]
    public void IlFileCifratoReggeUnLettoreRigoroso()
    {
        var file = Write(new Painted(2, Rich), new PdfExportOptions("Prova", Compress: false) { Security = new PdfSecurityOptions { OpenPassword = "apri" } });

        _structure.Validate(file);
    }

    [Fact]
    public void LeDestinazioniNominateSonoInOrdineDiByte()
    {
        var file = Encoding.Latin1.GetString(Write(new Painted(1, (_, canvas) =>
        {
            canvas.MarkTarget("a!", 100);
            canvas.MarkTarget("a", 200);
            canvas.MarkTarget("ab", 300);
        }), new PdfExportOptions("Prova", Compress: false)));

        var shorter = file.IndexOf("(a) [", StringComparison.Ordinal);
        var bang = file.IndexOf("(a!) [", StringComparison.Ordinal);
        var longer = file.IndexOf("(ab) [", StringComparison.Ordinal);
        Assert.True(shorter >= 0 && shorter < bang && bang < longer);
    }

    [Fact]
    public void IlContoDeiSegnalibriComprendeSoloIVisibili()
    {
        var file = Encoding.Latin1.GetString(Write(new Painted(1, (_, canvas) =>
        {
            canvas.MarkHeading(1, "Uno", 10);
            canvas.MarkHeading(2, "Due", 20);
            canvas.MarkHeading(3, "Tre", 30);
        }), new PdfExportOptions("Prova", Compress: false)));

        Assert.Contains("/Type /Outlines", file, StringComparison.Ordinal);
        Assert.Matches(@"/Type /Outlines /First \d+ 0 R /Last \d+ 0 R /Count 2 >>", file);
        Assert.Contains("/Title (Due) /Parent", file, StringComparison.Ordinal);
        Assert.Matches(@"/Title \(Due\)[^>]*/Count -1", file);
    }

    [Fact]
    public void NumeriNonFinitiNonEntranoNelContenuto()
    {
        var file = Write(new Painted(1, (_, canvas) =>
        {
            canvas.DrawGlyphs(new PrintGlyphRun("Ciao", 72, 72, PrintFont.Default, PrintColor.Black, [6, 6, double.NaN, 6]) { RotationDegrees = double.NaN, HorizontalScale = double.PositiveInfinity });
            canvas.DrawGlyphs(new PrintGlyphRun("Ciao", double.NaN, 72, PrintFont.Default, PrintColor.Black, [6, 6, 6, 6]));
            canvas.DrawText("Ciao", new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14)), PrintFont.Default with { SizePoints = double.NaN }, PrintColor.Black);
            canvas.MarkHeading(1, "Uno", double.NaN);
        }), new PdfExportOptions("Prova", Compress: false));

        _structure.Validate(file);
        var probe = new PdfProbe(file);
        foreach (var id in probe.Objects.Keys)
            if (probe.Stream(id) is { } stream)
                Assert.DoesNotContain("NaN", Encoding.Latin1.GetString(stream), StringComparison.Ordinal);
        Assert.Contains("Ciao", probe.ExtractText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConDataFissaIlFileEDeterministico()
    {
        var options = new PdfExportOptions("Prova", "Anna", Compress: true) { Created = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero) };

        Assert.Equal(Write(new Painted(2, Rich), options), Write(new Painted(2, Rich), options));
    }
}
