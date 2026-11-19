using System.Text;
using System.Text.RegularExpressions;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round247TaggedPdfTests
{
    private sealed class Painted(Action<IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) => paint(canvas);
    }

    private static readonly DocumentRect Line = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(200), Twip.FromPoints(14));

    private static readonly DocumentRect Below = new(Twip.FromPoints(50), Twip.FromPoints(300), Twip.FromPoints(200), Twip.FromPoints(14));

    private readonly ShapeOutline _square = new([new ShapeFigure([
        new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 0)),
        new LineSegment(new ShapePoint(1, 1)), new LineSegment(new ShapePoint(0, 1)), CloseSegment.Instance,
    ])]);

    private static string Write(Action<IPageCanvas> paint)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(new Painted(paint), stream, new PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    private static string Element(string pdf, string role) =>
        Regex.Match(pdf, @"(\d+) 0 obj\n<< /Type /StructElem /S /" + role + " [^\n]*").Value;

    private static string Id(string element) => element[..element.IndexOf(' ', StringComparison.Ordinal)];

    [Fact]
    public void A_link_without_a_tagged_run_joins_the_cell_it_covers()
    {
        var pdf = Write(canvas =>
        {
            var row = new PrintTag("tr", PrintRole.TableRow, new PrintTag("t", PrintRole.Table));
            canvas.Tag(new PrintTag("td1", PrintRole.TableCell, row));
            canvas.DrawText("Nord", Line, PrintFont.Default, PrintColor.Black);
            canvas.Tag(new PrintTag("td2", PrintRole.TableCell, row));
            canvas.DrawText("Sud", Below, PrintFont.Default, PrintColor.Black);
            canvas.Tag(null);
            canvas.MarkLink(Below, "https://example.org");
        });

        var cells = Regex.Matches(pdf, @"(\d+) 0 obj\n<< /Type /StructElem /S /TD ").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(2, cells.Count);
        Assert.Contains($"/P {cells[1]} 0 R", Element(pdf, "Link"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_rotated_figure_box_covers_the_turned_shape_and_a_table_gets_a_box()
    {
        var pdf = Write(canvas =>
        {
            canvas.Tag(new PrintTag("fig", PrintRole.Figure) { Alternate = "Freccia" });
            canvas.DrawPath(new PrintPath(_square, Line) { Fill = PrintColor.Black, RotationDegrees = 90 });
            canvas.Tag(new PrintTag("td", PrintRole.TableCell, new PrintTag("tr", PrintRole.TableRow, new PrintTag("t", PrintRole.Table))));
            canvas.DrawText("Nord", Below, PrintFont.Default, PrintColor.Black);
        });

        Assert.Contains("/BBox [143 ", Element(pdf, "Figure"), StringComparison.Ordinal);
        Assert.Contains("/BBox [50 ", Element(pdf, "Table"), StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, Id(Element(pdf, "Table")));
    }
}
