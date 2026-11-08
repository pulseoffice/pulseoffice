using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdsViewSettingsMapper
{
    private readonly XNamespace _config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public IReadOnlyList<XElement> Build(Workbook workbook)
    {
        var frozen = workbook.Sheets.Where(sheet => sheet.Frozen.IsFrozen).ToList();
        if (frozen.Count == 0) return [];

        return
        [
            new XElement(_config + "config-item-set", new XAttribute(_config + "name", "ooo:view-settings"),
                new XElement(_config + "config-item-map-indexed", new XAttribute(_config + "name", "Views"),
                    new XElement(_config + "config-item-map-entry",
                        Item("ViewId", "string", "view1"),
                        new XElement(_config + "config-item-map-named", new XAttribute(_config + "name", "Tables"),
                            frozen.Select(Table))))),
        ];
    }

    public void Read(XDocument? settings, Workbook workbook)
    {
        var tables = settings?.Descendants(_config + "config-item-map-named")
            .FirstOrDefault(map => (string?)map.Attribute(_config + "name") == "Tables");
        if (tables is null) return;

        foreach (var entry in tables.Elements(_config + "config-item-map-entry"))
        {
            if (entry.Attribute(_config + "name")?.Value is not { } name || workbook.FindSheet(name) is not { } sheet) continue;
            var columns = Value(entry, "HorizontalSplitMode") == 2 ? Value(entry, "HorizontalSplitPosition") : 0;
            var rows = Value(entry, "VerticalSplitMode") == 2 ? Value(entry, "VerticalSplitPosition") : 0;
            sheet.Frozen = new FrozenPanes(
                Math.Clamp(rows, 0, Sheets.Addressing.CellAddress.MaxRows - 1),
                Math.Clamp(columns, 0, Sheets.Addressing.CellAddress.MaxColumns - 1));
        }
    }

    private XElement Table(Worksheet sheet)
    {
        var (rows, columns) = (sheet.Frozen.Rows, sheet.Frozen.Columns);
        return new XElement(_config + "config-item-map-entry", new XAttribute(_config + "name", sheet.Name),
            Item("HorizontalSplitMode", "short", columns > 0 ? "2" : "0"),
            Item("VerticalSplitMode", "short", rows > 0 ? "2" : "0"),
            Item("HorizontalSplitPosition", "int", Text(columns)),
            Item("VerticalSplitPosition", "int", Text(rows)),
            Item("ActiveSplitRange", "short", rows > 0 ? "2" : "3"),
            Item("PositionLeft", "int", "0"),
            Item("PositionRight", "int", Text(columns)),
            Item("PositionTop", "int", "0"),
            Item("PositionBottom", "int", Text(rows)));
    }

    private XElement Item(string name, string type, string value) =>
        new(_config + "config-item", new XAttribute(_config + "name", name), new XAttribute(_config + "type", type), value);

    private int Value(XElement entry, string name) =>
        int.TryParse(entry.Elements(_config + "config-item").FirstOrDefault(item => (string?)item.Attribute(_config + "name") == name)?.Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
