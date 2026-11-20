using PulseOffice.UI.Controls.Search;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round336DeckFindHighlightTests
{
    [Fact]
    public void Highlights_follow_whole_words_and_regular_expressions()
    {
        var highlights = new TextFindHighlights();

        var words = highlights.SpansIn(new TextFindQuery("cat", false, WholeWords: true), "cat scatter cat");
        var pattern = highlights.SpansIn(new TextFindQuery(@"\d+", false, Regex: true), "a 12 b 3");

        Assert.Equal(new[] { 0, 12 }, words.Select(span => span.Start));
        Assert.Equal(new[] { (2, 2), (7, 1) }, pattern.Select(span => (span.Start, span.Length)));
    }
}
