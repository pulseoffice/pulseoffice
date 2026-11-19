using System.Text;
using System.Text.RegularExpressions;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round252PdfFormFieldTests
{
    private sealed class Painted(int pages, Action<int, IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas) => paint(index, canvas);
    }

    private static readonly DocumentRect Box = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(120), Twip.FromPoints(14));

    private static string Write(int pages, Action<int, IPageCanvas> paint, bool tagged = false)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics()))
            .Write(new Painted(pages, paint), stream, new PdfExportOptions("Prova", Compress: false) { Tagged = tagged });
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void A_text_widget_appearance_paints_its_value_over_an_opaque_background()
    {
        var pdf = Write(1, (_, canvas) => canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text) { Value = "Anna" }));

        Assert.Contains("1 g 0 0 120 14 re f /Tx BMC", pdf, StringComparison.Ordinal);
        Assert.Contains("/Helv 10 Tf 0 g", pdf, StringComparison.Ordinal);
        Assert.Contains("(Anna) Tj ET Q EMC", pdf, StringComparison.Ordinal);
        Assert.Contains("/Resources << /Font << /Helv", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/NeedAppearances", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void A_field_across_pages_is_one_parent_field_with_a_widget_per_page()
    {
        var pdf = Write(2, (page, canvas) => canvas.MarkField(Box,
            new PrintFormField("Note", PrintFormFieldKind.Text) { Value = page == 0 ? "Prima " : "seconda", MultiLine = true, Identity = "c1" }));

        Assert.DoesNotContain("/T (Note_2)", pdf, StringComparison.Ordinal);
        var parent = Regex.Match(pdf, @"(\d+) 0 obj\n<< /T \(Note\)[^\n]*/V \(Prima seconda\)[^\n]*/Kids \[(\d+) 0 R (\d+) 0 R\]");
        Assert.True(parent.Success);
        Assert.Equal(2, Regex.Matches(pdf, "/Parent " + parent.Groups[1].Value + " 0 R").Count);
        Assert.Contains("/Fields [" + parent.Groups[1].Value + " 0 R]", pdf, StringComparison.Ordinal);
        Assert.Contains("(Prima ) Tj", pdf, StringComparison.Ordinal);
        Assert.Contains("(seconda) Tj", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Fields_without_an_identity_that_share_a_name_stay_separate()
    {
        var pdf = Write(2, (_, canvas) => canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text)));

        Assert.Contains("/T (Nome)", pdf, StringComparison.Ordinal);
        Assert.Contains("/T (Nome_2)", pdf, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<< /T [^\n]*/Kids", pdf);
    }

    [Fact]
    public void A_tagged_form_element_is_a_child_of_the_paragraph_it_sits_in()
    {
        var pdf = Write(1, (_, canvas) =>
        {
            canvas.Tag(new PrintTag("p1", PrintRole.Paragraph));
            canvas.DrawText("Nome: Anna", Box, PrintFont.Default, PrintColor.Black);
            canvas.Tag(null);
            canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text) { Value = "Anna" });
        }, tagged: true);

        var paragraph = Regex.Match(pdf, @"(\d+) 0 obj\n<< /Type /StructElem /S /P ").Groups[1].Value;
        Assert.NotEqual(string.Empty, paragraph);
        Assert.Matches(@"<< /Type /StructElem /S /Form [^\n]*/P " + paragraph + " 0 R", pdf);
    }
}
