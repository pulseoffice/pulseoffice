using PulseOffice.Foundation.Colors;
using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTableStyleMapper
{
    public static readonly XNamespace TableOoo = "http://openoffice.org/2009/table";

    private readonly List<XElement> _declarations = [];
    private readonly Dictionary<(bool Hidden, string? Tab, bool Rtl, string Master), string> _names = [];
    private readonly OdfPageStyleMapper _pages = new();

    public IReadOnlyList<XElement> Declarations => _declarations;

    public void Write(Worksheet sheet, XElement table)
    {
        var master = _pages.MasterName(sheet.PageStyleName);
        if (!sheet.IsHidden && !sheet.RightToLeft && sheet.TabColorHex is not { Length: > 0 } && master == "Default") return;
        var key = (sheet.IsHidden, sheet.TabColorHex, sheet.RightToLeft, master);
        if (!_names.TryGetValue(key, out var name))
        {
            name = $"ta{_names.Count + 1}";
            _names[key] = name;
            var properties = new XElement(Namespaces.Style + "table-properties",
                new XAttribute(Namespaces.Table + "display", sheet.IsHidden ? "false" : "true"),
                new XAttribute(Namespaces.Style + "writing-mode", sheet.RightToLeft ? "rl-tb" : "lr-tb"));
            if (sheet.TabColorHex is { Length: > 0 } tab) properties.Add(new XAttribute(Namespaces.Table + "tab-color", tab), new XAttribute(TableOoo + "tab-color", tab));
            _declarations.Add(new XElement(Namespaces.Style + "style",
                new XAttribute(Namespaces.Style + "name", name),
                new XAttribute(Namespaces.Style + "family", "table"),
                new XAttribute(Namespaces.Style + "master-page-name", master),
                properties));
        }
        table.Add(new XAttribute(Namespaces.Table + "style-name", name));
    }

    public void Read(XElement table, XDocument content, Worksheet sheet)
    {
        if (table.Attribute(Namespaces.Table + "style-name")?.Value is not { } name) return;
        var style = content.Descendants(Namespaces.Style + "style").FirstOrDefault(element =>
            element.Attribute(Namespaces.Style + "family")?.Value == "table"
            && element.Attribute(Namespaces.Style + "name")?.Value == name);
        if (_pages.SheetStyle((string?)style?.Attribute(Namespaces.Style + "master-page-name")) is { Length: > 0 } page) sheet.PageStyleName = page;
        if (style?.Element(Namespaces.Style + "table-properties") is not { } properties) return;

        sheet.IsHidden = properties.Attribute(Namespaces.Table + "display")?.Value == "false";
        if ((properties.Attribute(Namespaces.Table + "tab-color") ?? properties.Attribute(TableOoo + "tab-color"))?.Value is { Length: > 0 } tab && ArgbColor.FromHashedRgbHex(tab) is not null) sheet.TabColorHex = tab.ToUpperInvariant();
        if (properties.Attribute(Namespaces.Style + "writing-mode")?.Value is { } mode) sheet.RightToLeft = mode.StartsWith("rl", StringComparison.Ordinal);
    }
}
