using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round227DeckConnectorSitesTests
{
    private readonly IDeckConnectors _connectors = new DeckConnectors(new ConnectorFramer());

    private static ObjectElement Shape(string id, int x, int y) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(1000), new Twip(1000)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    [Fact]
    public void Resite_keeps_the_glued_objects_and_reroutes_from_the_chosen_sides()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        var a = Shape("a", 0, 0);
        var b = Shape("b", 5000, 3000);
        slide.Add(a);
        slide.Add(b);
        var placed = _connectors.Connect(slide, a, b, "c1", ConnectorKind.Elbow)!;
        slide.Add(placed);

        var resited = _connectors.Resite(slide, placed, ConnectorSite.Bottom, ConnectorSite.Top);

        var content = Assert.IsType<ConnectorObject>(resited!.Content);
        Assert.Equal(new ConnectorEndpoint("a", ConnectorSite.Bottom), content.Start);
        Assert.Equal(new ConnectorEndpoint("b", ConnectorSite.Top), content.End);
        Assert.Equal("c1", resited.Id);
        Assert.NotEqual(((ConnectorObject)placed.Content).Path, content.Path);
    }

    [Fact]
    public void Resite_ignores_elements_that_are_not_connectors()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        var a = Shape("a", 0, 0);
        slide.Add(a);

        Assert.Null(_connectors.Resite(slide, a, ConnectorSite.Left, ConnectorSite.Right));
    }
}
