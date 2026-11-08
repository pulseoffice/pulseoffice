using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckTextSearchTests
{
    private RichText Plain(string text) =>
        new(PlaceholderRole.Body, [new TextParagraph(text)]);

    private RichText Runs(params string[] runs) =>
        new(PlaceholderRole.Body, [new TextParagraph(string.Concat(runs)).WithRuns([.. runs.Select(r => new TextRun(r))])]);

    [Fact]
    public void CountIsCaseSensitiveOrNot()
    {
        var t = Plain("Casa casa CASA");
        Assert.Equal(1, new DeckTextSearch().Count(t, "Casa", matchCase: true));
        Assert.Equal(3, new DeckTextSearch().Count(t, "casa", matchCase: false));
    }

    [Fact]
    public void ReplacePreservesRunsAndReportsCount()
    {
        var t = Runs("Nord ", "e Sud");
        var (updated, count) = new DeckTextSearch().Replace(t, "d", "D", matchCase: true);

        Assert.Equal(2, count);
        Assert.Equal(2, updated.Paragraphs[0].Runs.Count);
        Assert.Equal("NorD ", updated.Paragraphs[0].Runs[0].Text);
        Assert.Equal("e SuD", updated.Paragraphs[0].Runs[1].Text);
    }

    [Fact]
    public void ReplaceWithNoMatchReturnsSameCountZero()
    {
        var (_, count) = new DeckTextSearch().Replace(Plain("abc"), "z", "Y", matchCase: false);
        Assert.Equal(0, count);
    }

    [Fact]
    public void SlideMatchesLooksInTheNotesToo()
    {
        var slide = new Slide("s", SlideLayout.Blank) { Notes = "Ricordare il budget" };
        Assert.True(new DeckTextSearch().SlideMatches(slide, "budget", matchCase: false));
    }

    [Fact]
    public void SlideMatchesWalksTextElements()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(new TextElement { Id = "t", Text = Plain("Hello world") });
        Assert.True(new DeckTextSearch().SlideMatches(slide, "world", matchCase: false));
        Assert.False(new DeckTextSearch().SlideMatches(slide, "zzz", matchCase: false));
    }

    [Fact]
    public void UnaParolaDivisaFraDuePorzioniSiTrovaESiSostituisce()
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph(string.Empty).WithRuns([new TextRun("He") { Bold = true }, new TextRun("llo world")])]);

        Assert.Equal(1, new DeckTextSearch().Count(text, "Hello", matchCase: true));

        var (replaced, count) = new DeckTextSearch().Replace(text, "Hello", "Ciao", matchCase: true);

        Assert.Equal(1, count);
        Assert.Equal("Ciao world", string.Concat(replaced.Paragraphs[0].Runs.Select(run => run.Text)));
        Assert.True(replaced.Paragraphs[0].Runs[0].Bold);
    }
}
