using System.Runtime.CompilerServices;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public readonly record struct AnchorGeometry(CellPoint Start, CellPoint End, PlacementBox Absolute);

public interface ISheetObjectAnchoring
{
    AnchorGeometry Capture(SheetLayout layout, PlacedObject placed);

    PlacedObject Settle(SheetLayout layout, PlacedObject placed, AnchorGeometry geometry);
}

public sealed class SheetObjectAnchoring(ISheetPlacement placement) : ISheetObjectAnchoring
{
    private readonly CellAddress _origin = new(0, 0);

    public SheetObjectAnchoring() : this(new SheetPlacement()) { }

    public AnchorGeometry Capture(SheetLayout layout, PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(placed);

        var start = new CellPoint(placed.Anchor, placed.OffsetX, placed.OffsetY);
        var absolute = new PlacementBox(
            layout.ColumnOffset(placed.Anchor.Column) + placed.OffsetX,
            layout.RowOffset(placed.Anchor.Row) + placed.OffsetY,
            placed.Size.Width, placed.Size.Height);
        return new AnchorGeometry(start, placement.End(layout, placed), absolute);
    }

    public PlacedObject Settle(SheetLayout layout, PlacedObject placed, AnchorGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(placed);

        switch (placed.Anchoring)
        {
            case ObjectAnchoring.Fixed:
                return placement.Place(layout, _origin, placed, geometry.Absolute);
            case ObjectAnchoring.MoveAndSize:
                var startX = Within(geometry.Start.X, layout.ColumnWidth(geometry.Start.Cell.Column));
                var startY = Within(geometry.Start.Y, layout.RowHeight(geometry.Start.Cell.Row));
                var left = layout.ColumnOffset(geometry.Start.Cell.Column).Value + startX.Value;
                var top = layout.RowOffset(geometry.Start.Cell.Row).Value + startY.Value;
                var right = layout.ColumnOffset(geometry.End.Cell.Column).Value + Within(geometry.End.X, layout.ColumnWidth(geometry.End.Cell.Column)).Value;
                var bottom = layout.RowOffset(geometry.End.Cell.Row).Value + Within(geometry.End.Y, layout.RowHeight(geometry.End.Cell.Row)).Value;
                return placed with
                {
                    Anchor = geometry.Start.Cell,
                    OffsetX = startX,
                    OffsetY = startY,
                    Size = new DocumentSize(new Twip(Math.Max(0, right - left)), new Twip(Math.Max(0, bottom - top))),
                };
            default:
                return placed with { Anchor = geometry.Start.Cell, OffsetX = geometry.Start.X, OffsetY = geometry.Start.Y };
        }
    }

    private Twip Within(Twip offset, Twip extent) => new(Math.Clamp(offset.Value, 0, Math.Max(0, extent.Value)));
}

public sealed class SheetObjectFollower
{
    private readonly Worksheet _sheet;
    private readonly ISheetObjectAnchoring _anchoring;
    private readonly ConditionalWeakTable<PlacedObject, StrongBox<AnchorGeometry>> _markers = new();
    private List<(PlacedObject Placed, AnchorGeometry Geometry)> _captured = [];

    public SheetObjectFollower(Worksheet sheet, ISheetObjectAnchoring anchoring)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _anchoring = anchoring ?? throw new ArgumentNullException(nameof(anchoring));
        sheet.Layout.GeometryChanging += Capture;
        sheet.Layout.GeometryChanged += Settle;
    }

    private void Capture(SheetLayout layout) =>
        _captured = [.. _sheet.Objects
            .Where(placed => placed.Anchoring != ObjectAnchoring.Move)
            .Select(placed => (placed, Markers(layout, placed)))];

    private AnchorGeometry Markers(SheetLayout layout, PlacedObject placed) =>
        placed.Anchoring == ObjectAnchoring.MoveAndSize && _markers.TryGetValue(placed, out var settled)
            ? settled.Value
            : _anchoring.Capture(layout, placed);

    private void Settle(SheetLayout layout)
    {
        var captured = _captured;
        _captured = [];
        foreach (var (placed, geometry) in captured)
        {
            var index = _sheet.Objects.FindIndex(candidate => ReferenceEquals(candidate, placed));
            if (index < 0) continue;
            var moved = _anchoring.Settle(layout, placed, geometry);
            _sheet.Objects[index] = moved;
            if (moved.Anchoring == ObjectAnchoring.MoveAndSize) _markers.AddOrUpdate(moved, new StrongBox<AnchorGeometry>(geometry));
        }
    }
}
