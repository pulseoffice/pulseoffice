using Xunit;

namespace PulseOffice.Text.Tests;

public sealed class TextCaseTests
{
    private readonly TextCases _cases = new();

    [Theory]
    [InlineData("Ciao")]
    [InlineData("A")]
    [InlineData("ciao")]
    [InlineData("CIAO MONDO")]
    public void IlCicloCambiaSempreIlTesto(string sample)
    {
        var text = sample;
        for (var i = 0; i < 6; i++)
        {
            var next = _cases.NextInCycle(text).Apply(text);
            Assert.NotEqual(text, next);
            text = next;
        }
    }

    [Fact]
    public void LaMaiuscolaDiFraseValePerOgniFrase()
    {
        Assert.Equal("  «Ciao. Come stai? Bene!", _cases[TextCaseKind.Sentence].Apply("  «CIAO. COME STAI? bene!"));
    }

    [Fact]
    public void LaMaiuscolaDiFraseRiparteAOgniParagrafo()
    {
        Assert.Equal("Primo punto\nSecondo punto", _cases[TextCaseKind.Sentence].Apply("primo punto\nsecondo punto"));
    }

    [Fact]
    public void LaMaiuscolaDiFraseNonSaltaUnaLetteraFuoriDalPianoBase()
    {
        Assert.Equal("\U00010400bc. \U00010400", _cases[TextCaseKind.Sentence].Apply("\U00010428BC. \U00010428"));
    }

    [Fact]
    public void IlRovesciamentoCambiaAncheLeLettereFuoriDalPianoBase()
    {
        Assert.Equal("\U00010428a", _cases[TextCaseKind.Toggle].Apply("\U00010400A"));
    }
}
