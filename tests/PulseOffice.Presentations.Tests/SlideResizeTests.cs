using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideResizeTests
{
    [Fact]
    public void DaWidescreenAQuattroTerziIlContenutoStaDentroECentrato()
    {
        var from = SlideSize.Widescreen;
        var to = SlideSize.Standard;
        var full = new DocumentRect(new Twip(0), new Twip(0), from.Width, from.Height);

        var fitted = new SlideResize().Fit(full, from, to);

        Assert.InRange(fitted.Width.Value, to.Width.Value - 2, to.Width.Value + 2);
        Assert.True(fitted.Height.Value < to.Height.Value);
        Assert.InRange(fitted.Top.Value - (to.Height.Value - fitted.Bottom.Value), -2, 2);
    }

    [Fact]
    public void LaStessaMisuraNonSpostaNulla()
    {
        var rect = new DocumentRect(new Twip(100), new Twip(200), new Twip(300), new Twip(400));
        Assert.Equal(rect, new SlideResize().Fit(rect, SlideSize.Widescreen, SlideSize.Widescreen));
    }

    [Fact]
    public void ImisuriNotiSiRiconosconoAncheInVerticale()
    {
        Assert.Equal(SlideSizePreset.Widescreen, new SlideResize().Recognize(SlideSize.Widescreen));
        Assert.Equal(SlideSizePreset.A4, new SlideResize().Recognize(new SlideResize().Rotated(new SlideResize().For(SlideSizePreset.A4))));
        Assert.Equal(SlideSizePreset.Custom,
            new SlideResize().Recognize(new SlideSize(Twip.FromCentimeters(10), Twip.FromCentimeters(10))));
        Assert.True(new SlideResize().IsPortrait(new SlideResize().Rotated(SlideSize.Standard)));
    }

    [Fact]
    public void IlMasterSegueLaDiapositiva()
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Find("Light")!);
        var fitted = new SlideResize().Fit(master, SlideSize.Widescreen, SlideSize.Standard);

        foreach (var layout in fitted.Layouts)
            foreach (var placeholder in layout.Placeholders)
                Assert.True(placeholder.Bounds.Right.Value <= SlideSize.Standard.Width.Value + 1);
    }
}
