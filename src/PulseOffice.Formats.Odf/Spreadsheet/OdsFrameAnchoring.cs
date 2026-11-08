using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsFrameAnchoring(ISheetPlacement placement)
{
    private static readonly XName EndCell = Namespaces.Table + "end-cell-address";
    private static readonly XName EndX = Namespaces.Table + "end-x";
    private static readonly XName EndY = Namespaces.Table + "end-y";
    private static readonly XName[] Horizontal = [Namespaces.Svg + "x", Namespaces.Svg + "x1", Namespaces.Svg + "x2"];
    private static readonly XName[] Vertical = [Namespaces.Svg + "y", Namespaces.Svg + "y1", Namespaces.Svg + "y2"];
    private static readonly XName Transform = Namespaces.Draw + "transform";

    private readonly OdfDrawUnits _units = new();

    public OdsFrameAnchoring() : this(new SheetPlacement()) { }

    public XElement? Arrange(Worksheet sheet, Dictionary<CellAddress, List<XElement>> frames)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(frames);

        var byName = new Dictionary<string, PlacedObject>(StringComparer.Ordinal);
        foreach (var placed in sheet.Objects) byName.TryAdd(placed.Id, placed);

        var page = new List<XElement>();
        foreach (var (address, list) in frames.ToList())
        {
            for (var index = list.Count - 1; index >= 0; index--)
            {
                var frame = list[index];
                if ((string?)frame.Attribute(Namespaces.Draw + "name") is not { } name || !byName.TryGetValue(name, out var placed)) continue;

                switch (placed.Anchoring)
                {
                    case ObjectAnchoring.MoveAndSize:
                        if (frame.Attribute(EndCell) is null)
                            frame.Add(new OdfCellEnd(sheet.Name, placement.End(sheet.Layout, placed), _units.Length).Attributes());
                        break;
                    case ObjectAnchoring.Fixed:
                        Strip(frame);
                        Shift(frame, sheet.Layout.ColumnOffset(address.Column), sheet.Layout.RowOffset(address.Row));
                        list.RemoveAt(index);
                        page.Insert(0, frame);
                        break;
                    default:
                        Strip(frame);
                        break;
                }
            }

            if (list.Count == 0) frames.Remove(address);
        }

        return page.Count == 0 ? null : new XElement(Namespaces.Table + "shapes", page);
    }

    private void Strip(XElement frame)
    {
        frame.Attribute(EndCell)?.Remove();
        frame.Attribute(EndX)?.Remove();
        frame.Attribute(EndY)?.Remove();
        foreach (var child in frame.Descendants())
        {
            child.Attribute(EndCell)?.Remove();
            child.Attribute(EndX)?.Remove();
            child.Attribute(EndY)?.Remove();
        }
    }

    private void Shift(XElement frame, Twip dx, Twip dy)
    {
        if (dx == Twip.Zero && dy == Twip.Zero) return;

        foreach (var element in frame.DescendantsAndSelf())
        {
            foreach (var name in Horizontal) Move(element, name, dx);
            foreach (var name in Vertical) Move(element, name, dy);
            if ((string?)element.Attribute(Transform) is { } transform) element.SetAttributeValue(Transform, Translated(transform, dx, dy));
        }
    }

    private void Move(XElement element, XName name, Twip by)
    {
        if ((string?)element.Attribute(name) is not { } value) return;
        element.SetAttributeValue(name, _units.Length(_units.Parse(value) + by));
    }

    private string Translated(string transform, Twip dx, Twip dy)
    {
        var start = transform.IndexOf("translate", StringComparison.Ordinal);
        var open = start < 0 ? -1 : transform.IndexOf('(', start);
        var close = open < 0 ? -1 : transform.IndexOf(')', open + 1);
        if (close < 0) return transform;

        var parts = transform[(open + 1)..close].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return transform;

        var x = _units.Parse(parts[0]) + dx;
        var y = (parts.Length > 1 ? _units.Parse(parts[1]) : Twip.Zero) + dy;
        return string.Create(CultureInfo.InvariantCulture,
            $"{transform[..(open + 1)]}{_units.Length(x)} {_units.Length(y)}{transform[close..]}");
    }
}
