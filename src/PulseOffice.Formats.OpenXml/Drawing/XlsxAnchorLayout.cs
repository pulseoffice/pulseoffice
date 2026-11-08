using DocumentFormat.OpenXml;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxAnchorLayout(SheetLayout layout, ISheetPlacement placement)
{
    private readonly CellAddress _origin = new(0, 0);
    private readonly Dictionary<OpenXmlElement, int> _orders = new(ReferenceEqualityComparer.Instance);
    private OpenXmlElement? _indexedDrawing;

    public XlsxAnchorLayout(SheetLayout layout) : this(layout, new SheetPlacement()) { }

    public SheetLayout Layout => layout;

    public PlacedObject Place(OpenXmlElement anchor, PlacedObject seed, DocumentSize fallback)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(seed);

        seed = seed with { Anchoring = Anchoring(anchor) };
        var box = Box(anchor, fallback);
        if (Transform(anchor) is { } extent && extent.Width > 0 && extent.Height > 0)
        {
            var centreX = box.Left.Value + box.Width.Value / 2.0;
            var centreY = box.Top.Value + box.Height.Value / 2.0;
            box = new PlacementBox(
                new Twip((int)Math.Round(centreX - extent.Width / 2.0)),
                new Twip((int)Math.Round(centreY - extent.Height / 2.0)),
                new Twip(extent.Width), new Twip(extent.Height));
        }

        else if (anchor is not Xdr.AbsoluteAnchor && Cell(anchor.GetFirstChild<Xdr.FromMarker>()) is { } cell)
            return seed with { Anchor = cell.Address, OffsetX = cell.X, OffsetY = cell.Y, Size = new DocumentSize(box.Width, box.Height), ZOrder = Order(anchor) };

        box = box with { Left = new Twip(Math.Max(0, box.Left.Value)), Top = new Twip(Math.Max(0, box.Top.Value)) };
        return placement.Place(layout, _origin, seed, box) with { ZOrder = Order(anchor) };
    }

    public ObjectAnchoring Anchoring(OpenXmlElement anchor) => anchor switch
    {
        Xdr.AbsoluteAnchor => ObjectAnchoring.Fixed,
        Xdr.OneCellAnchor => ObjectAnchoring.Move,
        Xdr.TwoCellAnchor { EditAs.Value: var edit } when edit == Xdr.EditAsValues.OneCell => ObjectAnchoring.Move,
        Xdr.TwoCellAnchor { EditAs.Value: var edit } when edit == Xdr.EditAsValues.Absolute => ObjectAnchoring.Fixed,
        Xdr.TwoCellAnchor => ObjectAnchoring.MoveAndSize,
        _ => ObjectAnchoring.Move,
    };

    private (CellAddress Address, Twip X, Twip Y)? Cell(OpenXmlElement? marker)
    {
        if (marker is null) return null;
        var column = Whole(marker.GetFirstChild<Xdr.ColumnId>()?.Text);
        var row = Whole(marker.GetFirstChild<Xdr.RowId>()?.Text);
        if (column < 0 || row < 0 || column >= CellAddress.MaxColumns || row >= CellAddress.MaxRows) return null;
        var dx = new Emu(Long(marker.GetFirstChild<Xdr.ColumnOffset>()?.Text)).ToTwips();
        var dy = new Emu(Long(marker.GetFirstChild<Xdr.RowOffset>()?.Text)).ToTwips();
        return Within(dx.Value, layout.ColumnWidth(column).Value) && Within(dy.Value, layout.RowHeight(row).Value)
            ? (new CellAddress(row, column), dx, dy)
            : null;
    }

    private bool Within(int offset, int extent) => offset == 0 || (offset > 0 && offset < extent);

    public PlacementBox Markers(OpenXmlElement? from, OpenXmlElement? to)
    {
        var start = Point(from);
        var end = to is null ? start : Point(to);
        return new PlacementBox(start.X, start.Y, new Twip(Math.Max(0, end.X.Value - start.X.Value)), new Twip(Math.Max(0, end.Y.Value - start.Y.Value)));
    }

    public int Order(OpenXmlElement anchor)
    {
        var top = anchor;
        while (top.Parent is { } parent and not Xdr.WorksheetDrawing) top = parent;
        if (top.Parent is not { } drawing) return 0;
        if (!ReferenceEquals(drawing, _indexedDrawing))
        {
            _orders.Clear();
            var index = 0;
            foreach (var child in drawing.ChildElements) _orders[child] = index++;
            _indexedDrawing = drawing;
        }

        return _orders.GetValueOrDefault(top, -1);
    }

    private PlacementBox Box(OpenXmlElement anchor, DocumentSize fallback)
    {
        var extent = anchor.GetFirstChild<Xdr.Extent>();
        var size = new DocumentSize(
            extent?.Cx?.Value is { } cx and > 0 ? new Emu(cx).ToTwips() : fallback.Width,
            extent?.Cy?.Value is { } cy and > 0 ? new Emu(cy).ToTwips() : fallback.Height);

        switch (anchor)
        {
            case Xdr.TwoCellAnchor two:
                var box = Markers(two.FromMarker, two.ToMarker);
                return box.Width.Value > 0 && box.Height.Value > 0 ? box : box with { Width = size.Width, Height = size.Height };
            case Xdr.AbsoluteAnchor absolute:
                return new PlacementBox(
                    new Emu(absolute.Position?.X?.Value ?? 0).ToTwips(), new Emu(absolute.Position?.Y?.Value ?? 0).ToTwips(),
                    size.Width, size.Height);
            default:
                var start = Point(anchor.GetFirstChild<Xdr.FromMarker>());
                return new PlacementBox(start.X, start.Y, size.Width, size.Height);
        }
    }

    private (Twip X, Twip Y) Point(OpenXmlElement? marker)
    {
        var column = Whole(marker?.GetFirstChild<Xdr.ColumnId>()?.Text);
        var row = Whole(marker?.GetFirstChild<Xdr.RowId>()?.Text);
        var dx = new Emu(Long(marker?.GetFirstChild<Xdr.ColumnOffset>()?.Text)).ToTwips();
        var dy = new Emu(Long(marker?.GetFirstChild<Xdr.RowOffset>()?.Text)).ToTwips();

        return (layout.ColumnOffset(Math.Clamp(column, 0, CellAddress.MaxColumns - 1)) + dx,
            layout.RowOffset(Math.Clamp(row, 0, CellAddress.MaxRows - 1)) + dy);
    }

    private (int Width, int Height)? Transform(OpenXmlElement anchor)
    {
        var content = anchor.ChildElements.FirstOrDefault(child => child is Xdr.Shape or Xdr.Picture or Xdr.ConnectionShape);
        var transform = content?.GetFirstChild<Xdr.ShapeProperties>()?.GetFirstChild<A.Transform2D>();
        if ((transform?.Rotation?.Value ?? 0) % 21600000 == 0) return null;
        var extents = transform?.Extents;
        if (extents?.Cx?.Value is not { } cx || extents.Cy?.Value is not { } cy || cx <= 0 || cy <= 0) return null;
        return (new Emu(cx).ToTwips().Value, new Emu(cy).ToTwips().Value);
    }

    private int Whole(string? text) => int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    private long Long(string? text) => long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
}
