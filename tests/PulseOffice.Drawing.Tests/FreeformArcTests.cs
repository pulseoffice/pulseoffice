using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class FreeformArcTests
{
    [Fact]
    public void GliArchiDiventanoCurveEnonSpariscono()
    {
        var outline = new SvgPathParser().Parse("M 0 1 A 1 1 0 0 1 1 0 L 1 1 Z");

        var path = FreeformPath.FromOutline(outline);

        var nodes = path.Figures.Single().Nodes;
        Assert.True(nodes.Count >= 3);
        Assert.Contains(nodes, node => node.Point is { X: 1, Y: 0 } && node.IsCurved);
    }
}
