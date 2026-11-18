using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlConnectionSites
{
    public uint Index(ConnectorSite site) => site switch
    {
        ConnectorSite.Top => 0U,
        ConnectorSite.Left => 1U,
        ConnectorSite.Bottom => 2U,
        _ => 3U,
    };

    public ConnectorSite Site(uint? index) => index switch
    {
        0U => ConnectorSite.Top,
        1U => ConnectorSite.Left,
        2U => ConnectorSite.Bottom,
        3U => ConnectorSite.Right,
        _ => ConnectorSite.Automatic,
    };

    public ConnectorEndpoint Endpoint(D.StartConnection? connection) =>
        connection?.Id?.Value is { } id ? new ConnectorEndpoint(id.ToString(CultureInfo.InvariantCulture), Site(connection.Index?.Value)) : ConnectorEndpoint.Free;

    public ConnectorEndpoint Endpoint(D.EndConnection? connection) =>
        connection?.Id?.Value is { } id ? new ConnectorEndpoint(id.ToString(CultureInfo.InvariantCulture), Site(connection.Index?.Value)) : ConnectorEndpoint.Free;

    public bool KeepsSites(SlideElement target) =>
        target is TextElement || target is ObjectElement { Content: ShapeObject { Shape: ShapeKind.Rectangle or ShapeKind.RoundedRectangle } };

    public ConnectorSite Facing(ConnectorSite site, (double X, double Y) point, PulseOffice.Foundation.Units.DocumentRect target)
    {
        if (site != ConnectorSite.Automatic) return site;
        var distances = new (ConnectorSite Site, double Distance)[]
        {
            (ConnectorSite.Top, Math.Abs(point.Y - target.Top.Value)),
            (ConnectorSite.Left, Math.Abs(point.X - target.Left.Value)),
            (ConnectorSite.Bottom, Math.Abs(point.Y - (target.Top.Value + target.Height.Value))),
            (ConnectorSite.Right, Math.Abs(point.X - (target.Left.Value + target.Width.Value))),
        };
        return distances.MinBy(entry => entry.Distance).Site;
    }
}
