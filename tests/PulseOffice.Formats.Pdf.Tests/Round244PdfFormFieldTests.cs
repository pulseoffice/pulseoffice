using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class Round244PdfFormFieldTests
{
    private sealed class Painted(Action<IPageCanvas> paint) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) => paint(canvas);
    }

    private static readonly DocumentRect Box = new(Twip.FromPoints(50), Twip.FromPoints(80), Twip.FromPoints(120), Twip.FromPoints(14));

    private static string Write(Action<IPageCanvas> paint, PdfExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())).Write(new Painted(paint), stream, options ?? new PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Message);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    [Fact]
    public void Form_fields_become_widgets_listed_in_the_acro_form()
    {
        var pdf = Write(canvas =>
        {
            canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text) { Value = "Anna" });
            canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.CheckBox) { Checked = true });
            canvas.MarkField(Box, new PrintFormField("Città", PrintFormFieldKind.ListBox) { Value = "Roma", Options = ["Roma", "Milano"] });
        });

        Assert.Contains("/AcroForm << /Fields [", pdf, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Widget", pdf, StringComparison.Ordinal);
        Assert.Contains("/FT /Tx /Ff 0 /V (Anna)", pdf, StringComparison.Ordinal);
        Assert.Contains("/T (Nome_2)", pdf, StringComparison.Ordinal);
        Assert.Contains("/AS /Yes /AP << /N << /Yes", pdf, StringComparison.Ordinal);
        Assert.Contains("/V /Yes", pdf, StringComparison.Ordinal);
        Assert.Contains("/FT /Ch /Ff 131072 /Opt [(Roma) (Milano)] /V (Roma)", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void No_fields_and_no_acro_form_when_forms_are_off()
    {
        var pdf = Write(canvas => canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text)),
            new PdfExportOptions("Prova", Compress: false) { Forms = false });

        Assert.DoesNotContain("/AcroForm", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Widget", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Round248TaggedWidgetsAreFormElementsReachedThroughObjectReferences()
    {
        var pdf = Write(canvas => canvas.MarkField(Box, new PrintFormField("Nome", PrintFormFieldKind.Text) { Tooltip = "Il tuo nome" }),
            new PdfExportOptions("Prova", Compress: false) { Tagged = true });

        Assert.Contains("/MK << /BG [1 1 1] >> /StructParent 1", pdf, StringComparison.Ordinal);
        Assert.Contains("/TU (Il tuo nome) /FT /Tx", pdf, StringComparison.Ordinal);
        Assert.Contains("/S /Form", pdf, StringComparison.Ordinal);
        Assert.Contains("/Type /OBJR", pdf, StringComparison.Ordinal);
        Assert.Contains("/ParentTreeNextKey 2", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Round248PictureFieldsArePushButtonsWithAnAppearanceAndUntaggedWidgetsHaveNoStructParent()
    {
        var pdf = Write(canvas => canvas.MarkField(Box, new PrintFormField("Foto", PrintFormFieldKind.Picture)),
            new PdfExportOptions("Prova", Compress: false) { Tagged = false });

        Assert.Contains("/MK << /TP 1 >> /AP << /N ", pdf, StringComparison.Ordinal);
        Assert.Contains("/TU (Foto) /FT /Btn /Ff 65536", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/StructParent", pdf, StringComparison.Ordinal);
    }
}
