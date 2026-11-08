using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfLayoutMapper
{
    private readonly OutlineAxis _rows = new("table-row-group", "table-row", "number-rows-repeated", ["table-header-rows", "table-rows"]);
    private readonly OutlineAxis _columns = new("table-column-group", "table-column", "number-columns-repeated", ["table-header-columns", "table-columns"]);

    private readonly List<XElement> _declarations = [];
    private readonly Dictionary<string, string> _columnNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _rowNames = new(StringComparer.Ordinal);

    public IReadOnlyList<XElement> Declarations => _declarations;

    public string ColumnStyle(Twip width) => ColumnStyle(width, false);

    public string ColumnStyle(Twip? width, bool pageBreak)
    {
        var key = (width is { } w ? Measure(w) : "auto") + (pageBreak ? "|break" : string.Empty);

        if (_columnNames.TryGetValue(key, out var existing)) return existing;

        var name = $"co{_columnNames.Count + 1}";
        _columnNames[key] = name;

        var properties = new XElement(Namespaces.Style + "table-column-properties");
        if (width is { } size) properties.Add(new XAttribute(Namespaces.Style + "column-width", Measure(size)));
        if (pageBreak) properties.Add(new XAttribute(Namespaces.Fo + "break-before", "page"));
        _declarations.Add(new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "table-column"),
            properties));

        return name;
    }

    public string RowStyle(Twip height) => RowStyle(height, false, false);

    public string RowStyle(Twip? height, bool pageBreak, bool optimal)
    {
        var key = (height is { } h ? Measure(h) : "auto") + (pageBreak ? "|break" : string.Empty) + (optimal ? "|optimal" : string.Empty);

        if (_rowNames.TryGetValue(key, out var existing)) return existing;

        var name = $"ro{_rowNames.Count + 1}";
        _rowNames[key] = name;

        var properties = new XElement(Namespaces.Style + "table-row-properties");
        if (height is { } size)
            properties.Add(new XAttribute(Namespaces.Style + "row-height", Measure(size)),
                new XAttribute(Namespaces.Style + "use-optimal-row-height", optimal ? "true" : "false"));
        if (pageBreak) properties.Add(new XAttribute(Namespaces.Fo + "break-before", "page"));
        _declarations.Add(new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "table-row"),
            properties));

        return name;
    }

    public IReadOnlySet<string> ReadBreaks(XDocument content, string family, string propertiesName) =>
        content.Descendants(Namespaces.Style + "style")
            .Where(style => style.Attribute(Namespaces.Style + "family")?.Value == family
                && style.Element(Namespaces.Style + propertiesName)?.Attribute(Namespaces.Fo + "break-before")?.Value == "page")
            .Select(style => style.Attribute(Namespaces.Style + "name")?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    public Twip? ReadMeasure(string? value) => new OdfLength().Parse(value);

    public IReadOnlySet<string> ReadOptimalRows(XDocument content) =>
        content.Descendants(Namespaces.Style + "style")
            .Where(style => style.Attribute(Namespaces.Style + "family")?.Value == "table-row"
                && style.Element(Namespaces.Style + "table-row-properties")?.Attribute(Namespaces.Style + "use-optimal-row-height")?.Value == "true")
            .Select(style => style.Attribute(Namespaces.Style + "name")?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Twip> ReadColumnWidths(XDocument content) =>
        ReadSizes(content, "table-column", "table-column-properties", "column-width");

    public IReadOnlyDictionary<string, Twip> ReadRowHeights(XDocument content) =>
        ReadSizes(content, "table-row", "table-row-properties", "row-height");

    public IReadOnlyList<OutlineGroup> ReadRowGroups(XElement table) => _rows.Read(table);

    public IReadOnlyList<OutlineGroup> ReadColumnGroups(XElement table) => _columns.Read(table);

    private IReadOnlyDictionary<string, Twip> ReadSizes(XDocument content, string family, string propertiesName, string attribute)
    {
        var sizes = new Dictionary<string, Twip>(StringComparer.Ordinal);

        foreach (var style in content.Descendants(Namespaces.Style + "style"))
        {
            if (style.Attribute(Namespaces.Style + "family")?.Value != family) continue;
            if (style.Attribute(Namespaces.Style + "name")?.Value is not { } name) continue;
            if (ReadMeasure(style.Element(Namespaces.Style + propertiesName)?.Attribute(Namespaces.Style + attribute)?.Value) is { } size)
                sizes[name] = size;
        }
        return sizes;
    }

    private string Measure(Twip value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value.ToCentimeters():0.###}cm");
}

internal sealed class OutlineAxis(string group, string item, string repeat, IReadOnlyCollection<string> transparent)
{
    public IReadOnlyList<OutlineGroup> Read(XElement container)
    {
        ArgumentNullException.ThrowIfNull(container);

        var groups = new List<OutlineGroup>();
        var index = 0;
        var pending = new Stack<(IEnumerator<XElement> Children, XElement Owner, int Level, int Start)>();
        pending.Push((container.Elements().GetEnumerator(), container, 0, 0));
        while (pending.TryPeek(out var frame))
        {
            if (!frame.Children.MoveNext())
            {
                pending.Pop();
                frame.Children.Dispose();
                if (frame.Owner != container && frame.Owner.Name == Namespaces.Table + group)
                    groups.Add(new OutlineGroup
                    {
                        First = frame.Start,
                        Last = Math.Max(index - 1, frame.Start),
                        Level = frame.Level,
                        IsCollapsed = frame.Owner.Attribute(Namespaces.Table + "display")?.Value == "false",
                    });
                continue;
            }

            var child = frame.Children.Current;
            if (child.Name == Namespaces.Table + group)
                pending.Push((child.Elements().GetEnumerator(), child, frame.Level + 1, index));
            else if (child.Name.Namespace == Namespaces.Table && transparent.Contains(child.Name.LocalName))
                pending.Push((child.Elements().GetEnumerator(), child, frame.Level, index));
            else if (child.Name == Namespaces.Table + item)
                index = (int)Math.Min((long)index + (int.TryParse(child.Attribute(Namespaces.Table + repeat)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? Math.Max(value, 1)
                    : 1), int.MaxValue);
        }
        return groups;
    }
}
