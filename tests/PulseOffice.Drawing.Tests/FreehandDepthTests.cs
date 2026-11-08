using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class FreehandDepthTests
{
    [Fact]
    public void UnTrattoLunghissimoNonEsaurisceLaPila()
    {
        var points = Enumerable.Range(0, 200_000)
            .Select(index => index / 200_000.0)
            .Select(x => new ShapePoint(x, Math.Pow(x, 60)))
            .ToList();

        var path = new PathAuthoring().Freehand(points, 1e-12, closed: false, filled: false);

        Assert.NotEmpty(path.Figures);
    }
}
