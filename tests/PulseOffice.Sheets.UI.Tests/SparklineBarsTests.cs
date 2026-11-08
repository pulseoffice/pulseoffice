using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class SparklineBarsTests
{
    [Fact]
    public void ConValoriTuttiNegativiLeColonneRestanoNellArea()
    {
        var points = new SparklinePoints([-1.0, -3.0], -3, -1);

        var bars = new SparklineBars().Layout(points, SparklineKind.Column, 0, 20);

        Assert.All(bars, bar => Assert.InRange(bar.Top, 0, 20));
        Assert.All(bars, bar => Assert.InRange(bar.Bottom, 0, 20));
        Assert.Equal(0, bars[0].Top);
    }

    [Fact]
    public void VinciPerdiHaColonneUgualiSopraESotto()
    {
        var points = new SparklinePoints([5.0, -100.0, 0.0], -100, 5);

        var bars = new SparklineBars().Layout(points, SparklineKind.Stacked, 0, 20);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new SparklineBar(0, 0, 10, false), bars[0]);
        Assert.Equal(new SparklineBar(1, 10, 20, true), bars[1]);
    }
}
