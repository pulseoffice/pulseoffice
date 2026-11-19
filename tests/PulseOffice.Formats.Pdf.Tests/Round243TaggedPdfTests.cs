using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round243TaggedPdfTests
{
    private sealed class Painted(Action<IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) => paint(canvas);
    }

    private static readonly DocumentRect Line = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14));

    private static string Write(Action<IPageCanvas> paint)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(new Painted(paint), stream, new PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void Link_and_comment_annotations_join_the_structure_tree()
    {
        var pdf = Write(canvas =>
        {
            var paragraph = new PrintTag("p", PrintRole.Paragraph);
            canvas.Tag(new PrintTag("a", PrintRole.Link, paragraph) { Target = "https://example.org" });
            canvas.DrawText("sito", Line, PrintFont.Default, PrintColor.Black);
            canvas.MarkLink(Line, "https://example.org");
            canvas.MarkNote(Line, new PrintNote("Anna", "Nota"));
        });

        Assert.Contains("/StructParent 1 ", pdf, StringComparison.Ordinal);
        Assert.Contains("/StructParent 2", pdf, StringComparison.Ordinal);
        Assert.Equal(2, Count(pdf, "/Type /OBJR"));
        Assert.Equal(1, Count(pdf, "/S /Link "));
        Assert.Contains("/S /Annot ", pdf, StringComparison.Ordinal);
        Assert.Contains("/ParentTreeNextKey 3", pdf, StringComparison.Ordinal);
        Assert.Contains("/Contents (https://example.org)", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Figures_get_a_bounding_box_cells_their_spans_and_row_headers_their_scope()
    {
        var pdf = Write(canvas =>
        {
            canvas.Tag(new PrintTag("fig", PrintRole.Figure) { Alternate = "Logo" });
            canvas.FillRectangle(Line, PrintColor.Black);
            var row = new PrintTag("tr", PrintRole.TableRow, new PrintTag("t", PrintRole.Table));
            canvas.Tag(new PrintTag("th", PrintRole.TableHeader, row) { Scope = PrintHeaderScope.Row });
            canvas.DrawText("Nord", Line, PrintFont.Default, PrintColor.Black);
            canvas.Tag(new PrintTag("td", PrintRole.TableCell, row) { ColumnSpan = 2, RowSpan = 3 });
            canvas.DrawText("10", Line, PrintFont.Default, PrintColor.Black);
        });

        Assert.Contains("/A << /O /Layout /BBox [50 ", pdf, StringComparison.Ordinal);
        Assert.Contains("/A << /O /Table /Scope /Row >>", pdf, StringComparison.Ordinal);
        Assert.Contains("/A << /O /Table /ColSpan 2 /RowSpan 3 >>", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_scopes_leave_the_paragraph_and_hyphens_carry_actual_text()
    {
        var pdf = Write(canvas =>
        {
            var paragraph = new PrintTag("p", PrintRole.Paragraph);
            canvas.Tag(paragraph);
            canvas.BeginArtifact();
            canvas.FillRectangle(Line, PrintColor.Grid);
            canvas.EndArtifact();
            canvas.DrawText("paro", Line, PrintFont.Default, PrintColor.Black);
            canvas.Tag(new PrintTag("hy", PrintRole.Span, paragraph) { ActualText = string.Empty });
            canvas.DrawText("-", Line, PrintFont.Default, PrintColor.Black);
        });

        Assert.True(pdf.IndexOf("/Artifact BMC", StringComparison.Ordinal) < pdf.IndexOf("/P <</MCID 0>> BDC", StringComparison.Ordinal));
        Assert.Contains("/Span <</MCID 1>> BDC", pdf, StringComparison.Ordinal);
        Assert.Contains("/ActualText ()", pdf, StringComparison.Ordinal);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
