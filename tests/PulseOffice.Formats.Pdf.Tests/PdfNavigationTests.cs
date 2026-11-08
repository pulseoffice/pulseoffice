using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfNavigationTests
{
    private sealed class Marked(int pages) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas)
        {
            var area = new DocumentRect(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14));
            canvas.DrawText($"Capitolo {index + 1}", area, PrintFont.Default, PrintColor.Black);
            canvas.MarkHeading(1, $"Capitolo {index + 1}", 80);
            canvas.MarkHeading(2, $"Paragrafo {index + 1}.1", 200);
            canvas.MarkTarget($"segno{index}", 200);
            canvas.MarkLink(area, index == 0 ? "https://example.org/è" : "#segno0");
            canvas.MarkNote(area, new PrintNote("Anna", "Da rivedere", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)));
        }
    }

    private static string Write(IPrintableDocument document, PdfExportOptions options)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, options);
        Assert.True(result.Succeeded);
        return System.Text.Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void ITitoliDiventanoSegnalibriAnnidati()
    {
        var text = Write(new Marked(2), new PdfExportOptions("Prova", Compress: false));

        Assert.Contains("/Type /Outlines", text, StringComparison.Ordinal);
        Assert.Contains("/PageMode /UseOutlines", text, StringComparison.Ordinal);
        Assert.Contains("/Title (Capitolo 2)", text, StringComparison.Ordinal);
        Assert.Contains("/Title (Paragrafo 1.1) /Parent", text, StringComparison.Ordinal);
        Assert.Equal(4, text.Split("/Dest [").Length - 1);
    }

    [Fact]
    public void ICollegamentiEsterniEInterniDiventanoAnnotazioni()
    {
        var text = Write(new Marked(2), new PdfExportOptions("Prova", Compress: false));

        Assert.Contains("/URI (https://example.org/%C3%A8)", text, StringComparison.Ordinal);
        Assert.Contains("/Dest (segno0)", text, StringComparison.Ordinal);
        Assert.Contains("/Names << /Dests", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ICommentiDiventanoNoteConAutoreEAspetto()
    {
        var text = Write(new Marked(1), new PdfExportOptions("Prova", Compress: false));

        Assert.Contains("/Subtype /Text", text, StringComparison.Ordinal);
        Assert.Contains("/T (Anna)", text, StringComparison.Ordinal);
        Assert.Contains("/Contents (Da rivedere)", text, StringComparison.Ordinal);
        Assert.Contains("/AP << /N", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LeOpzioniSpengonoSegnalibriCollegamentiENote()
    {
        var text = Write(new Marked(1), new PdfExportOptions("Prova", Compress: false) { Bookmarks = false, Links = false, Notes = false });

        Assert.DoesNotContain("/Outlines", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Annots", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LIntervalloScriveSoloLePagineScelte()
    {
        var text = Write(new Marked(5), new PdfExportOptions("Prova", Compress: false) { Pages = [1, 3] });

        Assert.Contains("/Count 2", text, StringComparison.Ordinal);
        Assert.Contains("(Capitolo 2)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(Capitolo 3)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IlPdfAPortaMetadatiIntentoDiUscitaEIdentificativo()
    {
        var text = Write(new Marked(1), new PdfExportOptions("Prova", "Anna", Compress: false) { Archive = true, Language = "it-IT" });

        Assert.Contains("<pdfaid:part>2</pdfaid:part>", text, StringComparison.Ordinal);
        Assert.Contains("/OutputIntents [", text, StringComparison.Ordinal);
        Assert.Contains("/S /GTS_PDFA1", text, StringComparison.Ordinal);
        Assert.Contains("/ID [<", text, StringComparison.Ordinal);
        Assert.Contains("/Lang (it-IT)", text, StringComparison.Ordinal);
        Assert.Contains("<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Prova</rdf:li>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IlProfiloSrgbHaLaFirmaEDimensioneGiusta()
    {
        var profile = new SrgbProfile().Bytes();

        Assert.Equal(profile.Length, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(profile));
        Assert.Equal("acsp", System.Text.Encoding.ASCII.GetString(profile, 36, 4));
        Assert.Equal("mntr", System.Text.Encoding.ASCII.GetString(profile, 12, 4));
    }
}
