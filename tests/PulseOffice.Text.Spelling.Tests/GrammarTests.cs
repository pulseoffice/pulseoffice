using PulseOffice.Text.Spelling;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class GrammarTests
{
    private readonly GrammarChecker _checker = new([new RepeatedWordRule(), new DoubleSpaceRule(), new SpaceBeforePunctuationRule(), new SentenceStartRule()]);

    private IReadOnlyList<GrammarIssue> Check(string text, string language = "it") => _checker.Check(text, LanguageTag.Parse(language));

    [Fact]
    public void LaParolaRipetutaSiSegnalaConLaCorrezione()
    {
        var issue = Assert.Single(Check("Ho visto il il gatto"));
        Assert.Equal("repeatedWord", issue.Rule);
        Assert.Equal("il il", issue.Span.Text);
        Assert.Equal(["il"], issue.Suggestions);
    }

    [Fact]
    public void GliSpaziDoppiSiSegnalanoMaNonInTestaEInCoda()
    {
        Assert.Equal("doubleSpace", Assert.Single(Check("Uno  due")).Rule);
        Assert.Empty(Check("  rientro"));
    }

    [Fact]
    public void LoSpazioPrimaDelPuntoEUnErroreMaNonInFranceseDavantiAiDuePunti()
    {
        Assert.Equal("spaceBeforePunctuation", Assert.Single(Check("Ciao , mondo")).Rule);
        Assert.Empty(Check("Bonjour : monde", "fr"));
    }

    [Fact]
    public void LaFraseDopoIlPuntoCominciaConLaMaiuscola()
    {
        var issue = Assert.Single(Check("Prima frase. seconda frase."));
        Assert.Equal("sentenceCapital", issue.Rule);
        Assert.Equal(["Seconda"], issue.Suggestions);
        Assert.Empty(Check("Vedi ad es. il capitolo"));
    }
}
