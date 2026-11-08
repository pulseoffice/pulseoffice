using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Pdf;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public class PdfExporterTests
{
    private sealed class Document(int pages, Action<int, IPageCanvas>? paint = null) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas) => paint?.Invoke(index, canvas);
    }

    private static readonly IPdfExporter Exporter = new PdfExporter(new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics()));

    private static byte[] Write(IPrintableDocument document)
    {
        using var stream = new MemoryStream();
        var result = Exporter.Write(document, stream, new PdfExportOptions("Prova", Compress: false));

        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return stream.ToArray();
    }

    private static string Text(byte[] payload) =>
        System.Text.Encoding.Latin1.GetString(payload);

    [Fact]
    public void UnFileVuotoNonSiScrive()
    {
        using var stream = new MemoryStream();
        Assert.False(Exporter.Write(new Document(0), stream).Succeeded);
    }

    [Fact]
    public void IlFileCominciaConLIntestazioneEFinisceConLaFine()
    {
        var text = Text(Write(new Document(1)));

        Assert.StartsWith("%PDF-1.7", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LaTavolaDegliIndirizziContaTuttiGliOggetti()
    {
        var text = Text(Write(new Document(2)));

        var declared = int.Parse(
            text[(text.LastIndexOf("/Size ", StringComparison.Ordinal) + 6)..].Split(' ')[0],
            System.Globalization.CultureInfo.InvariantCulture);

        var written = text.Split(" 0 obj").Length - 1;

        Assert.Equal(written + 1, declared);
    }

    [Fact]
    public void OgniPaginaDiventaUnOggettoPagina()
    {
        var text = Text(Write(new Document(3)));

        Assert.Equal(3, text.Split("/Type /Page ").Length - 1);
        Assert.Contains("/Count 3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IlFormatoDellaPaginaEQuelloDellImpostazione()
    {
        var text = Text(Write(new Document(1)));

        Assert.Contains("/MediaBox [0 0 595.3 841.9]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCaratteresiDichiaraUnaVoltaSola()
    {
        var text = Text(Write(new Document(3, (_, canvas) => canvas.DrawText(
            "Conti", new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(100), Twip.FromPoints(20)),
            PrintFont.Default, PrintColor.Black))));

        Assert.Equal(1, text.Split("/BaseFont /Helvetica").Length - 1);
    }

    [Fact]
    public void UnImmagineDiventaUnXObject()
    {
        var image = new PageImage(2, 2, [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0]);

        var text = Text(Write(new Document(1, (_, canvas) => canvas.DrawImage(
            image, new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(100), Twip.FromPoints(60))))));

        Assert.Contains("/Subtype /Image /Width 2 /Height 2", text, StringComparison.Ordinal);
        Assert.Contains("/ColorSpace /DeviceRGB /BitsPerComponent 8", text, StringComparison.Ordinal);
        Assert.Contains("/XObject << /Img1", text, StringComparison.Ordinal);
        Assert.Contains("/Img1 Do", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnImmagineConTrasparenzaPortaLaSuaMascheraMorbida()
    {
        var image = new PageImage(2, 1, [255, 0, 0, 0, 255, 0]) { Alpha = [255, 64] };

        var text = Text(Write(new Document(1, (_, canvas) => canvas.DrawImage(
            image, new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(100), Twip.FromPoints(60))))));

        Assert.Contains("/ColorSpace /DeviceGray /BitsPerComponent 8", text, StringComparison.Ordinal);
        Assert.Contains("/SMask", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaCorsaDiGlifiEUnSoloTestoConLeCorrezioniDiPosizione()
    {
        var metrics = new PdfFontMetrics();
        var font = new PrintFont("Liberation Serif", 12, false, false);
        var resolved = metrics.Resolve(font.Family, false, false);
        const string text = "Testo sotto";
        var advances = text.Select(character => metrics.WidthOf(character.ToString(), resolved, 12) * 1.03).ToList();
        var document = new Document(1, (_, canvas) => canvas.DrawGlyphs(new PrintGlyphRun(text, 72, 100, font, PrintColor.Black, advances)));

        var content = Text(Write(document));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(content, @"\] TJ"));
        Assert.Contains("(T) -18.33", content, StringComparison.Ordinal);
        if (Environment.GetEnvironmentVariable("PDF_GLYPHS_OUT") is { Length: > 0 } output)
        {
            using var file = File.Create(output);
            Exporter.Write(document, file, new PdfExportOptions("Prova", Compress: false));
        }
    }

    [Fact]
    public void UnImmagineOpacaNonHaMascheraMorbida()
    {
        var image = new PageImage(1, 1, [255, 0, 0]);

        var text = Text(Write(new Document(1, (_, canvas) => canvas.DrawImage(
            image, new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10))))));

        Assert.DoesNotContain("/SMask", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LeParentesiNelTestoNonRomponoIlFile()
    {
        var text = Text(Write(new Document(1, (_, canvas) => canvas.DrawText(
            "Totale (netto)", new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(200), Twip.FromPoints(20)),
            PrintFont.Default, PrintColor.Black))));

        Assert.Contains(@"[(Totale \(netto\))] TJ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnEmojiNelTestoNonFaCadereLEsportazione()
    {
        var text = Text(Write(new Document(1, (_, canvas) => canvas.DrawText(
            "Ok 👍\tfine", new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(400), Twip.FromPoints(20)),
            PrintFont.Default, PrintColor.Black))));

        Assert.Contains("[(Ok ? fine)] TJ", text, StringComparison.Ordinal);
    }
}
