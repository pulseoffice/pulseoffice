using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class AuditSpellingTests
{
    [Fact]
    public void UnaLetteraFuoriDalPianoBaseNonSpezzaLaParola()
    {
        var text = "ab\U0002000Bcd prova";
        var words = new WordTokenizer().Words(text);

        Assert.Equal(["ab\U0002000Bcd", "prova"], words.Select(word => word.Text));
    }

    [Fact]
    public void UnaParolaCheIniziaConUnaLetteraFuoriDalPianoBaseSiControlla()
    {
        var words = new WordTokenizer().Words("\U0001D400bc");

        Assert.Equal(["\U0001D400bc"], words.Select(word => word.Text));
    }

    [Fact]
    public void LaSottoetichettaDiScritturaNonDiventaLaRegione()
    {
        Assert.Equal("sr-RS", LanguageTag.Parse("sr-Latn-RS").Canonical);
        Assert.Equal("zh", LanguageTag.Parse("zh-Hans").Canonical);
        Assert.Equal("de-DE", LanguageTag.Parse("de_DE.UTF-8").Canonical);
        Assert.Equal("es-419", LanguageTag.Parse("es-419").Canonical);
    }

    [Fact]
    public void UnaParolaLunghissimaSiSillabaInTempoLineare()
    {
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("it"), ["UTF-8", "a1b", "b1c", ".ab4"]);
        var word = string.Concat(Enumerable.Repeat("abc", 20_000));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var positions = patterns.Positions(word);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{watch.Elapsed}");
        Assert.Contains(4, positions);
    }

    [Fact]
    public void UnaCifraNonLatinaNonDiventaUnLivelloDiSillabazione()
    {
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("it"), ["UTF-8", "a\u0663b"]);

        Assert.Empty(patterns.Positions("aaabbb"));
    }

    [Fact]
    public void UnConteggioNegativoNelDizionarioDeiSinonimiNonInghiotteIlResto()
    {
        var thesaurus = MyThesThesaurus.Parse(LanguageTag.Parse("it"),
            ["UTF-8", "rotto|-1", "casa|1", "(nome)|abitazione|dimora"]);

        Assert.Equal(["abitazione", "dimora"], thesaurus.Lookup("casa").Single().Synonyms);
    }
}
