using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class FreeformRefitTests
{
    [Fact]
    public void UnNodoTiratoFuoriAllargaIlRiquadro()
    {
        var path = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 0)), new PathNode(new ShapePoint(0.5, 2))]) { Closed = true }]);
        var freeform = new FreeformObject { Id = "f", Path = FreeformPath.Empty };
        var element = new ObjectElement { Id = "f", Content = freeform, Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)) };

        var refitted = new FreeformRefit().Refit(element, freeform, path);

        Assert.Equal(1000, refitted.Bounds.Top.Value);
        Assert.Equal(2000, refitted.Bounds.Height.Value);
        Assert.Equal(2000, refitted.Bounds.Width.Value);
        var nodes = ((FreeformObject)refitted.Content).Path.Figures[0].Nodes;
        Assert.Equal(1, nodes[2].Point.Y, 6);
    }
}
