using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class ViewZoomTests
{
    [Fact]
    public void GliEstremiNonSiSuperano()
    {
        Assert.Equal(ZoomLimits.Slides.Maximum, ZoomLimits.Slides.At(9).Factor);
        Assert.Equal(1, ZoomLimits.Slides.At(double.NaN).Factor);
    }

    [Fact]
    public void UnFattoreCorrispondeSoloInModoFisso()
    {
        Assert.True(ZoomLimits.Slides.At(1.5).Matches(1.5));
        Assert.False(ViewZoom.Whole.Matches(1));
    }
}
