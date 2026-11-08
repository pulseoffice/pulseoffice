using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class NumberFormatRegressionTests
{
    private static readonly DateOnly Epoch = new(1899, 12, 30);

    private static NumberFormatter Formatter(CultureInfo? culture = null) =>
        new(culture ?? CultureInfo.InvariantCulture, serial => Epoch.ToDateTime(TimeOnly.MinValue).AddDays(serial));

    private static string Format(double value, string code) =>
        Formatter().Format(CellValue.Number(value), new NumberFormatParser().Parse(code)).Text;

    [Fact]
    public void UnLetteraleConLaEDopoINumeriNonRendeScientificoIlFormato()
    {
        Assert.Equal(FormatValueKind.Number, new NumberFormatParser().Parse("0.00\" EUR\"").Sections[0].ValueKind);
        Assert.Equal("5.00 EUR", Format(5, "0.00\" EUR\""));
    }

    [Fact]
    public void UnLetteraleConLettereDiDataNonRendeDataIlFormato() =>
        Assert.Equal("1,234 pezzi", Format(1234, "#,##0\" pezzi\""));

    [Fact]
    public void ColoreELetteraliNonSiStampanoDueVolteNelleDate()
    {
        var result = Formatter().Format(CellValue.Number(45292), new NumberFormatParser().Parse("[RED]\"Il \"dd/mm/yyyy"));
        Assert.Equal("Il 01/01/2024", result.Text);
        Assert.Equal("Red", result.ColorName);
    }

    [Theory]
    [InlineData("DD/MM/YYYY", "01/01/2024")]
    [InlineData("D/M/YY", "1/1/24")]
    public void IGettoniDataMaiuscoliSonoRiconosciuti(string code, string expected) =>
        Assert.Equal(expected, Format(45292, code));

    [Fact]
    public void IGettoniTedeschiTradottiProduconoLaData()
    {
        var format = new NumberFormatParser().Parse("TT.MM.JJJJ", "de", new FormatCodeVocabulary());
        Assert.Equal("01.01.2024", Formatter().Format(CellValue.Number(45292), format).Text);
    }

    [Fact]
    public void LaEmmePrimaDeiSecondiIndicaIMinuti() =>
        Assert.Equal("30:15", Format(45292 + (30 * 60 + 15) / 86400.0, "mm:ss"));

    [Theory]
    [InlineData(45292.75, "h:mm AM/PM", "6:00 PM")]
    [InlineData(45292.25, "h:mm A/P", "6:00 A")]
    [InlineData(1.5, "[h]:mm", "36:00")]
    [InlineData(0.5, "[mm]", "720")]
    [InlineData(0.01, "[ss]", "864")]
    public void MeridianiEOreTrascorseSiRendono(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code));

    [Fact]
    public void ILAmPmItalianoNonDiventaAnno()
    {
        var format = new NumberFormatParser().Parse("h:mm AM/PM", "it", new FormatCodeVocabulary());
        Assert.Equal("6:00 PM", Formatter().Format(CellValue.Number(0.75), format).Text);
    }

    [Fact]
    public void BarraEDuePuntiRestanoLetteraliInOgniLingua()
    {
        var text = Formatter(new CultureInfo("de-DE")).Format(CellValue.Number(45292.5), new NumberFormatParser().Parse("dd/mm/yyyy hh:mm")).Text;
        Assert.Equal("01/01/2024 12:00", text);
    }

    [Fact]
    public void LaSezioneNegativaEsplicitaUsaIlValoreAssoluto() =>
        Assert.Equal("(5.00)", Format(-5, "0.00;(0.00)"));

    [Theory]
    [InlineData(1.5, "0.##", "1.5")]
    [InlineData(1.257, "0.0#", "1.26")]
    public void IDecimaliFacoltativiNonSiRiempiono(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code));

    [Theory]
    [InlineData(1234, "0E+00", "1E+03")]
    [InlineData(1234, "0.0E+0", "1.2E+3")]
    [InlineData(45123.375, "##0.0E+0", "45.1E+3")]
    [InlineData(1234567, "##0.0E+0", "1.2E+6")]
    [InlineData(0.00123, "##0.0E+0", "1.2E-3")]
    [InlineData(-123456, "##0.0E+0", "-123.5E+3")]
    public void LaNotazioneScientificaSegueIlCodice(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code));

    [Theory]
    [InlineData(0.9999, "# ?/?", "1")]
    [InlineData(2.9999, "# ?/?", "3")]
    [InlineData(3, "# ?/?", "3")]
    [InlineData(1.375, "# ?/8", "1 3/8")]
    [InlineData(0.333, "# ??/??", "1/3")]
    public void LeFrazioniArrotondanoESeguonoIlDenominatore(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code).Trim());

    [Theory]
    [InlineData("it-IT", "VERO")]
    [InlineData("en-US", "TRUE")]
    [InlineData("de-DE", "WAHR")]
    public void IBooleaniSeguonoLaLingua(string culture, string expected) =>
        Assert.Equal(expected, Formatter(new CultureInfo(culture)).Format(CellValue.Boolean(true), NumberFormat.General).Text);

    [Fact]
    public void UnFormatoTestualeAUnaSezioneSiApplicaAlTesto() =>
        Assert.Equal("Tot: abc",
            Formatter().Format(CellValue.Text("abc"), new NumberFormatParser().Parse("\"Tot: \"@")).Text);

    [Theory]
    [InlineData("m/d/yy", "7/27/15")]
    [InlineData("mm/dd/yyyy", "07/27/2015")]
    public void IlMeseInTestaAllaDataNonEUnNumero(string code, string expected) =>
        Assert.Equal(expected, Format(42212, code));
}

public sealed class CellPresenterTests
{
    [Fact]
    public void UnFormatoPersonalizzatoSiLeggeDalCatalogoDelDocumento()
    {
        var workbook = Workbook.CreateDefault();
        var formatId = workbook.NumberFormats.Register("m/d/yy");
        var styleId = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = formatId });
        var presenter = new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture,
            serial => new DateOnly(1899, 12, 30).ToDateTime(TimeOnly.MinValue).AddDays(serial)));

        var shown = presenter.Present(new Cell { Value = CellValue.Number(42212), StyleId = styleId }, workbook.Styles, workbook.NumberFormats, 12);

        Assert.Equal("7/27/15", shown.Text);
    }
}
