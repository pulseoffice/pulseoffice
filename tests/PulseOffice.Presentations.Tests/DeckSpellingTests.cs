using PulseOffice.Presentations;
using PulseOffice.Text.Spelling;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckSpellingTests
{
    private sealed class FakeChecker : ISpellChecker
    {
        public bool HasDictionary(LanguageTag language) => true;

        public IReadOnlyList<SpellingMistake> Check(string text, LanguageTag language, int suggestions = 6) =>
            [.. new WordTokenizer().Words(text).Where(w => w.Text.Contains("zz")).Select(w => new SpellingMistake(w, ["ok"]))];

        public bool IsCorrect(string word, LanguageTag language) => !word.Contains("zz");
    }

    private readonly DeckSpelling _spelling = new(new FakeChecker(), new TextLanguageResolver());
    private static readonly LanguageTag Italian = LanguageTag.Parse("it-IT");

    [Fact]
    public void LaSostituzioneConservaLaFormattazioneDellePorzioni()
    {
        var paragraph = new TextParagraph(string.Empty).WithRuns(
            [new TextRun("Una "), new TextRun("pazzola") { Bold = true }, new TextRun(" qui")]);
        var text = new RichText(PlaceholderRole.Body, [paragraph]);

        var updated = _spelling.Replace(text, 0, new WordSpan(4, 7, "pazzola"), "parola");

        var runs = updated.Paragraphs[0].Runs;
        Assert.Equal("Una parola qui", updated.Paragraphs[0].Text);
        Assert.Equal("parola", runs[1].Text);
        Assert.True(runs[1].Bold);
    }

    [Fact]
    public void UnaParolaAStavoltaFraDuePorzioniDiventaUnaSola()
    {
        var paragraph = new TextParagraph(string.Empty).WithRuns([new TextRun("pa"), new TextRun("zzo fine")]);
        var updated = _spelling.Replace(new RichText(PlaceholderRole.Body, [paragraph]), 0, new WordSpan(0, 5, "pazzo"), "matto");

        Assert.Equal("matto fine", updated.Paragraphs[0].Text);
    }

    [Fact]
    public void IlGiroPartiDallaDiapositivaCorrenteEDTornaAllInizio()
    {
        var deck = new Presentation();
        var first = new Slide("a", SlideLayout.Blank);
        first.Add(new TextElement { Id = "a.t", Text = new RichText(PlaceholderRole.Body, [new TextParagraph("uno azz")]) });
        var second = new Slide("b", SlideLayout.Blank);
        second.Add(new TextElement { Id = "b.t", Text = new RichText(PlaceholderRole.Body, [new TextParagraph("bzz due")]) });
        deck.InsertExisting(0, first);
        deck.InsertExisting(1, second);

        var found = _spelling.Walk(deck, 1, Italian).ToList();

        Assert.Equal(["bzz", "azz"], found.Select(f => f.Mistake.Word.Text));
        Assert.Equal("b", found[0].Slide.Id);
    }
}
