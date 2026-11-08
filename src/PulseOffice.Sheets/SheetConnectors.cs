using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public interface ISheetConnectors
{
    bool Refresh(Worksheet sheet);

    PlacedObject? Connect(
        Worksheet sheet, PlacedObject from, PlacedObject to, string id, ConnectorKind kind);
}

public sealed class SheetConnectors : ISheetConnectors
{
    private readonly ISheetPlacement _placement;
    private readonly IConnectorFramer _framer;

    public SheetConnectors(ISheetPlacement placement, IConnectorFramer framer)
    {
        _placement = placement;
        _framer = framer;
    }

    public SheetConnectors() : this(new SheetPlacement(), new ConnectorFramer(new ConnectorRouter())) { }

    public PlacedObject? Connect(
        Worksheet sheet, PlacedObject from, PlacedObject to, string id, ConnectorKind kind)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (ReferenceEquals(from, to)) return null;

        var connector = new ConnectorObject
        {
            Id = id,
            Shape = kind,
            Start = new ConnectorEndpoint(from.Id),
            End = new ConnectorEndpoint(to.Id),
        };

        var placed = new PlacedObject
        {
            Id = id,
            Content = connector,
            Anchor = from.Anchor,
            Size = new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1)),
        };

        return Reroute(sheet, placed, connector);
    }

    public bool Refresh(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var changed = false;

        for (var index = 0; index < sheet.Objects.Count; index++)
        {
            if (sheet.Objects[index].Content is not ConnectorObject connector) continue;

            var updated = Reroute(sheet, sheet.Objects[index], connector);
            if (updated is null) continue;

            if (!Same(sheet.Objects[index], updated) || connector.Path.Figures.Count == 0) changed = true;

            sheet.Objects[index] = updated;
        }

        return changed;
    }

    private bool Same(PlacedObject first, PlacedObject second) =>
        first.Anchor == second.Anchor
        && first.OffsetX.Value == second.OffsetX.Value
        && first.OffsetY.Value == second.OffsetY.Value
        && first.Size.Width.Value == second.Size.Width.Value
        && first.Size.Height.Value == second.Size.Height.Value;

    private PlacedObject? Reroute(Worksheet sheet, PlacedObject placed, ConnectorObject connector)
    {
        if (Find(sheet, connector.Start.ObjectId) is not { } from) return null;
        if (Find(sheet, connector.End.ObjectId) is not { } to) return null;

        var origin = new CellAddress(
            Math.Min(from.Anchor.Row, to.Anchor.Row),
            Math.Min(from.Anchor.Column, to.Anchor.Column));

        var fromBox = Box(sheet, origin, from);
        var toBox = Box(sheet, origin, to);

        if (_framer.Frame(connector, fromBox, toBox) is not { } framed) return null;

        var box = new PlacementBox(
            new Twip((int)Math.Round(framed.Left)), new Twip((int)Math.Round(framed.Top)),
            new Twip((int)Math.Round(framed.Width)), new Twip((int)Math.Round(framed.Height)));

        return _placement.Place(
            sheet.Layout, origin,
            placed with { Content = connector with { Path = framed.Path } },
            box);
    }

    private ConnectorBox Box(Worksheet sheet, CellAddress origin, PlacedObject placed)
    {
        var box = _placement.Box(sheet.Layout, origin, placed);

        return new ConnectorBox(box.Left.Value, box.Top.Value, box.Width.Value, box.Height.Value);
    }

    private PlacedObject? Find(Worksheet sheet, string? id) =>
        id is null ? null : sheet.Objects.FirstOrDefault(placed => placed.Id == id);
}
