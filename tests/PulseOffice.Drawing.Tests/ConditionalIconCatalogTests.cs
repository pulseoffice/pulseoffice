using PulseOffice.Drawing.ConditionalIcons;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ConditionalIconCatalogTests
{
    private readonly ConditionalIconCatalog _catalog = new();

    [Theory]
    [InlineData("3Arrows", 3)]
    [InlineData("3ArrowsGray", 3)]
    [InlineData("3Flags", 3)]
    [InlineData("3TrafficLights1", 3)]
    [InlineData("3TrafficLights2", 3)]
    [InlineData("3Signs", 3)]
    [InlineData("3Symbols", 3)]
    [InlineData("3Symbols2", 3)]
    [InlineData("3Smilies", 3)]
    [InlineData("3ColorSmilies", 3)]
    [InlineData("3Stars", 3)]
    [InlineData("3Triangles", 3)]
    [InlineData("4Arrows", 4)]
    [InlineData("4ArrowsGray", 4)]
    [InlineData("4RedToBlack", 4)]
    [InlineData("4Rating", 4)]
    [InlineData("4TrafficLights", 4)]
    [InlineData("5Arrows", 5)]
    [InlineData("5ArrowsGray", 5)]
    [InlineData("5Rating", 5)]
    [InlineData("5Quarters", 5)]
    [InlineData("5Boxes", 5)]
    public void OgniFamigliaDiCalcHaIlNumeroGiustoDiIcone(string set, int count)
    {
        Assert.Equal(count, _catalog.Count(set));
        for (var index = 0; index < count; index++)
            Assert.NotEmpty(_catalog.Icon(set, index).Layers);
    }

    [Fact]
    public void LeFamiglieSonoQuelleDiCalc() => Assert.Equal(22, _catalog.Names.Count);

    [Fact]
    public void LeIconeStannoNelRiquadroUnitario()
    {
        var points = _catalog.Names
            .SelectMany(set => Enumerable.Range(0, _catalog.Count(set)).Select(index => _catalog.Icon(set, index)))
            .SelectMany(icon => icon.Layers)
            .SelectMany(layer => new PulseOffice.Drawing.Shapes.SegmentEnds().Of(layer.Outline));

        Assert.All(points, point => Assert.True(point.X is >= -0.01 and <= 1.01 && point.Y is >= -0.01 and <= 1.01, $"{point}"));
    }

    [Fact]
    public void UnInsiemeSconosciutoRicadeSulleFrecceComeInCalc()
    {
        Assert.Equal(3, _catalog.Count("Sconosciuto"));
        Assert.Same(_catalog.Icon("3Arrows", 2), _catalog.Icon("Sconosciuto", 2));
    }
}
