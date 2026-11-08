using System.Globalization;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SystemDateFormatTests
{
    private readonly SystemDateCode _codes = new();

    [Theory]
    [InlineData("it-IT", "dd/mm/yyyy")]
    [InlineData("en-US", "m/d/yyyy")]
    [InlineData("de-DE", "dd.mm.yyyy")]
    [InlineData("fr-FR", "dd/mm/yyyy")]
    [InlineData("ja-JP", "yyyy/mm/dd")]
    public void LaDataBreveSegueLeImpostazioniLocali(string culture, string expected) =>
        Assert.Equal(expected, _codes.ShortDate(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void IlFormatoQuattordiciSegueLeImpostazioniLocaliNonUnCodiceFisso()
    {
        var american = new NumberFormatCatalog(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("en-US")), _codes);
        var german = new NumberFormatCatalog(new FixedSpreadsheetLocale(CultureInfo.GetCultureInfo("de-DE")), _codes);

        Assert.Equal("m/d/yyyy", american.CodeOf(NumberFormatCatalog.SystemShortDateId));
        Assert.Equal("dd.mm.yyyy h:mm", german.CodeOf(NumberFormatCatalog.SystemDateTimeId));
        Assert.Equal(NumberFormatCatalog.SystemShortDateId, german.Register("dd.mm.yyyy"));
        Assert.NotEqual(NumberFormatCatalog.SystemShortDateId, german.Register("dd/mm/yyyy"));
    }

    [Fact]
    public void CambiareLaLinguaDellInterfacciaNonCambiaIlFormatoData()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("dd/mm/yyyy", new NumberFormatCatalog().CodeOf(NumberFormatCatalog.SystemShortDateId));
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }
}
