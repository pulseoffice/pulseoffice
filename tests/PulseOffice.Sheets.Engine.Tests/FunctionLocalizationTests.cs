using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Parsing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FunctionLocalizationTests
{
    private readonly FunctionNameVocabulary _vocabulary = new();

    [Fact]
    public void LeCinqueLingueSonoDichiarate() =>
        Assert.Equal(["en", "it", "de", "es", "fr"], _vocabulary.SupportedLanguages);

    [Theory]
    [InlineData("it", "SOMMA")]
    [InlineData("de", "SUMME")]
    [InlineData("es", "SUMA")]
    [InlineData("fr", "SOMME")]
    public void LaSommaSiScriveDiversamenteInOgniLingua(string language, string localized)
    {
        Assert.Equal("SUM", _vocabulary.ToCanonical(localized, language));
        Assert.Equal(localized, _vocabulary.ToLanguage("SUM", language));
    }

    [Theory]
    [InlineData("it", "SE")]
    [InlineData("de", "WENN")]
    [InlineData("es", "SI")]
    [InlineData("fr", "SI")]
    public void AncheIlCondizionaleCambiaNome(string language, string localized) =>
        Assert.Equal("IF", _vocabulary.ToCanonical(localized, language));

    [Fact]
    public void LeFunzioniConNomeUgualeNonSonoInTabella()
    {
        foreach (var name in (string[])["COS", "TAN", "EXP", "LOG"])
            foreach (var language in _vocabulary.SupportedLanguages)
                Assert.Equal(name, _vocabulary.ToLanguage(name, language));
    }

    [Fact]
    public void LeTraduzioniSonoQuelleUfficialiEsterno()
    {
        Assert.Equal("ASS", _vocabulary.ToLanguage("ABS", "it"));
        Assert.Equal("PI.GRECO", _vocabulary.ToLanguage("PI", "it"));
        Assert.Equal("GLÄTTEN", _vocabulary.ToLanguage("TRIM", "de"));
        Assert.Equal("SUPPRESPACE", _vocabulary.ToLanguage("TRIM", "fr"));
        Assert.Equal("RESIDUO", _vocabulary.ToLanguage("MOD", "es"));
    }

    [Fact]
    public void UnNomeSconosciutoRestaComEStato() =>
        Assert.Equal("MIAFUNZIONE", _vocabulary.ToCanonical("MIAFUNZIONE", "it"));

    [Fact]
    public void LaConversioneNonDistingueMaiuscoleEMinuscole() =>
        Assert.Equal("SUM", _vocabulary.ToCanonical("somma", "it"));

    [Fact]
    public void OgniLinguaHaCentinaiaDiNomiTradotti()
    {
        foreach (var language in (string[])["it", "de", "es", "fr"])
            Assert.True(_vocabulary.NamesIn(language).Count() > 400,
                $"La lingua {language} ha troppi pochi nomi tradotti.");
    }

    [Fact]
    public void LAnalizzatorePortaINomiAllaFormaCanonica()
    {
        var node = (FunctionNode)new FormulaParser().Parse("=SOMMA(A1:A3)", "it", _vocabulary);
        Assert.Equal("SUM", node.Name);

        var tedesco = (FunctionNode)new FormulaParser().Parse("=SUMME(A1:A3)", "de", _vocabulary);
        Assert.Equal("SUM", tedesco.Name);
    }

    [Fact]
    public void LeFunzioniAnnidateSiTraduconoTutte()
    {
        var node = (FunctionNode)new FormulaParser().Parse("=SE(MEDIA(A1:A3)>10;SOMMA(B1:B3);0)", "it", _vocabulary);

        Assert.Equal("IF", node.Name);
        var condition = (BinaryNode)node.Arguments[0];
        Assert.Equal("AVERAGE", ((FunctionNode)condition.Left).Name);
        Assert.Equal("SUM", ((FunctionNode)node.Arguments[1]).Name);
    }

    [Fact]
    public void LaScritturaRiportaINomiNellaLinguaDellUtente()
    {
        var writer = new FormulaWriter(_vocabulary);
        var node = new FormulaParser().Parse("=SUM(A1:A3)");

        Assert.Equal("=SOMMA(A1:A3)", writer.Write(node, "it"));
        Assert.Equal("=SUMME(A1:A3)", writer.Write(node, "de"));
        Assert.Equal("=SUM(A1:A3)", writer.Write(node, "en"));
    }

    [Fact]
    public void AndataERitornoRestituisceLaFormulaDiPartenza()
    {
        var writer = new FormulaWriter(_vocabulary);

        foreach (var language in (string[])["it", "de", "es", "fr"])
        {
            var localized = writer.Write(new FormulaParser().Parse("=SUM(A1:A3)"), language);
            var reparsed = (FunctionNode)new FormulaParser().Parse(localized, language, _vocabulary);
            Assert.Equal("SUM", reparsed.Name);
        }
    }

    [Fact]
    public void IlSeparatoreDegliArgomentiSegueLaLingua()
    {
        var writer = new FormulaWriter(_vocabulary);
        var node = new FormulaParser().Parse("=SUM(1,2,3)");

        Assert.Contains(",", writer.Write(node, "en"), StringComparison.Ordinal);
        Assert.Contains(";", writer.Write(node, "it"), StringComparison.Ordinal);
    }

    [Fact]
    public void IlTestoDentroLaFormulaNonVieneTradotto()
    {
        var writer = new FormulaWriter(_vocabulary);
        var node = new FormulaParser().Parse("=IF(A1;\"SUM\";\"\")");

        Assert.Contains("\"SUM\"", writer.Write(node, "it"), StringComparison.Ordinal);
        Assert.StartsWith("=SE(", writer.Write(node, "it"), StringComparison.Ordinal);
    }
}
