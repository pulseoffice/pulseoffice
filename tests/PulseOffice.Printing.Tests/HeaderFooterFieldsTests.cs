using PulseOffice.Foundation.Pages;
using System.Globalization;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public class HeaderFooterFieldsTests
{
    private static readonly IHeaderFooterFields Fields =
        new HeaderFooterFields(CultureInfo.GetCultureInfo("it-IT"));

    private static readonly PageContext Context = new(
        PageNumber: 3, PageCount: 7, DocumentName: "Conti.ods", SectionName: "Foglio1",
        PrintedAt: new DateTimeOffset(2026, 9, 23, 8, 30, 0, TimeSpan.FromHours(2)));

    [Theory]
    [InlineData("&P", "3")]
    [InlineData("&N", "7")]
    [InlineData("&F", "Conti.ods")]
    [InlineData("&A", "Foglio1")]
    [InlineData("Pagina &P di &N", "Pagina 3 di 7")]
    public void ICampiDiventanoValori(string pattern, string expected)
    {
        Assert.Equal(expected, Fields.Expand(pattern, Context));
    }

    [Fact]
    public void LaECommercialeRaddoppiataRestaTesto()
    {
        Assert.Equal("Costi & ricavi", Fields.Expand("Costi && ricavi", Context));
    }

    [Fact]
    public void UnCampoSconosciutoRestaComEra()
    {
        Assert.Equal("&Q", Fields.Expand("&Q", Context));
    }

    [Theory]
    [InlineData("&BBilancio&B", "Bilancio")]
    [InlineData("&\"Arial,Bold\"&14Titolo", "Titolo")]
    [InlineData("&KFF0000Rosso &I&P", "Rosso 3")]
    [InlineData("&G", "")]
    [InlineData("&Z&F", "Conti.odsConti.ods")]
    public void I_codici_di_formato_di_Excel_non_finiscono_stampati(string pattern, string expected)
    {
        Assert.Equal(expected, Fields.Expand(pattern, Context));
    }

    [Fact]
    public void LaDataSegueLaLinguaDellUtente()
    {
        Assert.Equal("23/09/2026", Fields.Expand("&D", Context));
    }

    [Fact]
    public void UnaECommercialeFinaleNonFaCadereNulla()
    {
        Assert.Equal("Totale&", Fields.Expand("Totale&", Context));
    }
}
