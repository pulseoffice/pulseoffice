using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpMasterRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation RoundTrip(Presentation deck)
    {
        var writer = new OdpWriter();
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new OdpReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void MasterPlaceholderGeometrySurvives()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        master = new MasterEditing().WithPlaceholderBounds(master, SlideLayout.TitleSlide, PlaceholderRole.Title,
            new DocumentRect(new Twip(1500), new Twip(2600), new Twip(9500), new Twip(2000)));

        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));

        var back = RoundTrip(deck);
        var title = back.Master.Template(SlideLayout.TitleSlide).Placeholders.First(p => p.Role == PlaceholderRole.Title);

        Assert.Equal(1500, title.Bounds.Left.Value, tolerance: 20);
        Assert.Equal(9500, title.Bounds.Width.Value, tolerance: 20);
    }

    [Fact]
    public void MasterPlaceholderTextStyleSurvives()
    {
        var theme = _themes.Find("Sand")!;
        var master = _masters.CreateStandard(theme);
        var custom = new TextParagraph(string.Empty)
        {
            FontSize = 28, Bold = false, Italic = true, Align = TextAlign.Right,
            ColorRole = ColorRole.Accent4, MajorFont = false, Bullet = true,
        };
        master = new MasterEditing().WithPlaceholderSeed(master, SlideLayout.TitleSlide, PlaceholderRole.Title, custom);

        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));

        var seed = RoundTrip(deck).Master.Template(SlideLayout.TitleSlide)
            .Placeholders.First(p => p.Role == PlaceholderRole.Title).Seed;

        Assert.Equal(28, seed.FontSize, tolerance: 0.01);
        Assert.False(seed.Bold);
        Assert.True(seed.Italic);
        Assert.Equal(TextAlign.Right, seed.Align);
        Assert.Equal(theme.Colors.Resolve(ColorRole.Accent4) & 0x00FFFFFF, seed.Color!.Value & 0x00FFFFFF);
        Assert.True(seed.Bullet);
    }
}
