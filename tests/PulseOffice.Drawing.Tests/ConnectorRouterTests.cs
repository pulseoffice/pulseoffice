using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ConnectorRouterTests
{
    private static readonly IConnectorRouter Router = new ConnectorRouter();

    private static readonly ConnectorBox Left = new(0, 0, 100, 100);
    private static readonly ConnectorBox Right = new(300, 0, 100, 100);
    private static readonly ConnectorBox Below = new(0, 300, 100, 100);

    private static IReadOnlyList<ShapePoint> Points(ShapeOutline outline) =>
    [
        .. outline.Figures[0].Segments.SelectMany<ShapeSegment, ShapePoint>(segment => segment switch
        {
            MoveSegment move => [move.To],
            LineSegment line => [line.To],
            CubicSegment cubic => [cubic.First, cubic.Second, cubic.To],
            _ => [],
        }),
    ];

    [Fact]
    public void ConDueRiquadriAffiancatiSiAttaccaAiLatiCheSiGuardano()
    {
        Assert.Equal(ConnectorSite.Right, Router.Resolve(Left, ConnectorSite.Automatic, Right));
        Assert.Equal(ConnectorSite.Left, Router.Resolve(Right, ConnectorSite.Automatic, Left));
    }

    [Fact]
    public void ConDueRiquadriSovrappostiInVerticaleSiAttaccaSopraESotto()
    {
        Assert.Equal(ConnectorSite.Bottom, Router.Resolve(Left, ConnectorSite.Automatic, Below));
        Assert.Equal(ConnectorSite.Top, Router.Resolve(Below, ConnectorSite.Automatic, Left));
    }

    [Fact]
    public void UnLatoDichiaratoVinceSullAutomatico()
    {
        Assert.Equal(ConnectorSite.Top, Router.Resolve(Left, ConnectorSite.Top, Right));
    }

    [Fact]
    public void IlPuntoDiAggancioStaSulBordoEAMeta()
    {
        var (x, y) = Router.Attachment(Left, ConnectorSite.Automatic, Right);

        Assert.Equal(100, x, 6);
        Assert.Equal(50, y, 6);
    }

    [Fact]
    public void IlTracciatoDirittoHaDuePunti()
    {
        var points = Points(Router.Route(
            ConnectorKind.Straight, Left, ConnectorSite.Automatic, Right, ConnectorSite.Automatic));

        Assert.Equal(2, points.Count);
        Assert.Equal(new ShapePoint(100, 50), points[0]);
        Assert.Equal(new ShapePoint(300, 50), points[1]);
    }

    [Fact]
    public void IlGomitoFraDueLatiOrizzontaliPassaPerLaMeta()
    {
        var points = Points(Router.Route(
            ConnectorKind.Elbow, Left, ConnectorSite.Automatic, Right, ConnectorSite.Automatic));

        Assert.Equal(4, points.Count);
        Assert.Equal(200, points[1].X, 6);
        Assert.Equal(200, points[2].X, 6);
    }

    [Fact]
    public void IlGomitoFraUnLatoOrizzontaleEUnoVerticaleGiraUnaVoltaSola()
    {
        var points = Points(Router.Route(
            ConnectorKind.Elbow, Left, ConnectorSite.Right, new ConnectorBox(300, 300, 100, 100), ConnectorSite.Top));

        Assert.Equal(3, points.Count);
    }

    [Fact]
    public void IlGomitoEsceVersoLEsternoDalLatoDiAggancio()
    {
        var points = Points(Router.Route(
            ConnectorKind.Elbow, Left, ConnectorSite.Right, Below, ConnectorSite.Top));

        Assert.True(points[1].X > 100);
        Assert.Equal(50, points[1].Y, 6);
        Assert.True(points[^2].Y < 300);
        Assert.Equal(50, points[^2].X, 6);
    }

    [Fact]
    public void IlGomitoVersoUnRiquadroAlleSpalleNonRientraNelRiquadro()
    {
        var points = Points(Router.Route(
            ConnectorKind.Elbow, Right, ConnectorSite.Right, Left, ConnectorSite.Left));

        Assert.True(points[1].X > 400);
        Assert.True(points[^2].X < 0);
    }

    [Fact]
    public void LaCurvaEUnaCubica()
    {
        var outline = Router.Route(
            ConnectorKind.Curved, Left, ConnectorSite.Automatic, Right, ConnectorSite.Automatic);

        Assert.Contains(outline.Figures[0].Segments, segment => segment is CubicSegment);
    }

    [Fact]
    public void LaCurvaEsceDalLatoDiPartenza()
    {
        var outline = Router.Route(
            ConnectorKind.Curved, Left, ConnectorSite.Automatic, Right, ConnectorSite.Automatic);

        var cubic = Assert.IsType<CubicSegment>(outline.Figures[0].Segments[1]);

        Assert.True(cubic.First.X > 100);
        Assert.Equal(50, cubic.First.Y, 6);
    }

    [Fact]
    public void IlTracciatoNonSiChiude()
    {
        var outline = Router.Route(
            ConnectorKind.Straight, Left, ConnectorSite.Automatic, Right, ConnectorSite.Automatic);

        Assert.False(outline.Figures[0].IsClosed);
    }
}
