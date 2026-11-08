using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class BackgroundImageOpacityTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Sample()
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = "bg",
            Data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="),
            MediaType = "image/png",
        };
        slide.BackgroundLayout = new BackgroundImageLayout(BackgroundImageMode.Tile, PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero) { Opacity = 0.51 };
        deck.AddExisting(slide);
        return deck;
    }

    [Fact]
    public void La_trasparenza_dell_immagine_di_sfondo_attraversa_ODP_e_PPTX()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Sample(), pptx).Succeeded);
        pptx.Position = 0;
        Assert.Equal(0.51, new PptxReader(_themes, _masters).Read(pptx).Value!.Slides[0].BackgroundLayout.Opacity, 2);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Sample(), odp).Succeeded);
        odp.Position = 0;
        Assert.Equal(0.51, new OdpReader(_themes, _masters).Read(odp).Value!.Slides[0].BackgroundLayout.Opacity, 2);
    }
}
