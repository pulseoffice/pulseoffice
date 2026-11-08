using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxSlideGroupWriter(ISlideGroupDecomposition decomposition) : SlideGroupWriter<OpenXmlElement>(decomposition)
{
    protected override IReadOnlyList<OpenXmlElement> InDocumentOrder(IReadOnlyList<OpenXmlElement> members)
    {
        var order = Root(members[0]).Descendants().Select((node, index) => (node, index))
            .ToDictionary(pair => (object)pair.node, pair => pair.index, ReferenceEqualityComparer.Instance);
        return [.. members.OrderBy(member => order.TryGetValue(member, out var index) ? index : int.MaxValue)];
    }

    protected override OpenXmlElement Enclose(SlideGroup group, IReadOnlyList<OpenXmlElement> members)
    {
        var id = NextId(Root(members[0]));
        var boxes = members.Select(Box).OfType<(long X, long Y, long Width, long Height)>().ToList();
        var left = boxes.Count > 0 ? boxes.Min(box => box.X) : 0;
        var top = boxes.Count > 0 ? boxes.Min(box => box.Y) : 0;
        var width = boxes.Count > 0 ? Math.Max(1, boxes.Max(box => box.X + box.Width) - left) : 1;
        var height = boxes.Count > 0 ? Math.Max(1, boxes.Max(box => box.Y + box.Height) - top) : 1;

        var shape = new P.GroupShape(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = group.Name ?? $"Group {id}" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup(
                new D.Offset { X = left, Y = top },
                new D.Extents { Cx = width, Cy = height },
                new D.ChildOffset { X = left, Y = top },
                new D.ChildExtents { Cx = width, Cy = height }) { Rotation = Rotation(group.RotationDegrees) }));
        members[0].InsertBeforeSelf(shape);
        foreach (var member in members)
        {
            member.Remove();
            shape.Append(member);
        }
        return shape;
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OpenXmlElement, System.Runtime.CompilerServices.StrongBox<uint>> _lastIds = new();

    private uint NextId(OpenXmlElement root)
    {
        var last = _lastIds.GetValue(root, tree => new System.Runtime.CompilerServices.StrongBox<uint>(
            tree.Descendants<P.NonVisualDrawingProperties>().Select(drawing => drawing.Id?.Value ?? 0U).DefaultIfEmpty(1U).Max()));
        return ++last.Value;
    }

    private Int32Value? Rotation(double degrees)
    {
        if (!double.IsFinite(degrees)) return null;
        var angle = (int)(((long)Math.Round(degrees % 360 * 60000) % 21600000 + 21600000) % 21600000);
        return angle == 0 ? null : angle;
    }

    private OpenXmlElement Root(OpenXmlElement node) => node.Ancestors().LastOrDefault() ?? node;

    private (long X, long Y, long Width, long Height)? Box(OpenXmlElement member) => member switch
    {
        P.GroupShape nested when nested.GroupShapeProperties?.TransformGroup is { } frame => Box(frame.Offset, frame.Extents),
        P.GraphicFrame graphic when graphic.Transform is { } transform => Box(transform.Offset, transform.Extents),
        _ when member.Descendants<D.Transform2D>().FirstOrDefault() is { } transform => Box(transform.Offset, transform.Extents),
        _ => null,
    };

    private (long X, long Y, long Width, long Height)? Box(D.Offset? offset, D.Extents? extents) =>
        offset is null || extents is null ? null
            : (offset.X?.Value ?? 0, offset.Y?.Value ?? 0, extents.Cx?.Value ?? 0, extents.Cy?.Value ?? 0);
}
