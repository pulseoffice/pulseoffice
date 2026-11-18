using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxConnectorGlue(OoxmlConnectionSites sites)
{
    private readonly SegmentEnds _ends = new();

    public void Write(P.ConnectionShape shape, ObjectElement placed, ConnectorObject connector, Slide slide, IReadOnlyDictionary<string, uint> ids)
    {
        if (shape.NonVisualConnectionShapeProperties?.NonVisualConnectorShapeDrawingProperties is not { } visual) return;
        var points = _ends.Of(connector.Path).ToList();
        if (points.Count == 0) return;
        var first = Absolute(points[0], placed.Bounds);
        var last = Absolute(points[^1], placed.Bounds);

        if (Target(connector.Start, slide, ids) is { } start)
            visual.Append(new D.StartConnection { Id = start.Id, Index = sites.Index(sites.Facing(connector.Start.Site, first, start.Bounds)) });
        if (Target(connector.End, slide, ids) is { } end)
            visual.Append(new D.EndConnection { Id = end.Id, Index = sites.Index(sites.Facing(connector.End.Site, last, end.Bounds)) });
    }

    private (double X, double Y) Absolute(ShapePoint point, DocumentRect bounds) =>
        (bounds.Left.Value + point.X * bounds.Width.Value, bounds.Top.Value + point.Y * bounds.Height.Value);

    private (uint Id, DocumentRect Bounds)? Target(ConnectorEndpoint endpoint, Slide slide, IReadOnlyDictionary<string, uint> ids) =>
        endpoint.ObjectId is { Length: > 0 } objectId
        && ids.TryGetValue(objectId, out var id)
        && slide.Elements.FirstOrDefault(element => element.Id == objectId) is { } target
            ? (id, target.Bounds)
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
