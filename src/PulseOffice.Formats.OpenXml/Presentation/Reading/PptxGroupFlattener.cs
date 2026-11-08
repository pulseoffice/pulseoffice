using DocumentFormat.OpenXml;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed record PptxGroupShape(uint Id, string? Name, IReadOnlyList<uint> Children, double RotationDegrees = 0);

internal sealed record PptxGroups(IReadOnlyDictionary<uint, IReadOnlyList<uint>> Members, IReadOnlyList<PptxGroupShape> Shapes)
{
    public static readonly PptxGroups None = new(new Dictionary<uint, IReadOnlyList<uint>>(), []);
}

internal sealed class PptxGroupFlattener
{
    private const int FullCircle = 21600000;

    private readonly PptxElementVisibility _visibility = new();

    public PptxGroups Flatten(P.ShapeTree? tree)
    {
        if (tree is null) return PptxGroups.None;
        var members = Members(tree);
        var shapes = Shapes(tree);

        while (tree.Elements<P.GroupShape>().Any())
        {
            var current = tree.ChildElements.ToList();
            tree.RemoveAllChildren();
            foreach (var child in current)
                if (child is P.GroupShape group) tree.Append(Dissolve(group));
                else tree.Append(child);
        }

        return new PptxGroups(members, shapes);
    }

    private List<OpenXmlElement> Dissolve(P.GroupShape group)
    {
        var frame = group.GroupShapeProperties?.TransformGroup;
        var hidden = group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Hidden?.Value == true;
        var children = group.ChildElements
            .Where(child => child is not P.NonVisualGroupShapeProperties and not P.GroupShapeProperties)
            .ToList();
        group.RemoveAllChildren();
        foreach (var child in children)
        {
            if (hidden) _visibility.Inherit(child);
            if (frame is not null) Place(child, frame);
        }

        return children;
    }

    private List<PptxGroupShape> Shapes(P.ShapeTree tree) =>
        [.. tree.Descendants<P.GroupShape>()
            .Where(group => group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Id?.Value is not null)
            .Select(group => new PptxGroupShape(
                group.NonVisualGroupShapeProperties!.NonVisualDrawingProperties!.Id!.Value,
                group.NonVisualGroupShapeProperties.NonVisualDrawingProperties.Name?.Value,
                [.. group.ChildElements
                    .Where(child => child is not P.NonVisualGroupShapeProperties and not P.GroupShapeProperties)
                    .Select(child => child is P.GroupShape nested
                        ? nested.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Id?.Value
                        : child.Descendants<P.NonVisualDrawingProperties>().FirstOrDefault()?.Id?.Value)
                    .OfType<uint>()],
                (group.GroupShapeProperties?.TransformGroup?.Rotation?.Value ?? 0) / 60000.0))];

    private Dictionary<uint, IReadOnlyList<uint>> Members(P.ShapeTree tree)
    {
        var members = new Dictionary<uint, IReadOnlyList<uint>>();
        foreach (var group in tree.Descendants<P.GroupShape>())
            if (group.NonVisualGroupShapeProperties?.NonVisualDrawingProperties?.Id?.Value is { } id)
                members[id] = [.. group.Descendants<P.NonVisualDrawingProperties>()
                    .Where(drawing => drawing.Parent?.Parent is not P.GroupShape)
                    .Select(drawing => drawing.Id?.Value).OfType<uint>()];
        return members;
    }

