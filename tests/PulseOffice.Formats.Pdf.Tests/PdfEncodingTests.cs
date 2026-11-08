using PulseOffice.Formats.Pdf;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfEncodingTests
{
    [Fact]
    public void UnaccentoCombinatoDiventaLaLetteraAccentata() =>
        Assert.Equal("(caffé)", PdfEncoding.Literal("caffé"));

    [Fact]
    public void UnSegnoCheNonSiComponeSparisceInveceDiDiventareUnPuntoInterrogativo() =>
        Assert.Equal("(q)", PdfEncoding.Literal("q́"));

    [Fact]
    public void LaLarghezzaDellAccentoCombinatoNonSiConta()
    {
        var metrics = new PdfFontMetrics();
        Assert.Equal(metrics.WidthOf("caffé", PdfStandardFont.Helvetica, 10),
            metrics.WidthOf("caffé", PdfStandardFont.Helvetica, 10));
    }
}

public sealed class PdfTextStringTests
{
    [Fact]
    public void IlTitoloConEuroEAccentiVaInUtf16() =>
        Assert.Equal("<FEFF20AC00E0>", PdfFile.TextString("€à"));

    [Fact]
    public void UnTitoloAsciiRestaUnaStringaLetterale() =>
        Assert.Equal("(Conti \\(2026\\))", PdfFile.TextString("Conti (2026)"));
}

public sealed class PdfGradientTests
{
    [Fact]
    public void UnaFormaConLaSolaSfumaturaSiDisegna()
    {
        var canvas = new PdfContentCanvas(
            new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(12000), new PulseOffice.Foundation.Units.Twip(12000)),
            new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics()).Create());

        canvas.DrawPath(new PulseOffice.Printing.PrintPath(
            new PulseOffice.Drawing.Shapes.OutlineBuilder().Ellipse(0.5, 0.5, 0.5, 0.5).Build(),
            new PulseOffice.Foundation.Units.DocumentRect(new(1000), new(1000), new(3000), new(2000)))
        {
            Gradient = new PulseOffice.Printing.PrintGradient(new(255, 0, 0), new(0, 0, 255), 0, false),
        });

        Assert.Single(canvas.UsedPatterns);
        Assert.Contains("f\n", canvas.Content, StringComparison.Ordinal);
    }
}

public sealed class PdfRotatedGradientTests
{
    [Fact]
    public void LaSfumaturaRuotaConLaForma()
    {
        var canvas = new PdfContentCanvas(
            new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(12000), new PulseOffice.Foundation.Units.Twip(12000)),
            new PulseOffice.Formats.Pdf.Fonts.StandardPdfFonts(new PdfFontMetrics()).Create());

        canvas.DrawPath(new PulseOffice.Printing.PrintPath(
            new PulseOffice.Drawing.Shapes.OutlineBuilder().Ellipse(0.5, 0.5, 0.5, 0.5).Build(),
            new PulseOffice.Foundation.Units.DocumentRect(new(1000), new(1000), new(3000), new(2000)))
        {
            Gradient = new PulseOffice.Printing.PrintGradient(new(255, 0, 0), new(0, 0, 255), 0, false),
            RotationDegrees = 90,
        });

        var pattern = Assert.Single(canvas.UsedPatterns).Value;
        Assert.Contains("/Matrix [0 -1 -1 0 ", pattern, StringComparison.Ordinal);
    }

    [Fact]
    public void LaComposizioneDelleMatriciSegueLOrdineDelPdf()
    {
        var moved = new PdfMatrix(1, 0, 0, 1, 10, 0).Then(new PdfMatrix(2, 0, 0, 2, 0, 0));

        Assert.Equal(new PdfMatrix(2, 0, 0, 2, 20, 0), moved);
    }
}
