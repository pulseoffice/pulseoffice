using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class ChartColoursTests
{
    [Fact]
    public void UnColoreIllegibileRicadeSullaTavolozza() =>
        Assert.Equal(Color.Parse(ChartPalette.For(2)), ChartColours.Resolve("non-un-colore", 2));

    [Fact]
    public void UnColoreValidoVieneUsato() =>
        Assert.Equal(Color.Parse("#112233"), ChartColours.Resolve("#112233", 0));
}
