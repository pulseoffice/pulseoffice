using PulseOffice.Foundation.Units;

namespace PulseOffice.Sheets;

public enum ObjectAlignmentEdge
{
    Left,
    HorizontalCentre,
    Right,
    Top,
    VerticalMiddle,
    Bottom,
}

public enum ObjectDistribution
{
    Horizontal,
    Vertical,
}

public interface IObjectAlignment
{
    IReadOnlyList<PlacedObject> Align(
        Worksheet sheet, IReadOnlyList<PlacedObject> objects, ObjectAlignmentEdge edge);

    IReadOnlyList<PlacedObject> Distribute(
        Worksheet sheet, IReadOnlyList<PlacedObject> objects, ObjectDistribution direction);
}

public sealed class ObjectAlignment : IObjectAlignment
{
    private readonly ISheetPlacement _placement;

    public ObjectAlignment(ISheetPlacement placement) => _placement = placement;

    public ObjectAlignment() : this(new SheetPlacement()) { }

    public IReadOnlyList<PlacedObject> Align(
        Worksheet sheet, IReadOnlyList<PlacedObject> objects, ObjectAlignmentEdge edge)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(objects);

        if (objects.Count < 2) return [];

        var origin = Origin(objects);
        var boxes = objects.ToDictionary(placed => placed, placed => _placement.Box(sheet.Layout, origin, placed));

        var left = boxes.Values.Min(box => box.Left.Value);
        var right = boxes.Values.Max(box => box.Right.Value);
        var top = boxes.Values.Min(box => box.Top.Value);
        var bottom = boxes.Values.Max(box => box.Bottom.Value);

        var moved = new List<PlacedObject>(objects.Count);

        foreach (var placed in objects)
        {
            var box = boxes[placed];

            var target = edge switch
            {
                ObjectAlignmentEdge.Left => box with { Left = new Twip(left) },
                ObjectAlignmentEdge.Right => box with { Left = new Twip(right - box.Width.Value) },
                ObjectAlignmentEdge.HorizontalCentre => box with
                {
                    Left = new Twip((left + right - box.Width.Value) / 2),
                },
                ObjectAlignmentEdge.Top => box with { Top = new Twip(top) },
                ObjectAlignmentEdge.Bottom => box with { Top = new Twip(bottom - box.Height.Value) },
                _ => box with { Top = new Twip((top + bottom - box.Height.Value) / 2) },
            };

            moved.Add(_placement.Place(sheet.Layout, origin, placed, target));
        }

        return moved;
    }

    public IReadOnlyList<PlacedObject> Distribute(
        Worksheet sheet, IReadOnlyList<PlacedObject> objects, ObjectDistribution direction)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(objects);

        if (objects.Count < 3) return [];

        var origin = Origin(objects);

        var ordered = objects
            .Select(placed => (Placed: placed, Box: _placement.Box(sheet.Layout, origin, placed)))
            .OrderBy(entry => direction is ObjectDistribution.Horizontal
                ? entry.Box.Left.Value
                : entry.Box.Top.Value)
            .ToList();

        var first = ordered[0].Box;
        var span = direction is ObjectDistribution.Horizontal
            ? ordered.Max(entry => entry.Box.Right.Value) - first.Left.Value
            : ordered.Max(entry => entry.Box.Bottom.Value) - first.Top.Value;

        var occupied = ordered.Sum(entry => direction is ObjectDistribution.Horizontal
            ? entry.Box.Width.Value
            : entry.Box.Height.Value);

        var gap = (span - occupied) / (double)(ordered.Count - 1);

        var moved = new List<PlacedObject>(ordered.Count);
        var cursor = (double)(direction is ObjectDistribution.Horizontal ? first.Left.Value : first.Top.Value);

        foreach (var (placed, box) in ordered)
        {
            var target = direction is ObjectDistribution.Horizontal
                ? box with { Left = new Twip((int)Math.Round(cursor)) }
                : box with { Top = new Twip((int)Math.Round(cursor)) };

            moved.Add(_placement.Place(sheet.Layout, origin, placed, target));

            cursor += (direction is ObjectDistribution.Horizontal ? box.Width.Value : box.Height.Value) + gap;
        }

        return moved;
    }

    private static Addressing.CellAddress Origin(IReadOnlyList<PlacedObject> objects) =>
        new(objects.Min(placed => placed.Anchor.Row), objects.Min(placed => placed.Anchor.Column));
}
