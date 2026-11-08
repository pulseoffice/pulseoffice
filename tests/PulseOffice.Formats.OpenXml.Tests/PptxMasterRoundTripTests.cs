using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxMasterRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

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
    public void UnaDiapositivaSenzaSfondoProprioEreditaQuelloDelloSchemaInPptx()
    {
        var theme = _themes.Find("Light")!;
        var master = new MasterEditing().WithBackground(_masters.CreateStandard(theme), ColorRole.Accent2);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var package = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, false))
        {
            var slide = package.PresentationPart!.SlideParts.Single().Slide!;
            Assert.Null(slide.CommonSlideData!.Background);
            var ids = package.PresentationPart.Presentation!.SlideMasterIdList!.Elements<DocumentFormat.OpenXml.Presentation.SlideMasterId>().Select(id => (uint)id.Id!.Value)
                .Concat(package.PresentationPart.SlideMasterParts.SelectMany(part => part.SlideMaster!.SlideLayoutIdList!.Elements<DocumentFormat.OpenXml.Presentation.SlideLayoutId>().Select(id => (uint)id.Id!.Value)))
                .ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(ids, id => Assert.True(id >= 2147483648U));
        }
        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;
        Assert.Null(back.Slides[0].Background);
    }

    [Fact]
    public void MasterPlaceholderGeometryAndBackgroundSurvive()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        master = new MasterEditing().WithPlaceholderBounds(master, SlideLayout.TitleSlide, PlaceholderRole.Title,
            new DocumentRect(new Twip(1234), new Twip(2345), new Twip(9000), new Twip(1800)));
        master = new MasterEditing().WithBackground(master, ColorRole.Accent2);
        master = new MasterEditing().WithLayoutBackground(master, SlideLayout.SectionHeader, ColorRole.Accent3);

        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));

        var back = RoundTrip(deck);

        var title = back.Master.Template(SlideLayout.TitleSlide).Placeholders.First(p => p.Role == PlaceholderRole.Title);
        Assert.Equal(1234, title.Bounds.Left.Value, tolerance: 2);
        Assert.Equal(9000, title.Bounds.Width.Value, tolerance: 2);
        Assert.Equal(ColorRole.Accent2, back.Master.Background);
        Assert.Equal(ColorRole.Accent3, back.Master.Template(SlideLayout.SectionHeader).Background);
    }

    [Fact]
    public void MasterPlaceholderTextStyleSurvives()
    {
        var theme = _themes.Find("Light")!;
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
        Assert.Equal(ColorRole.Accent4, seed.ColorRole);
        Assert.False(seed.MajorFont);
        Assert.True(seed.Bullet);
    }
}
