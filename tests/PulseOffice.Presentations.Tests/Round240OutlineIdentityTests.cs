using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round240OutlineIdentityTests
{
    private readonly IDeckOutlineText _outline = new DeckOutlineText();
    private readonly IOutlineSlideMatching _matching = new OutlineSlideMatching();

    [Fact]
    public void Swapped_blocks_with_the_same_title_follow_their_bodies()
    {
        var matches = _matching.Match(_outline.Parse("Same\n\ta\nSame\n\tb"), _outline.Parse("Same\n\tb\nSame\n\ta"));

        Assert.Equal(new int?[] { 1, 0 }, matches);
    }

    [Fact]
    public void A_title_holding_a_pilcrow_survives_the_outline_text()
    {
        var title = "Mark ¶ here\nnext";

        var entry = Assert.Single(_outline.Parse(_outline.Render([Slide(title)])));

        Assert.Equal(title, entry.Title);
    }

    private static Slide Slide(string title)
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var slide = new SlideFactory().Create("s1", master, SlideLayout.TitleOnly);
        var heading = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(heading with { Text = heading.Text with { Paragraphs = [.. title.Split('\n').Select(text => new TextParagraph(text))] } });
        return slide;
    }
}
