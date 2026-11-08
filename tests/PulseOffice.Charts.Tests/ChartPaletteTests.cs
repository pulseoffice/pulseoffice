using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class ChartPaletteTests
{
    [Fact]
    public void UnIndiceNegativoDaComunqueUnColoreDellaTavolozza()
    {
        Assert.Equal(ChartPalette.For(7), ChartPalette.For(-1));
        Assert.Equal(ChartPalette.For(0), ChartPalette.For(-8));
    }
}
