using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round239TaggedPdfTests
{
    private sealed class Painted(int pages, Action<int, IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public string? Language { get; init; }

        public void PaintPage(int index, IPageCanvas canvas) => paint(index, canvas);
    }

    private static readonly DocumentRect Line = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14));

    private static string Write(IPrintableDocument document, PdfExportOptions options)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, options);
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    private static void Tagged(int index, IPageCanvas canvas)
    {
        var heading = new PrintTag("h", PrintRole.Heading) { Level = 2 };
        var table = new PrintTag("t", PrintRole.Table);
        var row = new PrintTag("tr", PrintRole.TableRow, table);
        canvas.FillRectangle(Line, PrintColor.Grid);
        canvas.Tag(heading);
        canvas.DrawText("Titolo", Line, PrintFont.Default, PrintColor.Black);
        canvas.Tag(new PrintTag("th", PrintRole.TableHeader, row));
        canvas.DrawText("Nome", Line, PrintFont.Default, PrintColor.Black);
        canvas.Tag(new PrintTag("fig", PrintRole.Figure) { Alternate = "Un grafico" });
        canvas.FillRectangle(Line, PrintColor.Black);
        canvas.Tag(null);
        canvas.DrawLine(new DocumentPoint(Line.Left, Line.Top), new DocumentPoint(Line.Right, Line.Top), PrintColor.Black, Twip.FromPoints(1));
    }

    [Fact]
    public void Tagged_content_writes_a_structure_tree_marked_content_and_the_catalog_entries()
    {
        var pdf = Write(new Painted(1, Tagged), new PdfExportOptions("Prova", Compress: false) { Language = "it-IT" });

        Assert.Contains("/MarkInfo << /Marked true >>", pdf, StringComparison.Ordinal);
        Assert.Contains("/StructTreeRoot", pdf, StringComparison.Ordinal);
        Assert.Contains("/StructParents 0 /Tabs /S", pdf, StringComparison.Ordinal);
        Assert.Contains("/H2 <</MCID 0>> BDC", pdf, StringComparison.Ordinal);
        Assert.Contains("/TH <</MCID 1>> BDC", pdf, StringComparison.Ordinal);
        Assert.Contains("/Figure <</MCID 2>> BDC", pdf, StringComparison.Ordinal);
        Assert.Contains("/Artifact BMC", pdf, StringComparison.Ordinal);
        Assert.Contains("/S /Table", pdf, StringComparison.Ordinal);
        Assert.Contains("/S /TR", pdf, StringComparison.Ordinal);
        Assert.Contains("/S /Document", pdf, StringComparison.Ordinal);
        Assert.Contains("/A << /O /Table /Scope /Column >>", pdf, StringComparison.Ordinal);
        Assert.Contains("/Alt (Un grafico)", pdf, StringComparison.Ordinal);
        Assert.Contains("/RoleMap << /Sheet /Sect /Slide /Sect /Title /H1", pdf, StringComparison.Ordinal);
        Assert.Contains("/Lang (it-IT)", pdf, StringComparison.Ordinal);
        Assert.Equal(Count(pdf, " BDC\n") + Count(pdf, " BMC\n"), Count(pdf, "EMC\n"));
    }

    [Fact]
    public void An_untagged_export_or_a_document_without_tags_writes_no_structure()
    {
        var off = Write(new Painted(1, Tagged), new PdfExportOptions("Prova", Compress: false) { Tagged = false });
        var plain = Write(new Painted(1, (_, canvas) => canvas.DrawText("Testo", Line, PrintFont.Default, PrintColor.Black)), new PdfExportOptions("Prova", Compress: false));

        Assert.DoesNotContain("/StructTreeRoot", off, StringComparison.Ordinal);
        Assert.DoesNotContain("BDC", off, StringComparison.Ordinal);
        Assert.DoesNotContain("/StructTreeRoot", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("/MarkInfo", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void An_archive_with_a_structure_claims_level_a_and_one_without_claims_level_b()
    {
        var tagged = Write(new Painted(1, Tagged), new PdfExportOptions("Prova", Compress: false) { Archive = true });
        var plain = Write(new Painted(1, (_, canvas) => canvas.DrawText("Testo", Line, PrintFont.Default, PrintColor.Black)), new PdfExportOptions("Prova", Compress: false) { Archive = true });

        Assert.Contains("<pdfaid:conformance>A</pdfaid:conformance>", tagged, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void The_document_language_is_used_when_the_options_name_none()
    {
        var pdf = Write(new Painted(1, Tagged) { Language = "de-DE" }, new PdfExportOptions("Prova", Compress: false));

        Assert.Contains("/Lang (de-DE)", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void A_figure_without_alternative_text_is_reported()
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(
            new Painted(1, (_, canvas) =>
            {
                canvas.Tag(new PrintTag("fig", PrintRole.Figure));
                canvas.FillRectangle(Line, PrintColor.Black);
            }), stream, new PdfExportOptions("Prova"));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PDF015");
    }

    [Fact]
    public void Content_continuing_on_a_second_page_joins_the_same_element()
    {
        var pdf = Write(new Painted(2, (_, canvas) =>
        {
            canvas.Tag(new PrintTag("p", PrintRole.Paragraph));
            canvas.DrawText("Testo", Line, PrintFont.Default, PrintColor.Black);
        }), new PdfExportOptions("Prova", Compress: false));

        Assert.Equal(1, Count(pdf, "/S /P "));
        Assert.Equal(2, Count(pdf, "/Type /MCR"));
        Assert.Contains("/StructParents 1 /Tabs /S", pdf, StringComparison.Ordinal);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
