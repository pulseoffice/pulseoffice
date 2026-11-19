using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round238PageLabelTests
{
    private sealed class Labelled : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 4;

        public PrintPageLabel? PageLabel(int index) =>
            index < 2 ? new PrintPageLabel(PageLabelStyle.LowerRoman, index + 1) : new PrintPageLabel(PageLabelStyle.Decimal, index - 1);

        public void PaintPage(int index, IPageCanvas canvas) { }
    }

    [Fact]
    public void Ranges_start_where_style_or_numbering_changes()
    {
        var labels = new PdfPageLabels().Write([new PrintPageLabel(PageLabelStyle.LowerRoman, 1), new PrintPageLabel(PageLabelStyle.LowerRoman, 2),
            new PrintPageLabel(PageLabelStyle.Decimal, 1), new PrintPageLabel(PageLabelStyle.Decimal, 5, "A-")]);

        Assert.Equal("<< /Nums [ 0 << /S /r >> 2 << /S /D >> 3 << /S /D /P (A-) /St 5 >> ] >>", labels);
    }

    [Fact]
    public void Documents_without_labels_write_none() =>
        Assert.Null(new PdfPageLabels().Write([null, null]));

    [Fact]
    public void The_exporter_writes_the_labels_in_the_catalog()
    {
        using var stream = new MemoryStream();
        Assert.True(new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(new Labelled(), stream, new PdfExportOptions("Labels", Compress: false)).Succeeded);

        Assert.Contains("/PageLabels << /Nums [ 0 << /S /r >> 2 << /S /D >> ] >>", System.Text.Encoding.Latin1.GetString(stream.ToArray()), StringComparison.Ordinal);
    }
}
