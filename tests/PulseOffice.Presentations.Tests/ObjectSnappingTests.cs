using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class ObjectSnappingTests
{
    [Fact]
    public void NearestWithinToleranceWins()
    {
        Assert.Equal(100.0, new ObjectSnapping().Nearest(104, [0, 100, 200], 8));
        Assert.Null(new ObjectSnapping().Nearest(120, [0, 100, 200], 8));
    }

    [Fact]
    public void MoveSnapsLeftEdgeToLine()
    {
        var (dx, dy) = new ObjectSnapping().SnapMove(103, 50, 40, 20, [100, 200], [], 8);

        Assert.Equal(-3, dx, 3);
        Assert.Equal(0, dy, 3);
    }

    [Fact]
    public void MoveSnapsCentreToLine()
    {
        var (dx, _) = new ObjectSnapping().SnapMove(78, 0, 40, 0, [100], [], 8);

        Assert.Equal(2, dx, 3);
    }

    [Fact]
    public void FarBoxDoesNotMove()
    {
        var (dx, dy) = new ObjectSnapping().SnapMove(50, 50, 40, 20, [200], [200], 8);

        Assert.Equal(0, dx, 3);
        Assert.Equal(0, dy, 3);
    }

    [Fact]
    public void GuideStoreAddsMovesAndClears()
    {
        var grid = new GridSettings();
        grid.AddGuide(new SlideGuide(true, 100));
        grid.AddGuide(new SlideGuide(true, 100));
        Assert.Single(grid.Guides);

        grid.MoveGuide(0, 250);
        Assert.Equal(250, grid.Guides[0].Position);

        grid.ClearGuides();
        Assert.Empty(grid.Guides);
    }
}
