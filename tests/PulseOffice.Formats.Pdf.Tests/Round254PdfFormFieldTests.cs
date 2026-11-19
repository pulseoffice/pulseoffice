using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round254PdfFormFieldTests
{
    private sealed class Painted(Action<int, IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 2;

        public void PaintPage(int index, IPageCanvas canvas) => paint(index, canvas);
    }

    private static readonly DocumentRect Box = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(120), Twip.FromPoints(14));

    private static string Write(Func<int, PrintFormField> field)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics()))
            .Write(new Painted((page, canvas) => canvas.MarkField(Box, field(page))), stream, new PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void Fragments_from_different_paragraphs_are_joined_with_a_line_break()
    {
        var pdf = Write(page => new PrintFormField("Note", PrintFormFieldKind.Text)
        {
            Value = page == 0 ? "Prima" : "seconda", MultiLine = true, Identity = "c1",
            FirstSegment = page == 0 ? "p1" : "p2", LastSegment = page == 0 ? "p1" : "p2",
        });

        Assert.Contains("/V " + PdfFile.TextString("Prima\nseconda"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Fragments_of_one_paragraph_broken_across_pages_join_without_a_break()
    {
        var pdf = Write(page => new PrintFormField("Note", PrintFormFieldKind.Text)
        {
            Value = page == 0 ? "Prima " : "seconda", MultiLine = true, Identity = "c1", FirstSegment = "p1", LastSegment = "p1",
        });

        Assert.Contains("/V (Prima seconda)", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void A_text_widget_is_drawn_with_the_control_font_and_size_from_the_document_font_set()
    {
        var pdf = Write(page => new PrintFormField("Nome" + page, PrintFormFieldKind.Text) { Value = "Anna", Font = new PrintFont("Helvetica", 14, Bold: true) });

        var font = System.Text.RegularExpressions.Regex.Match(pdf, @"/(F\d+) 14 Tf \(Anna\) Tj").Groups[1].Value;
        Assert.NotEqual(string.Empty, font);
        Assert.Contains("/DA (/" + font + " 14 Tf 0 g)", pdf, StringComparison.Ordinal);
        Assert.Matches(@"/DR << /Font << /Helv << [^>]*>> /" + font + @" \d+ 0 R", pdf);
        Assert.Matches(@"/" + font + @" (\d+) 0 R >> >>", pdf);
    }
}
