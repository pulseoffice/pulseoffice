using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class NumberFormatTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static readonly DateOnly Epoch = new(1899, 12, 30);

    private static NumberFormatter Formatter() =>
        new(Culture, serial => Epoch.ToDateTime(TimeOnly.MinValue).AddDays(serial));

    private static string Format(double value, string code) =>
        Formatter().Format(CellValue.Number(value), new NumberFormatParser().Parse(code)).Text;

    [Theory]
    [InlineData(1e12, "dd/mm/yyyy", "#####")]
    [InlineData(double.PositiveInfinity, "0.00", "#NUM!")]
    [InlineData(double.NaN, "dd/mm/yyyy", "#NUM!")]
    public void ValuesOutsideTheFormatRangeDoNotThrow(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code));

    [Fact]
    public void IlFormatoGeneraleSiRiconosceDaiSuoiNomi()
    {
        Assert.True(new NumberFormatParser().Parse("General").IsGeneral);
        Assert.True(new NumberFormatParser().Parse("Standard").IsGeneral);
        Assert.True(new NumberFormatParser().Parse("").IsGeneral);
    }

    [Fact]
    public void IlCodiceSiDivideInSezioni()
    {
        var format = new NumberFormatParser().Parse("#,##0.00;[RED]-#,##0.00;\"-\";@");
        Assert.Equal(4, format.Sections.Count);
        Assert.Equal("Red", format.Sections[1].ColorName);
    }

    [Fact]
    public void UnPuntoEVirgolaFraVirgoletteNonDivideLeSezioni()
    {
        var format = new NumberFormatParser().Parse("\"a;b\"0");
        Assert.Single(format.Sections);
    }

    [Theory]
    [InlineData("0.00", 2)]
    [InlineData("0.000", 3)]
    [InlineData("0", 0)]
    [InlineData("#,##0.0", 1)]
    public void SiContanoLeCifreDecimaliRichieste(string code, int expected) =>
        Assert.Equal(expected, new NumberFormatParser().Parse(code).Sections[0].DecimalPlaces);

    [Fact]
    public void IlSeparatoreDelleMigliaiaSiDistingueDaQuelloDecimale()
    {
        Assert.True(new NumberFormatParser().Parse("#,##0").Sections[0].UseThousandSeparator);
        Assert.False(new NumberFormatParser().Parse("0.00").Sections[0].UseThousandSeparator);
    }

    [Fact]
    public void LeSezioniConCondizioneHannoLaPrecedenzaSullaRegolaPosizionale()
    {
        var format = new NumberFormatParser().Parse("[>100]\"alto\";[<10]\"basso\";\"medio\"");

        Assert.Equal("alto", format.Select(500).Prefix);
        Assert.Equal("basso", format.Select(5).Prefix);
        Assert.Equal("medio", format.Select(50).Prefix);
    }

    [Theory]
    [InlineData(1234.5, "0.00", "1234.50")]
    [InlineData(1234.5, "#,##0.00", "1,234.50")]
    [InlineData(1234.5, "#,##0", "1,235")]
    [InlineData(0.256, "0%", "26%")]
    [InlineData(0.256, "0.00%", "25.60%")]
    public void IlNumeroSiFormattaSecondoIlCodice(double value, string code, string expected) =>
        Assert.Equal(expected, Format(value, code));

    [Fact]
    public void ISeparatoriInCodaDividonoPerMille()
    {
        Assert.Equal("1235", Format(1_234_567, "0,"));
        Assert.Equal("1", Format(1_234_567, "0,,"));
    }

    [Fact]
    public void LaSezioneDeiNegativiSiApplicaAiNegativi()
    {
        var format = new NumberFormatParser().Parse("0.00;[RED]-0.00");
        var result = Formatter().Format(CellValue.Number(-5), format);

        Assert.Equal("Red", result.ColorName);
        Assert.Contains("5.00", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void IlTestoLetteraleRestaFuoriDalNumero()
    {
        Assert.Equal("1,234.50 €", Format(1234.5, "#,##0.00 \"€\""));
    }

    [Fact]
    public void LeDateSiFormattanoDalSerialeNumerico() =>

        Assert.Equal("01/01/2024", Format(45292, "dd/mm/yyyy"));

    [Fact]
    public void UnCodiceDataScrittoInItalianoPassaDalVocabolario()
    {
        var format = new NumberFormatParser().Parse("gg/mm/aaaa", "it", new FormatCodeVocabulary());
        Assert.Equal("01/01/2024", Formatter().Format(CellValue.Number(45292), format).Text);
    }

    [Fact]
    public void DopoLOraLaEmmeIndicaIMinutiNonIMesi()
    {
        var text = Format(45292.5, "h:mm");
        Assert.Equal("12:00", text);
    }

    [Fact]
    public void UnSerialeNegativoNonEUnaDataValida() =>
        Assert.Equal("#####", Format(-1, "dd/mm/yyyy"));

    [Fact]
    public void GliErroriNonSiFormattano()
    {
        var result = Formatter().Format(
            CellValue.FromError(CellError.DivideByZero), new NumberFormatParser().Parse("0.00"));

        Assert.Equal("#DIV/0!", result.Text);
        Assert.Equal("Red", result.ColorName);
    }

    [Fact]
    public void UnaCellaVuotaNonProduceTesto() =>
        Assert.Equal(string.Empty, Formatter().Format(CellValue.Empty, NumberFormat.General).Text);

    [Fact]
    public void IlFormatoGeneraleAdattaLeCifreAllaLarghezza()
    {
        var stretta = Formatter().Format(CellValue.Number(3.14159265), NumberFormat.General, 6).Text;
        var larga = Formatter().Format(CellValue.Number(3.14159265), NumberFormat.General, 15).Text;

        Assert.True(stretta.Length <= larga.Length);
        Assert.StartsWith("3.14", larga, StringComparison.Ordinal);
    }

    [Fact]
    public void IlFormatoGeneralePassaAllaNotazioneScientificaQuandoServe()
    {
        var text = Formatter().Format(CellValue.Number(1.23e20), NumberFormat.General, 10).Text;
        Assert.Contains("E+", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LeFrazioniSiApprossimanoConDenominatoreLimitato()
    {
        Assert.Equal(" 1/2", Format(0.5, "# ?/?"));
        Assert.Equal("1 1/4", Format(1.25, "# ?/?"));
    }

    [Fact]
    public void GliIndiciPredefinitiSonoQuelliDellaSpecifica()
    {
        var catalog = new NumberFormatCatalog();

        Assert.True(catalog.Get(0).IsGeneral);
        Assert.Equal(2, catalog.Get(2).Sections[0].DecimalPlaces);
        Assert.True(catalog.Get(4).Sections[0].UseThousandSeparator);
    }

    [Fact]
    public void UnIndiceSconosciutoRicadeSulFormatoGenerale() =>
        Assert.True(new NumberFormatCatalog().Get(9999).IsGeneral);

    [Fact]
    public void RegistrareUnCodiceGiaPredefinitoNeRiusaLIndice()
    {
        var catalog = new NumberFormatCatalog();
        Assert.Equal(2, catalog.Register("0.00"));
    }

    [Fact]
    public void IFormatiDellUtentePartonoDaCentoSessantaQuattro()
    {
        var catalog = new NumberFormatCatalog();

        var first = catalog.Register("0.0000 \"unità\"");
        var second = catalog.Register("[>0]\"su\";\"giù\"");

        Assert.Equal(NumberFormatCatalog.FirstCustomId, first);
        Assert.Equal(NumberFormatCatalog.FirstCustomId + 1, second);

        Assert.Equal(first, catalog.Register("0.0000 \"unità\""));
    }
}
