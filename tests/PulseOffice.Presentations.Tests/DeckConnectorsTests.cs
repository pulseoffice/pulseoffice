using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckConnectorsTests
{
    private readonly IDeckConnectors _connectors = new DeckConnectors(new PulseOffice.Drawing.Shapes.ConnectorFramer());

    private static ObjectElement Shape(string id, int x, int y) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(1000), new Twip(1000)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    [Fact]
    public void ConnectBuildsAConnectorElement()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        var a = Shape("a", 0, 0);
        var b = Shape("b", 5000, 3000);
        slide.Add(a);
        slide.Add(b);

        var connector = _connectors.Connect(slide, a, b, "c1", ConnectorKind.Straight);

        Assert.NotNull(connector);
        var content = Assert.IsType<ConnectorObject>(connector!.Content);
        Assert.Equal("a", content.Start.ObjectId);
        Assert.Equal("b", content.End.ObjectId);
        Assert.True(content.Path.Figures.Count > 0);
        Assert.True(connector.Bounds.Width.Value > 0);
    }

    [Fact]
    public void ConnectRefusesSameObject()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        var a = Shape("a", 0, 0);
        slide.Add(a);
        Assert.Null(_connectors.Connect(slide, a, a, "c1", ConnectorKind.Straight));
    }

    [Fact]
    public void RerouteFollowsMovedObject()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(Shape("a", 0, 0));
        slide.Add(Shape("b", 5000, 3000));
        var connector = _connectors.Connect(slide, (ObjectElement)slide.Find("a")!, (ObjectElement)slide.Find("b")!, "c1", ConnectorKind.Elbow)!;
        slide.Add(connector);
        var before = connector.Bounds;

        slide.Replace(Shape("b", 9000, 7000));
        Assert.True(_connectors.Reroute(slide));

        var after = (ObjectElement)slide.Find("c1")!;
        Assert.True(after.Bounds.Width.Value != before.Width.Value || after.Bounds.Height.Value != before.Height.Value);
    }
}
