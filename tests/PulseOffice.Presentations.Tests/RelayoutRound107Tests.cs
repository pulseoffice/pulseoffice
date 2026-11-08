using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class RelayoutRound107Tests
{
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void A_layout_with_thousands_of_placeholders_takes_the_content_in_order_and_ids_stay_unique()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var crowded = new SlideLayoutTemplate
        {
            Id = "layout.custom.crowded",
            Name = "Crowded",
            Kind = SlideLayout.FourContent,
            Placeholders = [.. Enumerable.Range(0, 3000).Select(index => new PlaceholderTemplate(PlaceholderRole.Body,
                new DocumentRect(new Twip(index), new Twip(index), new Twip(100), new Twip(100)), new TextParagraph(string.Empty)))],
        };
        master = master with { Layouts = [.. master.Layouts, crowded] };
        var source = new Slide("s1", SlideLayout.Blank);
        for (var index = 0; index < 3000; index++)
            source.Add(new TextElement { Id = $"s1.body{index}", Text = new RichText(PlaceholderRole.Body, [new TextParagraph($"t{index}")]) });

        var result = _slides.Relayout(source, master, crowded);

        Assert.Equal("layout.custom.crowded", result.LayoutId);
        Assert.Equal(3000, result.Elements.Count);
        Assert.Equal(3000, result.Elements.Select(element => element.Id).Distinct(StringComparer.Ordinal).Count());
        var texts = result.Elements.OfType<TextElement>().ToList();
        Assert.Equal("t0", texts[0].Text.PlainText);
        Assert.Equal(new Twip(2999), texts[2999].Bounds.Left);
        Assert.Equal("t2999", texts[2999].Text.PlainText);
    }

    [Fact]
    public void Content_without_a_matching_placeholder_becomes_a_plain_text_box_and_empty_placeholders_go()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var source = _slides.Create("s1", master, SlideLayout.TitleSlide);
        var subtitle = source.Placeholder(PlaceholderRole.Subtitle)!;
        source.Replace(subtitle with { Text = new RichText(PlaceholderRole.Subtitle, [new TextParagraph("kept")]) });

        var result = _slides.Relayout(source, master, SlideLayout.TitleOnly);

        Assert.Equal(PlaceholderRole.Free, Assert.Single(result.Elements.OfType<TextElement>(), text => text.Text.PlainText == "kept").Text.Role);
        Assert.Single(result.Elements.OfType<TextElement>(), text => text.Text.Role == PlaceholderRole.Title);
        Assert.Equal(2, result.Elements.Count);
    }
}
