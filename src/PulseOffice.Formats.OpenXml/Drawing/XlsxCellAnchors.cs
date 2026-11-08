using DocumentFormat.OpenXml;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxCellAnchors(ISheetPlacement placement)
{
    public XlsxCellAnchors() : this(new SheetPlacement()) { }

    public Xdr.TwoCellAnchor Create(SheetLayout layout, PlacedObject placed, params OpenXmlElement[] content)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(placed);

        var end = placement.End(layout, placed);
        var anchor = new Xdr.TwoCellAnchor(
            Marker(new Xdr.FromMarker(), placed.Anchor, placed.OffsetX, placed.OffsetY),
            Marker(new Xdr.ToMarker(), end.Cell, end.X, end.Y))
        { EditAs = EditAs(placed.Anchoring) };

        foreach (var element in content) anchor.AppendChild(element);
        return anchor;
    }

    public Xdr.EditAsValues EditAs(ObjectAnchoring anchoring) => anchoring switch
    {
        ObjectAnchoring.MoveAndSize => Xdr.EditAsValues.TwoCell,
        ObjectAnchoring.Fixed => Xdr.EditAsValues.Absolute,
        _ => Xdr.EditAsValues.OneCell,
    };

    public T Marker<T>(T marker, CellAddress cell, Twip x, Twip y) where T : OpenXmlCompositeElement
    {
        marker.Append(
            new Xdr.ColumnId(cell.Column.ToString()),
            new Xdr.ColumnOffset(Emu.FromTwips(x).Value.ToString()),
            new Xdr.RowId(cell.Row.ToString()),
            new Xdr.RowOffset(Emu.FromTwips(y).Value.ToString()));
        return marker;
    }
}
