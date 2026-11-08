using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Spelling;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class SpellingRoundTests
{
    [Fact]
    public void IgnoraTuttoValePerLeParoleSuccessive()
    {
        var round = new SpellingRound(new Lexicon());

        Assert.Equal(SpellingStepKind.Skip, round.Decide("ciaoo", new SpellingAnswer(SpellingDecision.IgnoreAll)).Kind);

        Assert.True(round.Ignores("ciaoo"));
        Assert.False(round.Ignores("altro"));
    }

    [Fact]
    public void ImparaAggiungeAlDizionarioDellUtente()
    {
        var words = new Lexicon();
        var round = new SpellingRound(words);

        round.Decide("pulseoffice", new SpellingAnswer(SpellingDecision.Learn));

        Assert.True(words.Knows("pulseoffice"));
        Assert.True(round.Ignores("pulseoffice"));
    }

    [Fact]
    public void SostituisciTuttoRicordaLaSostituzione()
    {
        var round = new SpellingRound(new Lexicon());

        var step = round.Decide("perche", new SpellingAnswer(SpellingDecision.ReplaceAll, "perché"));

        Assert.Equal(new SpellingStep(SpellingStepKind.Replace, "perché"), step);
        Assert.Equal("perché", round.Automatic("perche"));
        Assert.Null(round.Automatic("poiche"));
    }

    [Theory]
    [InlineData(SpellingDecision.Stop, SpellingStepKind.Stop)]
    [InlineData(SpellingDecision.Recheck, SpellingStepKind.Recheck)]
    [InlineData(SpellingDecision.Ignore, SpellingStepKind.Skip)]
    [InlineData(SpellingDecision.Replace, SpellingStepKind.Replace)]
    public void OgniDecisioneHaIlSuoPasso(SpellingDecision decision, SpellingStepKind expected) =>
        Assert.Equal(expected, new SpellingRound(new Lexicon()).Decide("x", new SpellingAnswer(decision, "y")).Kind);

    private sealed class Lexicon : IUserDictionary
    {
        private readonly HashSet<string> _words = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Words => _words;

        public bool Knows(string word) => _words.Contains(word);

        public void Add(string word) => _words.Add(word);

        public bool Remove(string word) => _words.Remove(word);

        public event EventHandler? Changed { add { } remove { } }
    }
}
