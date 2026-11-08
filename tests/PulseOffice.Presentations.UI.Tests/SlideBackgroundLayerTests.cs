using Avalonia;
using Avalonia.Media;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class SlideBackgroundLayerTests
{
    private readonly SlideRenderer _renderer = new(new PulseOffice.UI.Controls.Shapes.DrawingObjectRenderer(), new FakeTextCatalog());

    private (Presentation Deck, Slide Slide) Deck()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme) with { BackgroundColor = 0xFF069A2Eu };
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private Color Base(Presentation deck, Slide slide) =>
        _renderer.Layers(slide, deck.Size, new Rect(0, 0, 640, 360), deck.ThemeOf(slide), deck.MasterOf(slide), new SlidePalette(Colors.Transparent, Colors.Transparent))[0].Fill!.Value;

    [Fact]
    public void Sotto_l_immagine_di_sfondo_della_diapositiva_non_si_vede_lo_sfondo_dello_schema()
    {
        var (deck, slide) = Deck();
        Assert.Equal(Color.FromUInt32(0xFF069A2Eu), Base(deck, slide));

        slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "sfondo", Data = new byte[] { 0x89, 0x50, 0x4E, 0x47 }, MediaType = "image/png" };
        slide.BackgroundLayout = new BackgroundImageLayout(BackgroundImageMode.Natural, PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero);
        Assert.Equal(Colors.White, Base(deck, slide));
    }
}
