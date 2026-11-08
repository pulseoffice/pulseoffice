using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Parsing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class FormulaLocalizationTests
{
    private readonly FormulaLocalization _localization = new(new FormulaParser(), new FunctionNameVocabulary());

    [Theory]
    [InlineData("it", "=SOMMA(A1:A2;1,5)", "SUM")]
    [InlineData("de", "=SUMME(A1:A2;1,5)", "SUM")]
    [InlineData("fr", "=SOMME(A1:A2;1,5)", "SUM")]
    [InlineData("es", "=SUMA(A1:A2;1,5)", "SUM")]
    public void UnaFormulaScrittaNellaLinguaSiConservaInFormaCanonicaETornaUguale(string language, string typed, string canonicalName)
    {
        var canonical = _localization.ToCanonical(typed, language);

        Assert.StartsWith("=" + canonicalName + "(", canonical, StringComparison.Ordinal);
        Assert.Contains("1.5", canonical, StringComparison.Ordinal);
        Assert.Equal(typed, _localization.ToDisplay(canonical, language));
    }

    [Fact]
    public void InIngleseEConUnErroreDiSintassiIlTestoResta()
    {
        Assert.Equal("=SUM(A1;A2)", _localization.ToCanonical("=SUM(A1,A2)", "en"));
        Assert.Equal("=SUM((", _localization.ToCanonical("=SUM((", "en"));
        Assert.Equal("=SOMMA((", _localization.ToCanonical("=SOMMA((", "it"));
        Assert.Equal("testo", _localization.ToCanonical("testo", "it"));
    }

    [Theory]
    [InlineData("en", "=SUM(A1:A2,1.5)", "=SUM(A1:A2;1.5)")]
    [InlineData("en", "=SUM({1,2;3,4})", "=SUM({1;2|3;4})")]
    [InlineData("en", "=IF(A1>0,ROUND(A1*2.5,1),\"x\")", "=IF(A1>0;ROUND(A1*2.5;1);\"x\")")]
    [InlineData("it", "=SOMMA({1;2|3;4})", "=SUM({1;2|3;4})")]
    [InlineData("it", "=SE(A1>0;ARROTONDA(A1*2,5;1);\"x\")", "=IF(A1>0;ROUND(A1*2.5;1);\"x\")")]
    public void InIngleseLaVirgolaSeparaGliArgomentiComeInExcelLeAltreLingueTengonoIlPuntoEVirgola(string language, string typed, string canonical)
    {
        Assert.Equal(canonical, _localization.ToCanonical(typed, language));
        Assert.Equal(typed, _localization.ToDisplay(canonical, language));
    }

    [Fact]
    public void LaPunteggiaturaDellInglesESeparatoreVirgolaRigheConPuntoEVirgolaEPuntoDecimale()
    {
        var english = _localization.Punctuation("en");
        Assert.Equal((',', ',', ';', '.'), (english.Argument, english.ArrayColumn, english.ArrayRow, english.Decimal));
        var german = _localization.Punctuation("de");
        Assert.Equal((';', ';', '|', ','), (german.Argument, german.ArrayColumn, german.ArrayRow, german.Decimal));
        Assert.Equal(',', _localization.Punctuation("pt").Argument);
    }

    [Theory]
    [InlineData("en", "=BESSELJ(1.5; 1)", "=BESSELJ(1.5, 1)")]
    [InlineData("it", "=SUM(A1; \"a;b\"; 'Foglio 1'.B2)", "=SOMMA(A1; \"a;b\"; 'Foglio 1'.B2)")]
    [InlineData("de", "=ROUND(Sheet2.A1*2.25; 1)", "=RUNDEN(Sheet2.A1*2,25; 1)")]
    public void LaVisualizzazioneConservaGliSpaziITestiEIRiferimentiAiFogli(string language, string canonical, string shown)
    {
        Assert.Equal(shown, _localization.ToDisplay(canonical, language));
        Assert.Equal(canonical, _localization.ToCanonical(shown, language));
    }

    [Theory]
    [InlineData("it", "=CERCA.VERT(B1;A1:C9;3;FALSO)", "=VLOOKUP(B1;A1:C9;3;FALSE)")]
    [InlineData("it", "=SE(VERO;1;0)", "=IF(TRUE;1;0)")]
    [InlineData("de", "=SVERWEIS(B1;A1:C9;3;FALSCH)", "=VLOOKUP(B1;A1:C9;3;FALSE)")]
    [InlineData("fr", "=SI(VRAI;1;0)", "=IF(TRUE;1;0)")]
    [InlineData("es", "=SI(FALSO;1;0)", "=IF(FALSE;1;0)")]
    public void IValoriLogiciScrittiSenzaParentesiSiTraduconoComeInCalcEdExcel(string language, string typed, string canonical)
    {
        Assert.Equal(canonical, _localization.ToCanonical(typed, language));
        Assert.Equal(typed, _localization.ToDisplay(canonical, language));
    }
}
