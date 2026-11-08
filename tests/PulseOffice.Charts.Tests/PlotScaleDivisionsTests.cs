using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public class PlotScaleDivisionsTests
{
    [Fact]
    public void LeTaccheSonoMultipliDelPassoTondo()
    {
        var scale = PlotScale.Nice(0, 1.3, 0, 100);

        Assert.Equal([0, 0.2, 0.4, 0.6, 0.8, 1, 1.2, 1.4], scale.Divisions().Select(tick => Math.Round(tick, 9)));
    }

    [Fact]
    public void LeTaccheNonSuperanoIlTetto() =>
        Assert.InRange(PlotScale.Nice(-37, 912, 0, 100).Divisions(8).Count, 2, 9);

    [Fact]
    public void LaScalaRovesciataMetteLaPrimaCategoriaInFondo()
    {
        var scale = new CategoryScale(4, 0, 400, Reversed: true);

        Assert.Equal(350, scale.Centre(0));
        Assert.Equal(50, scale.Centre(3));
    }
}
