using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round213ConnectorFollowTests
{
    private readonly IDeckConnectors _connectors = new DeckConnectors(new ConnectorFramer());

    private ObjectElement Connector(string? start, string? end) => new()
    {
        Id = "c",
        Bounds = new DocumentRect(new Twip(2000), new Twip(500), new Twip(4000), new Twip(2000)),
        Content = new ConnectorObject
        {
            Id = "c",
            Shape = ConnectorKind.Straight,
            Start = new ConnectorEndpoint(start),
            End = new ConnectorEndpoint(end),
            Path = new ShapeOutline([new ShapeFigure([new MoveSegment(new ShapePoint(0, 0)), new LineSegment(new ShapePoint(1, 1))])]),
        },
    };

    private (double X, double Y) End(Slide slide, bool last)
    {
        var element = slide.Elements.OfType<ObjectElement>().Single(candidate => candidate.Id == "c");
        var points = new SegmentEnds().Of(((ConnectorObject)element.Content).Path).ToList();
        var point = last ? points[^1] : points[0];
        return (element.Bounds.Left.Value + point.X * element.Bounds.Width.Value, element.Bounds.Top.Value + point.Y * element.Bounds.Height.Value);
    }

    [Fact]
    public void A_connector_glued_at_one_end_to_a_text_box_follows_it_and_keeps_its_free_end()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(new TextElement
        {
            Id = "t",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(1000)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("x")]),
        });
        slide.Add(Connector("t", null));

        Assert.True(_connectors.Reroute(slide));
        var free = End(slide, last: true);
        Assert.Equal((6000, 2500), (Math.Round(free.X), Math.Round(free.Y)));
        Assert.Equal((1000, 500), (Math.Round(End(slide, last: false).X), Math.Round(End(slide, last: false).Y)));

        for (var pass = 0; pass < 5; pass++) _connectors.Reroute(slide);
        var again = End(slide, last: true);
        Assert.Equal((Math.Round(free.X), Math.Round(free.Y)), (Math.Round(again.X), Math.Round(again.Y)));
    }
}
