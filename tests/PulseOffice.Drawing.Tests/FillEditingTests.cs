using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class FillEditingTests
{
    private readonly GradientStopEditing _stops = new();
    private readonly FillPatternCatalog _patterns = new();
    private readonly ShapeGradient _blackToWhite = new() { Stops = [new("#000000", 0), new("#FFFFFF", 1)] };

    [Fact]
    public void IlPuntoNuovoPrendeIlColoreDelPuntoInCuiNasce()
    {
        var added = _stops.Add(_blackToWhite, 0.5);

        Assert.Equal(3, added.Stops.Count);
        Assert.Equal(new GradientStop("#808080", 0.5), added.Stops[1]);
    }

    [Fact]
    public void NonSiScendeSottoDuePunti()
    {
        Assert.Same(_blackToWhite, _stops.Remove(_blackToWhite, 0));
        Assert.Equal(2, _stops.Remove(_stops.Add(_blackToWhite, 0.3), 1).Stops.Count);
    }

    [Fact]
    public void SpostareRiordinaEInvertireRibalta()
    {
        var moved = _stops.Move(_stops.Add(_blackToWhite, 0.5), 0, 0.75);
        var clamped = _stops.Move(_blackToWhite, 1, 7);
        var reversed = _stops.Reverse(_blackToWhite);

        Assert.Equal(["#808080", "#000000", "#FFFFFF"], moved.Stops.Select(stop => stop.ColorHex));
        Assert.Equal(1, clamped.Stops[1].Position);
        Assert.Equal(["#FFFFFF", "#000000"], reversed.Stops.Select(stop => stop.ColorHex));
    }

    [Fact]
    public void IlMotivoSiRiconosceDalTratteggio()
    {
        var grid = _patterns.Patterns.Single(pattern => pattern.Key == "grid");

        var hatch = _patterns.Apply(grid, new ShapeHatch { ColorHex = "#DC2626" });

        Assert.Equal("#DC2626", hatch.ColorHex);
        Assert.Equal(grid, _patterns.Match(hatch));
        Assert.Null(_patterns.Match(hatch with { Distance = Twip.FromPoints(13) }));
    }
}
