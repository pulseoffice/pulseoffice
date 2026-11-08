using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class PlotScaleExtremesTests
{
    [Fact]
    public void UnIntervalloEnormeNonProduceNaN()
    {
        var scale = PlotScale.Nice(-1e308, 1e308, 0, 100);

        Assert.True(double.IsFinite(scale.Minimum));
        Assert.True(double.IsFinite(scale.Maximum));
        Assert.All(scale.Ticks(), tick => Assert.True(double.IsFinite(tick)));
    }

    [Fact]
    public void LaPosizioneSuUnIntervalloEnormeResta()
    {
        var scale = PlotScale.Nice(-1e308, 1e308, 0, 100);

        Assert.Equal(50, scale.Position(0), 6);
        Assert.True(double.IsFinite(scale.Position(1e308)));
    }
}
