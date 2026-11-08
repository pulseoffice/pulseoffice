using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public interface ISheetGrouping
{
    PlacedObject? Group(Worksheet sheet, IReadOnlyList<PlacedObject> objects, string id);

    IReadOnlyList<PlacedObject>? Ungroup(Worksheet sheet, PlacedObject placed);
}

public sealed class SheetGrouping : ISheetGrouping
{
    private readonly ISheetPlacement _placement;
    private readonly IObjectOrdering _ordering;

    public SheetGrouping(ISheetPlacement placement, IObjectOrdering ordering)
    {
        _placement = placement;
        _ordering = ordering;
    }

    public SheetGrouping() : this(new SheetPlacement(), new ObjectOrdering()) { }

    public PlacedObject? Group(Worksheet sheet, IReadOnlyList<PlacedObject> objects, string id)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (objects.Count < 2) return null;

        var ordered = _ordering.Drawn(sheet).Where(objects.Contains).ToList();
        if (ordered.Count < 2) return null;

        var origin = new CellAddress(
            ordered.Min(placed => placed.Anchor.Row),
            ordered.Min(placed => placed.Anchor.Column));

        var boxes = ordered.ToDictionary(
            placed => placed, placed => _placement.Box(sheet.Layout, origin, placed));

        var left = boxes.Values.Min(box => box.Left.Value);
        var top = boxes.Values.Min(box => box.Top.Value);
        var right = boxes.Values.Max(box => box.Right.Value);
        var bottom = boxes.Values.Max(box => box.Bottom.Value);

        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);

        var children = ordered
            .Select(placed => new GroupChild(
                placed.Content,
                (boxes[placed].Left.Value - left) / (double)width,
                (boxes[placed].Top.Value - top) / (double)height,
                boxes[placed].Width.Value / (double)width,
                boxes[placed].Height.Value / (double)height))
            .ToList();

        var group = new PlacedObject
        {
            Id = id,
            Content = new ShapeGroupObject { Id = id, Children = children },
            Anchor = origin,
            Size = new DocumentSize(new Twip(width), new Twip(height)),
            ZOrder = ordered.Max(placed => placed.ZOrder),
        };

        return _placement.Place(
            sheet.Layout, origin, group, new PlacementBox(
                new Twip(left), new Twip(top), new Twip(width), new Twip(height)));
    }

    public IReadOnlyList<PlacedObject>? Ungroup(Worksheet sheet, PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(placed);

        if (placed.Content is not ShapeGroupObject group) return null;

        var width = placed.Size.Width.Value;
        var height = placed.Size.Height.Value;

        var released = new List<PlacedObject>(group.Children.Count);
        var order = placed.ZOrder;

        foreach (var child in group.Children)
        {
            var box = new PlacementBox(
                placed.OffsetX + new Twip((int)Math.Round(child.X * width)),
                placed.OffsetY + new Twip((int)Math.Round(child.Y * height)),
                new Twip(Math.Max(1, (int)Math.Round(child.Width * width))),
                new Twip(Math.Max(1, (int)Math.Round(child.Height * height))));

            released.Add(_placement.Place(sheet.Layout, placed.Anchor, new PlacedObject
            {
                Id = child.Content.Id,
                Content = child.Content,
                Anchor = placed.Anchor,
                ZOrder = order++,
            }, box));
        }

        return released;
    }
}
