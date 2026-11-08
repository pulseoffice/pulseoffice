using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxCustomLayoutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void Two_layouts_of_the_same_type_stay_distinct_and_each_slide_keeps_its_own()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var standard = master.Template(SlideLayout.TitleAndContent);
        var custom = standard with
        {
            Id = "layout.custom.21",
            Name = "Quote",
            Placeholders = [.. standard.Placeholders.Select(placeholder => placeholder with
            {
                Bounds = placeholder.Bounds with { Left = new Twip(4000) },
            })],
        };
        master = master with { Layouts = [.. master.Layouts, custom] };
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), standard));
        deck.AddExisting(_slides.Create(deck.NextId(), custom));

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        var kinds = back.Master.Layouts.Where(layout => layout.Kind == SlideLayout.TitleAndContent).ToList();
        Assert.Equal(2, kinds.Count);
        Assert.Equal("Quote", kinds[1].Name);
        var first = back.Master.Template(back.Slides[0]);
        var second = back.Master.Template(back.Slides[1]);
        Assert.Same(kinds[0], first);
        Assert.Same(kinds[1], second);
        Assert.All(second.Placeholders, placeholder => Assert.Equal(4000, placeholder.Bounds.Left.Value, tolerance: 2));
        Assert.All(first.Placeholders, placeholder => Assert.NotEqual(4000, placeholder.Bounds.Left.Value, tolerance: 2));
    }

    [Fact]
    public void A_layout_of_a_type_already_seen_is_read_as_a_further_layout_not_as_a_replacement()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var standard = master.Template(SlideLayout.TitleOnly);
        var moved = standard with
        {
            Id = "second",
            Name = "Banner",
            Placeholders = [.. standard.Placeholders.Select(placeholder => placeholder with { Bounds = placeholder.Bounds with { Top = new Twip(6000) } })],
        };
        var deck = new Presentation { Theme = theme, Master = master with { Layouts = [.. master.Layouts, moved] } };
        deck.AddExisting(_slides.Create(deck.NextId(), standard));

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal(standard.Placeholders[0].Bounds.Top.Value, back.Master.Template(SlideLayout.TitleOnly).Placeholders[0].Bounds.Top.Value, tolerance: 2);
        Assert.Equal(2, back.Master.Layouts.Count(layout => layout.Kind == SlideLayout.TitleOnly));
    }
}
