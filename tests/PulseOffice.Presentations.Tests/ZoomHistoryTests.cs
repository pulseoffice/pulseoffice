using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class ZoomHistoryTests
{
    [Fact]
    public void IndietroEAvantiRipercorronoGliZoom()
    {
        var history = new ZoomHistory();
        history.Record(ViewZoom.Whole, ZoomLimits.Slides.At(1));
        history.Record(ZoomLimits.Slides.At(1), ZoomLimits.Slides.At(2));

        Assert.Equal(ZoomLimits.Slides.At(1), history.Back(ZoomLimits.Slides.At(2)));
        Assert.Equal(ViewZoom.Whole, history.Back(ZoomLimits.Slides.At(1)));
        Assert.False(history.CanGoBack);
        Assert.Equal(ZoomLimits.Slides.At(1), history.Forward(ViewZoom.Whole));
    }

    [Fact]
    public void UnoZoomNuovoCancellaIlFuturo()
    {
        var history = new ZoomHistory();
        history.Record(ViewZoom.Whole, ZoomLimits.Slides.At(1));
        history.Back(ZoomLimits.Slides.At(1));
        history.Record(ViewZoom.Whole, ZoomLimits.Slides.At(3));
        Assert.False(history.CanGoForward);
    }
}
