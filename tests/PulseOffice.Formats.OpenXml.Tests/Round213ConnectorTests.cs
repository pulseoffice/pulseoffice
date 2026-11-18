using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round213ConnectorTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement { Id = "a", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle } });
        slide.Add(new ObjectElement { Id = "b", Bounds = new DocumentRect(new Twip(6000), new Twip(4000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "b", Shape = ShapeKind.Rectangle } });
        slide.Add(new ObjectElement
        {
            Id = "c",
            ZOrder = 5,
            Bounds = new DocumentRect(new Twip(3000), new Twip(1500), new Twip(3000), new Twip(3000)),
            Content = new ConnectorObject
            {
                Id = "c",
                Shape = ConnectorKind.Elbow,
                Start = new ConnectorEndpoint("a", ConnectorSite.Right),
                End = new ConnectorEndpoint("b", ConnectorSite.Left),
                Path = new ShapeOutline([new ShapeFigure(
                [
                    new MoveSegment(new ShapePoint(0, 0)),
                    new LineSegment(new ShapePoint(0.5, 0)),
                    new LineSegment(new ShapePoint(0.5, 1)),
                    new LineSegment(new ShapePoint(1, 1)),
                ])]),
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private void AssertGlued(Presentation deck)
    {
        var slide = deck.Slides[0];
        var element = slide.Elements.OfType<ObjectElement>().Single(candidate => candidate.Content is ConnectorObject);
        var connector = (ConnectorObject)element.Content;
        var from = slide.Elements.Single(candidate => candidate.Id == connector.Start.ObjectId);
        var to = slide.Elements.Single(candidate => candidate.Id == connector.End.ObjectId);
        Assert.Equal(1000, from.Bounds.Left.Value);
        Assert.Equal(6000, to.Bounds.Left.Value);
        Assert.Equal((ConnectorSite.Right, ConnectorSite.Left), (connector.Start.Site, connector.End.Site));
        Assert.Equal(ConnectorKind.Elbow, connector.Shape);
        Assert.Equal(4, connector.Path.Figures[0].Segments.Count);
        Assert.InRange(element.Bounds.Left.Value, 2999, 3001);
    }

    [Fact]
    public void A_glued_connector_keeps_its_ends_sites_and_route_through_PPTX()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        AssertGlued(new PptxReader(_themes, _masters).Read(buffer).Value!);
    }

    [Fact]
    public void A_glued_connector_keeps_its_ends_sites_and_route_through_ODP()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        AssertGlued(new OdpReader(_themes, _masters).Read(buffer).Value!);
    }
}
