using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round336DeckFindPatternTests
{
    private readonly DeckTextSearch _search = new();

    private Slide SlideWith(string notes)
    {
        var slide = new Slide("one", SlideLayout.Blank) { Notes = notes };
        return slide;
    }

    [Fact]
    public void Whole_words_skip_matches_inside_words()
    {
        var matches = _search.Matches([SlideWith("cat category cat")], new TextPattern("cat", false, WholeWords: true));

        Assert.Equal(new[] { 0, 13 }, matches.Select(match => match.Offset));
        Assert.All(matches, match => Assert.Equal(3, match.Length));
    }

    [Fact]
    public void Regular_expressions_match_and_expand_groups_on_replace()
    {
        var text = new RichText(PlaceholderRole.Free, [new TextParagraph("a1 b22")]);

        var (updated, count) = _search.Replace(text, new TextPattern(@"([a-z])(\d+)", true, Regex: true), "$2$1");

        Assert.Equal(2, count);
        Assert.Equal("1a 22b", updated.Paragraphs[0].Text);
    }

    [Fact]
    public void An_invalid_expression_finds_nothing()
    {
        Assert.Empty(_search.Matches([SlideWith("((")], new TextPattern("((", false, Regex: true)));
    }
}
