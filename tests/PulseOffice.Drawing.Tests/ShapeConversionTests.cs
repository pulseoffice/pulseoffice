using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Layout;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeConversionTests
{
    private readonly ShapeConversion _conversion = new();
    private readonly ShapeGeometry _geometry = new();

    [Fact]
    public void UnEllisseInCurvaHaNodiCurviEChiusi()
    {
        var path = _conversion.ToCurve(_geometry.Outline(ShapeKind.Ellipse));
        Assert.True(path.Filled);
        Assert.Contains(path.Figures[0].Nodes, node => node.IsCurved);
    }

    [Fact]
    public void UnEllisseInPoligonoHaSoloAngoli()
    {
        var path = _conversion.ToPolygon(_geometry.Outline(ShapeKind.Ellipse), 8);
        Assert.All(path.Figures[0].Nodes, node => Assert.False(node.IsCurved));
        Assert.True(path.Figures[0].Nodes.Count >= 16);
        Assert.True(path.Figures[0].Closed);
    }

    [Fact]
    public void UnRettangoloInPoligonoResta4Angoli()
    {
        var path = _conversion.ToPolygon(_geometry.Outline(ShapeKind.Rectangle));
        Assert.Equal(4, path.Figures[0].Nodes.Count);
    }

    [Fact]
    public void LaDistribuzioneLasciaFermiGliEstremiEPareggiaGliSpazi()
    {
        var result = new BoxDistribution().EqualGaps(
        [
            new DistributedBox("a", 0, 10), new DistributedBox("b", 15, 10), new DistributedBox("c", 90, 10),
        ]);
        Assert.Equal([0d, 45d, 90d], result.Select(box => box.Start));
    }
}
