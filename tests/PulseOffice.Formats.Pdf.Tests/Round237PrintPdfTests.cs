using System.Text;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round237PrintPdfTests
{
    private sealed class MixedPages : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 2;

        public DocumentSize PageSize(int index) => index == 1
            ? new DocumentSize(Setup.PageHeight, Setup.PageWidth)
            : new DocumentSize(Setup.PageWidth, Setup.PageHeight);

        public void PaintPage(int index, IPageCanvas canvas) =>
            canvas.FillRectangle(new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(10), Twip.FromPoints(10)), PrintColor.Black);
    }

    private sealed class Painted(Action<int, IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) => paint(index, canvas);
    }

    private readonly ShapeOutline _square = new([new ShapeFigure([
        new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 0)),
        new LineSegment(new ShapePoint(1, 1)), new LineSegment(new ShapePoint(0, 1)), CloseSegment.Instance,
    ])]);

    private static string Write(IPrintableDocument document, PdfExportOptions options)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(document, stream, options);
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    private static string Box(DocumentSize size) =>
        $"/MediaBox [0 0 {size.Width.ToPoints().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {size.Height.ToPoints().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}]";

    [Fact]
    public void Each_page_keeps_its_own_size_and_orientation()
    {
        var document = new MixedPages();

        var file = Write(document, new PdfExportOptions("Mixed", Compress: false));

        Assert.Contains(Box(document.PageSize(0)), file, StringComparison.Ordinal);
        Assert.Contains(Box(document.PageSize(1)), file, StringComparison.Ordinal);
    }

    [Fact]
    public void One_up_printing_keeps_a_landscape_page_landscape()
    {
        var document = new MixedPages();
        var imposed = new PrintImposition(new PageRangeParser()).Impose(document, new PrintJobOptions(), nativeCopies: true);

        Assert.Equal(document.PageSize(1), imposed.PageSize(1));
    }

    [Fact]
    public void Close_opacities_keep_their_own_graphics_states()
    {
        var shapes = new Painted((_, canvas) =>
        {
            foreach (var opacity in new[] { 0.496, 0.504 })
                canvas.DrawPath(new PrintPath(_square, new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromPoints(20), Twip.FromPoints(20)))
                {
                    Fill = PrintColor.Black,
                    FillOpacity = opacity,
                });
        });

        var file = Write(shapes, new PdfExportOptions("Alpha", Compress: false));

        Assert.Contains("/ca 0.496", file, StringComparison.Ordinal);
        Assert.Contains("/ca 0.504", file, StringComparison.Ordinal);
    }
}
