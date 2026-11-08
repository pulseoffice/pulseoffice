using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public interface IDeckGrouping
{
    ObjectElement? Group(IReadOnlyList<SlideElement> elements, string groupId);

    IReadOnlyList<ObjectElement>? Ungroup(ObjectElement group, Func<string> nextId);
}

public sealed class DeckGrouping : IDeckGrouping
{
    public ObjectElement? Group(IReadOnlyList<SlideElement> elements, string groupId)
    {
        var members = elements.OfType<ObjectElement>().OrderBy(e => e.ZOrder).ToList();
        if (members.Count < 2) return null;

        var left = members.Min(e => e.Bounds.Left.Value);
        var top = members.Min(e => e.Bounds.Top.Value);
        var right = members.Max(e => e.Bounds.Left.Value + e.Bounds.Width.Value);
        var bottom = members.Max(e => e.Bounds.Top.Value + e.Bounds.Height.Value);

        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);

        var children = members.Select(e => new GroupChild(
            e.Content,
            (e.Bounds.Left.Value - left) / (double)width,
            (e.Bounds.Top.Value - top) / (double)height,
            e.Bounds.Width.Value / (double)width,
            e.Bounds.Height.Value / (double)height) { RotationDegrees = e.RotationDegrees }).ToList();

        return new ObjectElement
        {
            Id = groupId,
            Bounds = new DocumentRect(new Twip(left), new Twip(top), new Twip(width), new Twip(height)),
            ZOrder = members.Max(e => e.ZOrder),
            Content = new ShapeGroupObject { Id = groupId, Children = children },
        };
    }

    public IReadOnlyList<ObjectElement>? Ungroup(ObjectElement group, Func<string> nextId)
    {
        if (group.Content is not ShapeGroupObject) return null;
        return Explode(group, nextId);
    }

    public static IReadOnlyList<ObjectElement> Explode(ObjectElement group) =>
        Explode(group, Counter(group.Id));

    private static IReadOnlyList<ObjectElement> Explode(ObjectElement group, Func<string> nextId)
    {
        if (group.Content is not ShapeGroupObject shapeGroup) return [group];

        var bounds = group.Bounds;
        var result = new List<ObjectElement>(shapeGroup.Children.Count);
        var z = group.ZOrder;

        foreach (var child in shapeGroup.Children)
        {
            var (x, y, w, h) = child.Scaled(bounds.Width.Value, bounds.Height.Value);
            var (centreX, centreY) = Rotated(
                bounds.Left.Value + x + w / 2, bounds.Top.Value + y + h / 2,
                bounds.Left.Value + bounds.Width.Value / 2.0, bounds.Top.Value + bounds.Height.Value / 2.0,
                group.RotationDegrees + shapeGroup.RotationDegrees);
            result.Add(new ObjectElement
            {
                Id = nextId(),
                Bounds = new DocumentRect(
                    new Twip((int)Math.Round(centreX - w / 2)),
                    new Twip((int)Math.Round(centreY - h / 2)),
                    new Twip((int)Math.Round(w)),
                    new Twip((int)Math.Round(h))),
                RotationDegrees = Normalized(child.RotationDegrees + group.RotationDegrees + shapeGroup.RotationDegrees),
                ZOrder = z++,
                Content = child.Content,
            });
        }

        return result;
    }

    private static (double X, double Y) Rotated(double x, double y, double centreX, double centreY, double degrees)
    {
        if (degrees == 0 || !double.IsFinite(degrees)) return (x, y);
        var radians = degrees * Math.PI / 180;
        var (dx, dy) = (x - centreX, y - centreY);
        return (centreX + dx * Math.Cos(radians) - dy * Math.Sin(radians), centreY + dx * Math.Sin(radians) + dy * Math.Cos(radians));
    }

    private static double Normalized(double degrees) =>
        double.IsFinite(degrees) ? (degrees % 360 + 360) % 360 : 0;

    private static Func<string> Counter(string prefix)
    {
        var index = 0;
        return () => $"{prefix}.c{index++}";
    }
}
