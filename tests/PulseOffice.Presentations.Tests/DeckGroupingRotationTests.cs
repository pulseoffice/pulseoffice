using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckGroupingRotationTests
{
    private static ObjectElement Shape(string id, int x, int y, int w, int h, double rotation = 0, int z = 0) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(w), new Twip(h)),
        RotationDegrees = rotation,
        ZOrder = z,
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    [Fact]
    public void RaggruppareESepararePreservaLaRotazioneDelFiglio()
    {
        var grouping = new DeckGrouping();
        var group = grouping.Group([Shape("a", 0, 0, 100, 50, 30), Shape("b", 200, 0, 100, 50)], "g")!;

        var children = grouping.Ungroup(group, () => Guid.NewGuid().ToString())!;

        Assert.Equal(30, children[0].RotationDegrees, 6);
        Assert.Equal(0, children[1].RotationDegrees, 6);
    }

    [Fact]
    public void SeparareUnGruppoRuotatoRuotaIFigliAttornoAlCentro()
    {
        var grouping = new DeckGrouping();
        var group = grouping.Group([Shape("a", 0, 0, 100, 100), Shape("b", 300, 0, 100, 100)], "g")! with { RotationDegrees = 180 };

        var children = grouping.Ungroup(group, () => Guid.NewGuid().ToString())!;

        Assert.Equal(300, children[0].Bounds.Left.Value);
        Assert.Equal(0, children[1].Bounds.Left.Value);
        Assert.All(children, child => Assert.Equal(180, child.RotationDegrees, 6));
    }

    [Fact]
    public void IlGruppoConservaLOrdineDiSovrapposizione()
    {
        var grouping = new DeckGrouping();
        var group = grouping.Group([Shape("sopra", 0, 0, 10, 10, z: 5), Shape("sotto", 20, 0, 10, 10, z: 1)], "g")!;

        var children = grouping.Ungroup(group, () => Guid.NewGuid().ToString())!;

        Assert.True(children.Single(c => c.Content.Id == "sopra").ZOrder > children.Single(c => c.Content.Id == "sotto").ZOrder);
    }
}
