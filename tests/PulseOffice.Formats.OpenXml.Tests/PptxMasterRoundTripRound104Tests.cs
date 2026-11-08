using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxMasterRoundTripRound104Tests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Find("Light")!;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme), Size = SlideSize.Widescreen };
        deck.AddExisting(_slides.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        return deck;
    }

    private Presentation RoundTrip(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new PptxReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void Second_master_keeps_its_name_elements_and_slides()
    {
        var deck = Deck();
        var dark = _themes.Themes.First(theme => theme.Name != deck.Theme.Name);
        var extra = _masters.CreateStandard(dark, deck.Size) with
        {
            Id = deck.NextMasterId(), Name = "Second", Elements = new MasterElements(false, true, false),
        };
        deck.AddMaster(extra);
        var slide = _slides.Create(deck.NextId(), extra, SlideLayout.TitleOnly);
        slide.MasterId = extra.Id;
        deck.AddExisting(slide);

        var back = RoundTrip(deck);

        Assert.Equal(2, back.Masters.Count);
        Assert.Equal("Second", back.Masters[1].Name);
        Assert.Equal(new MasterElements(false, true, false), back.Masters[1].Elements);
        Assert.Equal(back.Masters[1].Id, back.Slides[1].MasterId);
        Assert.Null(back.Slides[0].MasterId);
        Assert.Equal(SlideLayout.TitleOnly, back.Slides[1].Layout);
    }

    [Fact]
    public void Layout_background_and_placeholder_geometry_survive()
    {
        var deck = Deck();
        var editing = new MasterEditing();
        var bounds = new DocumentRect(new Twip(1440), new Twip(720), new Twip(7200), new Twip(1440));
        deck.Master = editing.WithLayoutBackground(deck.Master, SlideLayout.TitleOnly, ColorRole.Accent2);
        deck.Master = editing.WithLayoutBackground(deck.Master, SlideLayout.Blank, ColorRole.Accent3);
        deck.Master = editing.WithPlaceholderBounds(deck.Master, SlideLayout.TitleOnly, PlaceholderRole.Title, bounds);

        var back = RoundTrip(deck);

        Assert.Equal(ColorRole.Accent2, back.Master.Template(SlideLayout.TitleOnly).Background);
        Assert.Equal(ColorRole.Accent3, back.Master.Template(SlideLayout.Blank).Background);
        var title = back.Master.Template(SlideLayout.TitleOnly).Placeholders.Single(p => p.Role == PlaceholderRole.Title);
        Assert.InRange(title.Bounds.Left.Value - bounds.Left.Value, -20, 20);
        Assert.InRange(title.Bounds.Width.Value - bounds.Width.Value, -20, 20);
    }

    [Fact]
    public void Every_standard_layout_survives_with_its_kind()
    {
        var deck = Deck();
        foreach (var kind in Enum.GetValues<SlideLayout>())
            deck.AddExisting(_slides.Create(deck.NextId(), deck.Master, kind));

        var back = RoundTrip(deck);

        Assert.Equal(
            deck.Slides.Select(slide => slide.Layout),
            back.Slides.Select(slide => slide.Layout));
        Assert.Equal(deck.Master.Layouts.Select(l => l.Kind), back.Master.Layouts.Select(l => l.Kind));
    }

    [Fact]
    public void Title_seed_font_size_edited_on_the_master_survives()
    {
        var deck = Deck();
        var title = deck.Master.Template(SlideLayout.TitleOnly).Placeholders.Single(p => p.Role == PlaceholderRole.Title);
        deck.Master = new MasterEditing().WithPlaceholderSeed(deck.Master, SlideLayout.TitleOnly, PlaceholderRole.Title,
            title.Seed with { FontSize = 61, Bold = true, Italic = true });

        var back = RoundTrip(deck);

        var seed = back.Master.Template(SlideLayout.TitleOnly).Placeholders.Single(p => p.Role == PlaceholderRole.Title).Seed;
        Assert.Equal(61, seed.FontSize);
        Assert.True(seed.Bold);
        Assert.True(seed.Italic);
    }
}
