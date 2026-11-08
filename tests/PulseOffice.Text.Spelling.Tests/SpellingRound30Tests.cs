using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Dictionaries;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class SpellingRound30Tests
{
    private sealed class CountingDictionary : ISpellDictionary
    {
        public int Suggestions { get; private set; }

        public LanguageTag Language => LanguageTag.Parse("it");

        public bool Check(string word) => false;

        public IReadOnlyList<string> Suggest(string word)
        {
            Suggestions++;
            return ["parola"];
        }
    }

    private sealed class Provider(ISpellDictionary dictionary) : ISpellDictionaryProvider
    {
        public IReadOnlyList<LanguageTag> Available => [dictionary.Language];

        public ISpellDictionary? For(LanguageTag language) => dictionary;

        public void Refresh()
        {
        }
    }

    private sealed class NoUserWords : IUserDictionary
    {
        public IReadOnlyCollection<string> Words => [];

        public bool Knows(string word) => false;

        public void Add(string word)
        {
        }

        public bool Remove(string word) => false;

        public event EventHandler? Changed { add { } remove { } }
    }

    [Fact]
    public void ATextFullOfMistakesComputesABoundedNumberOfSuggestions()
    {
        var dictionary = new CountingDictionary();
        var checker = new SpellChecker(new Provider(dictionary), new NoUserWords(), new WordTokenizer());
        var text = string.Join(' ', Enumerable.Repeat("sbaglio", 5000));

        var mistakes = checker.Check(text, LanguageTag.Parse("it"));

        Assert.Equal(5000, mistakes.Count);
        Assert.Equal(SpellChecker.MaximumSuggestedMistakes, dictionary.Suggestions);
        Assert.NotEmpty(mistakes[0].Suggestions);
        Assert.Empty(mistakes[^1].Suggestions);
    }

    [Fact]
    public void AFullCompletionListStillLearnsNewWords()
    {
        var completion = new WordCompletion();

        for (var index = 0; index < 1000; index++)
        {
            var word = "parola" + new string((char)('a' + index % 26), 2) + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture).Replace('0', 'k').Replace('1', 'l').Replace('2', 'm').Replace('3', 'n').Replace('4', 'o').Replace('5', 'p').Replace('6', 'q').Replace('7', 'r').Replace('8', 's').Replace('9', 't');
            completion.Collect(word + " " + word);
        }

        completion.Collect("straordinariamente");

        Assert.Equal("straordinariamente", completion.Suggest("straord"));
    }

    [Fact]
    public void EnormousWordsAreNotCollectedForCompletion()
    {
        var completion = new WordCompletion();

        completion.Collect(new string('a', 100_000));

        Assert.Empty(completion.Words);
    }

    [Theory]
    [InlineData("en-US", "Flat 2a and room 3o", "Flat 2a and room 3o")]
    [InlineData("it", "il 2a fila", "il 2ª fila")]
    public void GenderedOrdinalsOnlyApplyToLanguagesThatUseThem(string language, string input, string expected)
    {
        var corrector = new AutoCorrect(new AutoCorrectFixture().Table, new WordTokenizer());

        var output = corrector.Apply(input, new AutoCorrectOptions
        {
            OrdinalSuperscript = true, CapitaliseFirstLetter = false, Replacements = false, Language = language,
        });

        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("CC-BY-ND-4.0", DictionaryFreedom.Unknown)]
    [InlineData("GPL-2.0 AND (MIT OR BSD-3-Clause)", DictionaryFreedom.Copyleft)]
    [InlineData("(MIT OR GPL-3.0) AND Apache-2.0", DictionaryFreedom.Free)]
    [InlineData("MIT AND (GPL-2.0", DictionaryFreedom.Unknown)]
    [InlineData("GPL-2.0-or-later WITH Classpath-exception-2.0", DictionaryFreedom.Copyleft)]
    [InlineData("MIT or GPL-3.0", DictionaryFreedom.Free)]
    public void LicenseExpressionsRespectParenthesesAndRestrictions(string spdx, DictionaryFreedom expected) =>
        Assert.Equal(expected, new DictionaryLicenseClassifier().FromSpdx(spdx).Freedom);
}
