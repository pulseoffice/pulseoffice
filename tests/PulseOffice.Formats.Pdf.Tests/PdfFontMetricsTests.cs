using PulseOffice.Formats.Pdf;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public class PdfFontMetricsTests
{
    private static readonly IPdfFontMetrics Metrics = new PdfFontMetrics();

    [Theory]
    [InlineData("Arial", false, false, PdfStandardFont.Helvetica)]
    [InlineData("Calibri", true, false, PdfStandardFont.HelveticaBold)]
    [InlineData("Times New Roman", false, true, PdfStandardFont.TimesItalic)]
    [InlineData("Courier New", true, true, PdfStandardFont.CourierBoldOblique)]
    [InlineData("Carattere inesistente", false, false, PdfStandardFont.Helvetica)]
    public void OgniCarattereRicadeSuUnoDeiQuattordiciStandard(
        string family, bool bold, bool italic, PdfStandardFont expected)
    {
        Assert.Equal(expected, Metrics.Resolve(family, bold, italic));
    }

    [Fact]
    public void LeLarghezzeSonoQuelleDichiarateDaAdobe()
    {
        Assert.Equal(0.944, Metrics.WidthOf("W", PdfStandardFont.Helvetica, 1), 3);
        Assert.Equal(0.556, Metrics.WidthOf("0", PdfStandardFont.Helvetica, 1), 3);
        Assert.Equal(0.278, Metrics.WidthOf(" ", PdfStandardFont.Helvetica, 1), 3);
    }

    [Fact]
    public void ILettereAccentateMisuranoComeLeLoroBasi()
    {
        Assert.Equal(
            Metrics.WidthOf("e", PdfStandardFont.Helvetica, 10),
            Metrics.WidthOf("é", PdfStandardFont.Helvetica, 10), 3);
    }

    [Fact]
    public void IlCourierEAPassoFisso()
    {
        Assert.Equal(
            Metrics.WidthOf("i", PdfStandardFont.Courier, 10),
            Metrics.WidthOf("W", PdfStandardFont.Courier, 10), 3);
    }

    [Fact]
    public void IlGrassettoENormalmentePiuLargo()
    {
        Assert.True(
            Metrics.WidthOf("Totale", PdfStandardFont.HelveticaBold, 10)
            > Metrics.WidthOf("Totale", PdfStandardFont.Helvetica, 10));
    }

    [Fact]
    public void UnTestoVuotoNonMisuraNulla()
    {
        Assert.Equal(0, Metrics.WidthOf("", PdfStandardFont.Helvetica, 12));
    }
}
