using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round229OutlineTextTests
{
    private readonly IDeckOutlineText _outline = new DeckOutlineText();

    [Fact]
    public void Bullet_levels_are_written_as_tabs_and_read_back()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var slide = new SlideFactory().Create("s1", master, SlideLayout.TitleAndContent);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText("Plan") });
        var body = slide.Placeholder(PlaceholderRole.Body)!;
        slide.Replace(body with { Text = body.Text with { Paragraphs = [new TextParagraph("One"), new TextParagraph("Deeper") { BulletLevel = 2 }] } });

        var text = _outline.Render([slide]);
        var entry = Assert.Single(_outline.Parse(text));

        Assert.Equal("Plan", entry.Title);
        Assert.Equal([new OutlineLine(0, "One"), new OutlineLine(2, "Deeper")], entry.Body);
    }

    [Fact]
    public void A_two_paragraph_title_stays_on_one_outline_line_so_later_slides_do_not_shift()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var first = new SlideFactory().Create("s1", master, SlideLayout.TitleOnly);
        var heading = first.Placeholder(PlaceholderRole.Title)!;
        first.Replace(heading with { Text = heading.Text with { Paragraphs = [new TextParagraph("A"), new TextParagraph("B")] } });
        var second = new SlideFactory().Create("s2", master, SlideLayout.TitleOnly);

        Assert.Equal(2, _outline.Parse(_outline.Render([first, second])).Count);
    }

    [Fact]
    public void Four_spaces_count_as_one_level()
    {
        var entry = Assert.Single(_outline.Parse("T\n    a\n        b"));
        Assert.Equal([new OutlineLine(0, "a"), new OutlineLine(1, "b")], entry.Body);
    }
}
