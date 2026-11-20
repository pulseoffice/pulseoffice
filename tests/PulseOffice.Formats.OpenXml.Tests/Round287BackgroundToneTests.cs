using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round287BackgroundToneTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly ImageTone Tone = new(20, -30, 0, ImageColorMode.Standard);

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme) with
        {
            BackgroundImage = new RasterImageObject { Id = "master.bg", Data = Png, MediaType = "image/png", Tone = new ImageTone(0, 0, 0, ImageColorMode.BlackWhite) },
        };
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.BackgroundImage = new RasterImageObject { Id = "bg", Data = Png, MediaType = "image/png", Tone = Tone };
        deck.AddExisting(slide);
        return deck;
    }

    private static void AssertTones(Presentation read)
    {
        Assert.Equal(Tone, read.Slides[0].BackgroundImage!.Tone);
        Assert.Equal(ImageColorMode.BlackWhite, read.Master.BackgroundImage!.Tone.Mode);
    }

    [Fact]
    public void Slide_and_master_background_tones_cross_pptx_and_odp()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), pptx).Succeeded);
        pptx.Position = 0;
        AssertTones(new PptxReader(_themes, _masters).Read(pptx).Value!);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), odp).Succeeded);
        odp.Position = 0;
        AssertTones(new OdpReader(_themes, _masters).Read(odp).Value!);
    }
}
