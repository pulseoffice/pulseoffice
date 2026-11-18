using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxConnectorGlue(OoxmlConnectionSites sites)
{
    private readonly SegmentEnds _ends = new();
    private readonly OoxmlAutomaticGlue _automatic = new();

    public void Write(P.ConnectionShape shape, ObjectElement placed, ConnectorObject connector, PptxGlueTargets targets)
    {
        if (shape.NonVisualConnectionShapeProperties?.NonVisualConnectorShapeDrawingProperties is not { } visual) return;
        var points = _ends.Of(connector.Path).ToList();
        if (points.Count == 0) return;
        var first = Absolute(points[0], placed.Bounds);
        var last = Absolute(points[^1], placed.Bounds);

        var start = targets.Of(connector.Start, placed.Id);
        var end = targets.Of(connector.End, placed.Id);
        if (start is { } origin)
            visual.Append(new D.StartConnection { Id = origin.Id, Index = sites.Index(sites.Facing(connector.Start.Site, first, origin.Bounds)) });
        if (end is { } to)
            visual.Append(new D.EndConnection { Id = to.Id, Index = sites.Index(sites.Facing(connector.End.Site, last, to.Bounds)) });
        _automatic.Write(visual, connector, start is not null, end is not null);
    }

    private (double X, double Y) Absolute(ShapePoint point, DocumentRect bounds) =>
        (bounds.Left.Value + point.X * bounds.Width.Value, bounds.Top.Value + point.Y * bounds.Height.Value);
}

internal sealed class PptxGlueTargets
{
    private readonly Dictionary<string, (uint Id, DocumentRect Bounds, string Element)> _targets = new(StringComparer.Ordinal);

    public void Add(SlideElement element, uint id)
    {
        _targets.TryAdd(element.Id, (id, element.Bounds, element.Id));
        if (element is ObjectElement { Content.Id: { Length: > 0 } content } && content != element.Id)
            _targets.TryAdd(content, (id, element.Bounds, element.Id));
    }

    public (uint Id, DocumentRect Bounds)? Of(ConnectorEndpoint endpoint, string self) =>
        endpoint.ObjectId is { Length: > 0 } objectId && _targets.TryGetValue(objectId, out var target) && target.Element != self
            ? (target.Id, target.Bounds)
            : null;
}

internal sealed class PptxConnectorGlueReader(SlideConnectorGlue glue, OoxmlConnectionSites sites) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly) =>
        glue.Resolve(assembly.Slide,
            raw => uint.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var frame)
                && assembly.ElementByFrame.TryGetValue(frame, out var element) ? element : null,
            sites.KeepsSites);
}
