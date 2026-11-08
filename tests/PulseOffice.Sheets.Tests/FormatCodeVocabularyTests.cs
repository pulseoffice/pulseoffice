using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class FormatCodeVocabularyTests
{
    private readonly FormatCodeVocabulary _vocabulary = new();

    [Fact]
    public void LeCinqueLingueSonoDichiarate() =>
        Assert.Equal(["en", "it", "de", "es", "fr"], FormatCodeVocabulary.SupportedLanguages);

    [Theory]
    [InlineData("it", "gg/mm/aaaa")]
    [InlineData("de", "tt/mm/jjjj")]
    [InlineData("fr", "jj/mm/aaaa")]
    [InlineData("es", "dd/mm/aaaa")]
    [InlineData("en", "dd/mm/yyyy")]
    public void LeDateSiRiportanoAllaFormaCanonica(string language, string localized) =>
        Assert.Equal("dd/mm/yyyy", _vocabulary.ToCanonical(localized, language));

    [Theory]
    [InlineData("it", "gg/mm/aaaa")]
    [InlineData("de", "tt/mm/jjjj")]
    [InlineData("fr", "jj/mm/aaaa")]
    public void LeDateSiRiscrivonoNellaLinguaDellUtente(string language, string expected) =>
        Assert.Equal(expected, _vocabulary.ToLanguage("dd/mm/yyyy", language));

    [Theory]
    [InlineData("it", "[ROSSO]0,00")]
    [InlineData("de", "[ROT]0,00")]
    [InlineData("fr", "[ROUGE]0,00")]
    [InlineData("es", "[ROJO]0,00")]
    public void IColoriSiRiportanoAllaFormaCanonica(string language, string localized) =>
        Assert.StartsWith("[RED]", _vocabulary.ToCanonical(localized, language), StringComparison.Ordinal);

    [Theory]
    [InlineData("it", "NERO")]
    [InlineData("de", "SCHWARZ")]
    [InlineData("es", "NEGRO")]
    [InlineData("fr", "NOIR")]
    public void IColoriSiRiscrivonoNellaLinguaDellUtente(string language, string expected) =>
        Assert.Contains(expected, _vocabulary.ToLanguage("[BLACK]0.00", language), StringComparison.Ordinal);

    [Fact]
    public void IlTestoFraVirgoletteNonVieneTradotto()
    {
        var canonical = _vocabulary.ToCanonical("\"gennaio\" aaaa", "it");
        Assert.Contains("\"gennaio\"", canonical, StringComparison.Ordinal);
        Assert.EndsWith("yyyy", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCarattereProtettoNonEUnSegnaposto()
    {
        var canonical = _vocabulary.ToCanonical(@"0\g", "it");
        Assert.EndsWith(@"\g", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void LeCondizioniNonSonoLingueEnonSiToccano() =>
        Assert.Contains("[>1000]", _vocabulary.ToCanonical("[>1000]0,00", "it"), StringComparison.Ordinal);

    [Fact]
    public void LaTraduzioneAndataERitornoRestituisceIlCodiceDiPartenza()
    {
        foreach (var language in FormatCodeVocabulary.SupportedLanguages)
        {
            var localized = _vocabulary.ToLanguage("dd/mm/yyyy hh:mm", language);
            Assert.Equal("dd/mm/yyyy hh:mm", _vocabulary.ToCanonical(localized, language));
        }
    }

    [Fact]
    public void UnaLinguaSconosciutaRicadeSullInglese() =>
        Assert.Equal("dd/mm/yyyy", _vocabulary.ToCanonical("dd/mm/yyyy", "ja"));

    [Fact]
    public void UnCodiceLocalizzatoSiAnalizzaCorrettamente()
    {
        var format = new NumberFormatParser().Parse("[ROSSO]gg/mm/aaaa", "it", _vocabulary);

        Assert.Equal("Red", format.Sections[0].ColorName);
        Assert.Equal(FormatValueKind.DateTime, format.Sections[0].ValueKind);
    }
}

public sealed class FormatCodeKeywordTests
{
    [Theory]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    public void GeneralKeywordSurvivesTranslation(string language)
    {
        var vocabulary = new PulseOffice.Sheets.Formatting.FormatCodeVocabulary();
        Assert.Equal("General", vocabulary.ToCanonical("General", language));
        Assert.Equal("Standard", vocabulary.ToCanonical("Standard", language));
        Assert.Equal("General", vocabulary.ToLanguage("General", language));
        Assert.True(new PulseOffice.Sheets.Formatting.NumberFormatParser().Parse("Standard", language, vocabulary).IsGeneral);
    }
}
