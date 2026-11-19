using PulseOffice.Presentations.UI.Rendering;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round265SlideBackdropTests
{
    [Fact]
    public void A_dark_slide_background_is_the_backdrop_shape_text_is_inked_against()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        slide.Background = 0xFF101820;

        var backdrop = new SlideBackdrop().For(slide, deck.Master, deck.Theme);

        Assert.Equal("#101820", backdrop.Hex);
    }
}