    private void Place(OpenXmlElement child, D.TransformGroup frame)
    {
        var offsetX = frame.Offset?.X?.Value ?? 0;
        var offsetY = frame.Offset?.Y?.Value ?? 0;
        var width = frame.Extents?.Cx?.Value ?? 0;
        var height = frame.Extents?.Cy?.Value ?? 0;
        var childX = frame.ChildOffset?.X?.Value ?? offsetX;
        var childY = frame.ChildOffset?.Y?.Value ?? offsetY;
        var childWidth = frame.ChildExtents?.Cx?.Value ?? width;
        var childHeight = frame.ChildExtents?.Cy?.Value ?? height;
        var scaleX = childWidth > 0 ? width / (double)childWidth : 1;
        var scaleY = childHeight > 0 ? height / (double)childHeight : 1;
        var flipH = frame.HorizontalFlip?.Value == true;
        var flipV = frame.VerticalFlip?.Value == true;
        var rotation = (frame.Rotation?.Value ?? 0) / 60000.0;

        var box = Box(child);
        if (box is null) return;

        var (x, y, w, h, childRotation, childFlipH, childFlipV) = box.Value;
        var centreX = offsetX + ((x + w / 2.0) - childX) * scaleX;
        var centreY = offsetY + ((y + h / 2.0) - childY) * scaleY;
        var groupCentreX = offsetX + width / 2.0;
        var groupCentreY = offsetY + height / 2.0;

        if (flipH) { centreX = 2 * groupCentreX - centreX; childFlipH = !childFlipH; childRotation = -childRotation; }
        if (flipV) { centreY = 2 * groupCentreY - centreY; childFlipV = !childFlipV; childRotation = -childRotation; }

        if (rotation != 0)
        {
            var radians = rotation * Math.PI / 180;
            var (dx, dy) = (centreX - groupCentreX, centreY - groupCentreY);
            centreX = groupCentreX + dx * Math.Cos(radians) - dy * Math.Sin(radians);
            centreY = groupCentreY + dx * Math.Sin(radians) + dy * Math.Cos(radians);
        }

        var newWidth = w * scaleX;
        var newHeight = h * scaleY;
        Write(child, (long)Math.Round(centreX - newWidth / 2), (long)Math.Round(centreY - newHeight / 2),
            (long)Math.Round(newWidth), (long)Math.Round(newHeight), childRotation + rotation, childFlipH, childFlipV);
    }

    private (long X, long Y, long Width, long Height, double Rotation, bool FlipH, bool FlipV)? Box(OpenXmlElement child)
    {
        if (child is P.GroupShape nested && nested.GroupShapeProperties?.TransformGroup is { } group)
            return (group.Offset?.X?.Value ?? 0, group.Offset?.Y?.Value ?? 0, group.Extents?.Cx?.Value ?? 0, group.Extents?.Cy?.Value ?? 0,
                (group.Rotation?.Value ?? 0) / 60000.0, group.HorizontalFlip?.Value == true, group.VerticalFlip?.Value == true);

        if (child is P.GraphicFrame frame && frame.Transform is { } transform)
            return (transform.Offset?.X?.Value ?? 0, transform.Offset?.Y?.Value ?? 0, transform.Extents?.Cx?.Value ?? 0, transform.Extents?.Cy?.Value ?? 0,
                (transform.Rotation?.Value ?? 0) / 60000.0, transform.HorizontalFlip?.Value == true, transform.VerticalFlip?.Value == true);

        if (child.Descendants<D.Transform2D>().FirstOrDefault() is { } xfrm)
            return (xfrm.Offset?.X?.Value ?? 0, xfrm.Offset?.Y?.Value ?? 0, xfrm.Extents?.Cx?.Value ?? 0, xfrm.Extents?.Cy?.Value ?? 0,
                (xfrm.Rotation?.Value ?? 0) / 60000.0, xfrm.HorizontalFlip?.Value == true, xfrm.VerticalFlip?.Value == true);

        return null;
    }

    private void Write(OpenXmlElement child, long x, long y, long width, long height, double degrees, bool flipH, bool flipV)
    {
        var rot = (int)((((long)Math.Round(degrees * 60000)) % FullCircle + FullCircle) % FullCircle);

        if (child is P.GroupShape nested && nested.GroupShapeProperties?.TransformGroup is { } group)
        {
            group.Offset = new D.Offset { X = x, Y = y };
            group.Extents = new D.Extents { Cx = width, Cy = height };
            group.Rotation = rot == 0 ? null : rot;
            group.HorizontalFlip = flipH ? true : null;
            group.VerticalFlip = flipV ? true : null;
            return;
        }

        if (child is P.GraphicFrame frame && frame.Transform is { } transform)
        {
            transform.Offset = new D.Offset { X = x, Y = y };
            transform.Extents = new D.Extents { Cx = width, Cy = height };
            return;
        }

        if (child.Descendants<D.Transform2D>().FirstOrDefault() is { } xfrm)
        {
            xfrm.Offset = new D.Offset { X = x, Y = y };
            xfrm.Extents = new D.Extents { Cx = width, Cy = height };
            xfrm.Rotation = rot == 0 ? null : rot;
            xfrm.HorizontalFlip = flipH ? true : null;
            xfrm.VerticalFlip = flipV ? true : null;
        }
    }
}
