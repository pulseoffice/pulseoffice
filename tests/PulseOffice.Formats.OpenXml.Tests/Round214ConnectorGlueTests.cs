using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round214ConnectorGlueTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private readonly ShapeOutline _route = new([new ShapeFigure(
    [
        new MoveSegment(new ShapePoint(0, 0)),
        new LineSegment(new ShapePoint(1, 1)),
    ])]);

    private (Presentation Deck, Slide Slide) Empty()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private ObjectElement Box(string id, int left) =>
        new() { Id = id, Bounds = new DocumentRect(new Twip(left), new Twip(1000), new Twip(1000), new Twip(1000)), Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle } };

    private Presentation Automatic()
    {
        var (deck, slide) = Empty();
        slide.Add(Box("a", 1000));
        slide.Add(Box("b", 4000));
        slide.Add(new ObjectElement
        {
            Id = "c",
            ZOrder = 5,
            Bounds = new DocumentRect(new Twip(2000), new Twip(1500), new Twip(2000), new Twip(1)),
            Content = new ConnectorObject { Id = "c", Shape = ConnectorKind.Straight, Start = new ConnectorEndpoint("a"), End = new ConnectorEndpoint("b", ConnectorSite.Left), Path = _route },
        });
        return deck;
    }

    private Presentation Grouped()
    {
        var (deck, slide) = Empty();
        slide.Add(new ObjectElement
        {
            Id = "g",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(1000)),
            Content = new ShapeGroupObject
            {
                Id = "g",
                Children =
                [
                    new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.25, 1),
                    new GroupChild(new ShapeObject { Id = "b", Shape = ShapeKind.Rectangle }, 0.75, 0, 0.25, 1),
                    new GroupChild(new ConnectorObject { Id = "c", Shape = ConnectorKind.Straight, Start = new ConnectorEndpoint("a", ConnectorSite.Right), End = new ConnectorEndpoint("b"), Path = _route }, 0.25, 0.5, 0.5, 0),
                ],
            },
        });
        return deck;
    }

    private ConnectorObject Connector(Presentation deck) =>
        deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ConnectorObject>().Single();

    private Presentation ThroughPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    [Fact]
    public void An_automatic_end_stays_automatic_through_PPTX_beside_a_chosen_one()
    {
        var connector = Connector(ThroughPptx(Automatic()));
        Assert.Equal((ConnectorSite.Automatic, ConnectorSite.Left), (connector.Start.Site, connector.End.Site));
        Assert.True(connector.Start.IsAttached && connector.End.IsAttached);
    }

    [Fact]
    public void An_automatic_end_stays_automatic_through_ODP_beside_a_chosen_one()
    {
        var connector = Connector(ThroughOdp(Automatic()));
        Assert.Equal((ConnectorSite.Automatic, ConnectorSite.Left), (connector.Start.Site, connector.End.Site));
    }

    [Fact]
    public void A_connector_inside_a_group_keeps_its_glue_through_PPTX_and_ODP()
    {
        foreach (var read in new[] { ThroughPptx(Grouped()), ThroughOdp(Grouped()) })
        {
            var connector = Connector(read);
            Assert.True(connector.Start.IsAttached && connector.End.IsAttached);
            Assert.Equal((ConnectorSite.Right, ConnectorSite.Automatic), (connector.Start.Site, connector.End.Site));
            Assert.Contains(read.Slides[0].Elements, element => element.Id == connector.Start.ObjectId);
        }
    }
}
