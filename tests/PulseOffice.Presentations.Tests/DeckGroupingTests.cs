using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckGroupingTests
{
    private readonly IDeckGrouping _grouping = new DeckGrouping();

    private static ObjectElement Shape(string id, int x, int y, int w, int h) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(w), new Twip(h)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    [Fact]
    public void GroupComputesBoundsAndFractionalChildren()
    {
        var group = _grouping.Group([Shape("a", 100, 100, 200, 200), Shape("b", 300, 300, 100, 100)], "g1");

        Assert.NotNull(group);
        Assert.Equal(100, group!.Bounds.Left.Value);
        Assert.Equal(100, group.Bounds.Top.Value);
        Assert.Equal(300, group.Bounds.Width.Value);
        Assert.Equal(300, group.Bounds.Height.Value);

        var children = ((ShapeGroupObject)group.Content).Children;
        Assert.Equal(2, children.Count);
        Assert.Equal(0, children[0].X, 3);
        Assert.Equal(2.0 / 3.0, children[0].Width, 3);
        Assert.Equal(2.0 / 3.0, children[1].X, 3);
    }

    [Fact]
    public void GroupRefusesFewerThanTwoObjects()
    {
        Assert.Null(_grouping.Group([Shape("a", 0, 0, 10, 10)], "g1"));
    }

    [Fact]
    public void UngroupRestoresAbsolutePositions()
    {
        var group = _grouping.Group([Shape("a", 100, 100, 200, 200), Shape("b", 300, 300, 100, 100)], "g1")!;

        var i = 0;
        var children = _grouping.Ungroup(group, () => $"c{i++}");

        Assert.NotNull(children);
        Assert.Equal(2, children!.Count);
        Assert.Equal(100, children[0].Bounds.Left.Value);
        Assert.Equal(200, children[0].Bounds.Width.Value);
        Assert.Equal(300, children[1].Bounds.Left.Value);
        Assert.Equal(100, children[1].Bounds.Width.Value);
    }

    [Fact]
    public void ExplodeYieldsChildrenWithStableIds()
    {
        var group = _grouping.Group([Shape("a", 100, 100, 200, 200), Shape("b", 300, 300, 100, 100)], "g1")!;
        var exploded = DeckGrouping.Explode(group);

        Assert.Equal(2, exploded.Count);
        Assert.Equal("g1.c0", exploded[0].Id);
        Assert.Equal("g1.c1", exploded[1].Id);
        Assert.All(exploded, e => Assert.IsType<ShapeObject>(e.Content));
    }
}
