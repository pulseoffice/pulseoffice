using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpSecondMasterLayoutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void The_layout_geometry_and_text_style_of_a_second_master_survive()
    {
        var theme = _themes.Find("Light")!;
        var editing = new MasterEditing();
        var second = editing.WithPlaceholderBounds(_masters.CreateStandard(theme) with { Id = "master2", Name = "Second" },
            SlideLayout.TitleSlide, PlaceholderRole.Title, new DocumentRect(new Twip(1500), new Twip(2600), new Twip(9500), new Twip(2000)));
        second = editing.WithPlaceholderSeed(second, SlideLayout.TitleSlide, PlaceholderRole.Title,
            new TextParagraph(string.Empty) { FontSize = 54, Italic = true, Align = TextAlign.Right, ColorRole = ColorRole.Text });
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        deck.AddMaster(second);
        var slide = _slides.Create(deck.NextId(), second, SlideLayout.TitleSlide);
        slide.MasterId = "master2";
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;

        var master = back.MasterOf(back.Slides[0]);
        Assert.Equal("master2", master.Id);
        var title = master.Template(SlideLayout.TitleSlide).Placeholders.First(placeholder => placeholder.Role == PlaceholderRole.Title);
        Assert.Equal(1500, title.Bounds.Left.Value, tolerance: 20);
        Assert.Equal(9500, title.Bounds.Width.Value, tolerance: 20);
        Assert.Equal(54, title.Seed.FontSize, tolerance: 0.01);
        Assert.True(title.Seed.Italic);
        var primary = back.Master.Template(SlideLayout.TitleSlide).Placeholders.First(placeholder => placeholder.Role == PlaceholderRole.Title);
        Assert.NotEqual(1500, primary.Bounds.Left.Value, tolerance: 20);
        Assert.NotEqual(54, primary.Seed.FontSize, tolerance: 0.01);
    }

    [Fact]
    public void Further_layouts_of_the_same_type_are_declared()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var custom = master.Template(SlideLayout.TitleOnly) with { Id = "layout.custom.21", Name = "Banner" };
        var deck = new Presentation { Theme = theme, Master = master with { Layouts = [.. master.Layouts, custom] } };

        Assert.Contains(new OdpWriter().CheckFidelity(deck).Losses, issue => issue.Code == "ODP107");
        Assert.DoesNotContain(new OdpWriter().CheckFidelity(new Presentation { Theme = theme, Master = master }).Losses, issue => issue.Code == "ODP107");
    }

    [Fact]
    public void Further_layouts_of_the_same_type_keep_name_geometry_background_and_their_slides()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var moved = new DocumentRect(new Twip(700), new Twip(800), new Twip(9000), new Twip(1200));
        var basis = master.Template(SlideLayout.TitleOnly);
        var custom = basis with
        {
            Id = "layout.custom.21",
            Name = "Banner",
            Background = ColorRole.Accent2,
            Placeholders = [.. basis.Placeholders.Select(placeholder => placeholder with { Bounds = moved })],
        };
        var deck = new Presentation { Theme = theme, Master = master with { Layouts = [.. master.Layouts, custom] } };
        deck.AddExisting(_slides.Create(deck.NextId(), custom));
        deck.AddExisting(_slides.Create(deck.NextId(), basis));

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;

        var banner = Assert.Single(back.Master.Layouts, layout => layout.Id == "layout.custom.21");
        Assert.Equal("Banner", banner.Name);
        Assert.Equal(ColorRole.Accent2, banner.Background);
        Assert.Equal(moved, banner.Placeholders[0].Bounds);
        Assert.Equal(master.Layouts.Count + 1, back.Master.Layouts.Count);
        Assert.Equal("layout.custom.21", back.Slides[0].LayoutId);
        Assert.Same(banner, back.Master.Template(back.Slides[0]));
        Assert.Equal(basis.Id, back.Master.Template(back.Slides[1]).Id);
    }
}
