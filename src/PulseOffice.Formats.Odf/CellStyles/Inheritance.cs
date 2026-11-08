using System.Xml.Linq;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfCellStyleInheritance(OdfPropertySetPlacement placement)
{
    public OdfCellStyleInheritance() : this(new OdfPropertySetPlacement())
    {
    }

    private const int MaximumDepth = 32;
    private const string RootStyle = "Default";

    private static readonly XName[] PropertySets =
    [
        Namespaces.Style + "table-cell-properties",
        Namespaces.Style + "paragraph-properties",
        Namespaces.Style + "text-properties",
    ];

    private static readonly XName ParentName = Namespaces.Style + "parent-style-name";
    private static readonly XName DataStyleName = Namespaces.Style + "data-style-name";

    public IReadOnlyDictionary<string, XElement> CommonStyles(XDocument? styles) =>
        styles?.Root?.Element(Namespaces.Office + "styles")?.Elements(Namespaces.Style + "style")
            .Where(style => style.Attribute(Namespaces.Style + "family")?.Value == "table-cell")
            .Select(style => (Name: style.Attribute(Namespaces.Style + "name")?.Value, Style: style))
            .Where(pair => pair.Name is { Length: > 0 })
            .GroupBy(pair => pair.Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Style, StringComparer.Ordinal)
        ?? new Dictionary<string, XElement>(StringComparer.Ordinal);

    public XElement Effective(XElement style, IReadOnlyDictionary<string, XElement> common)
    {
        var chain = new List<XElement>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (style.Attribute(Namespaces.Style + "name")?.Value is { } own) visited.Add(own);
        for (var parent = style.Attribute(ParentName)?.Value; parent is not null && chain.Count < MaximumDepth;)
        {
            if (string.Equals(parent, RootStyle, StringComparison.Ordinal) || !visited.Add(parent) || !common.TryGetValue(parent, out var found)) break;
            chain.Add(found);
            parent = found.Attribute(ParentName)?.Value;
        }
        if (chain.Count == 0) return style;

        var merged = new XElement(style);
        foreach (var ancestor in chain)
        {
            if (merged.Attribute(DataStyleName) is null && ancestor.Attribute(DataStyleName) is { } data)
                merged.SetAttributeValue(DataStyleName, data.Value);
            foreach (var set in PropertySets)
            {
                if (ancestor.Element(set) is not { } inherited) continue;
                var target = merged.Element(set) ?? placement.Insert(merged, set, PropertySets);
                foreach (var attribute in inherited.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
                    if (target.Attribute(attribute.Name) is null && !Overridden(target, attribute.Name))
                        target.SetAttributeValue(attribute.Name, attribute.Value);
            }
        }
        return merged;
    }

    private bool Overridden(XElement target, XName name) =>
        name == Namespaces.Fo + "border" && target.Attributes().Any(attribute => attribute.Name.Namespace == Namespaces.Fo && attribute.Name.LocalName.StartsWith("border-", StringComparison.Ordinal))
        || name.Namespace == Namespaces.Fo && name.LocalName.StartsWith("border-", StringComparison.Ordinal) && target.Attribute(Namespaces.Fo + "border") is not null;
}

internal sealed class OdfCellStyleOverrides(OdfCellStyleBuilder builder, OdfPropertySetPlacement placement)
{
    private static readonly XName[] PropertySets =
    [
        Namespaces.Style + "table-cell-properties",
        Namespaces.Style + "paragraph-properties",
        Namespaces.Style + "text-properties",
    ];

    private readonly Dictionary<XName, string> _neutral = new()
    {
        [Namespaces.Fo + "background-color"] = "transparent",
        [Namespaces.Fo + "wrap-option"] = "no-wrap",
        [Namespaces.Style + "vertical-align"] = "bottom",
        [Namespaces.Style + "text-align-source"] = "value-type",
        [Namespaces.Style + "repeat-content"] = "false",
        [Namespaces.Style + "rotation-angle"] = "0",
        [Namespaces.Style + "cell-protect"] = "protected",
        [Namespaces.Fo + "border-left"] = "none",
        [Namespaces.Fo + "border-top"] = "none",
        [Namespaces.Fo + "border-right"] = "none",
        [Namespaces.Fo + "border-bottom"] = "none",
        [Namespaces.Style + "diagonal-bl-tr"] = "none",
        [Namespaces.Style + "diagonal-tl-br"] = "none",
        [Namespaces.Fo + "margin-left"] = "0pt",
        [Namespaces.Fo + "font-weight"] = "normal",
        [Namespaces.Fo + "font-style"] = "normal",
        [Namespaces.Style + "text-underline-style"] = "none",
        [Namespaces.Style + "text-line-through-style"] = "none",
        [Namespaces.Style + "text-underline-type"] = "none",
        [Namespaces.Style + "text-overline-style"] = "none",
        [Namespaces.Fo + "text-shadow"] = "none",
        [Namespaces.Style + "text-outline"] = "false",
        [Namespaces.Style + "text-position"] = "0% 100%",
        [Namespaces.Fo + "color"] = CellFont.Default.ColorHex,
        [Namespaces.Style + "font-name"] = CellFont.Default.Family,
        [Namespaces.Fo + "font-size"] = CellFont.Default.Size.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "pt",
    };

    public void Reset(XElement declaration, CellStyle parent)
    {
        var inherited = builder.Build("parent", parent);
        foreach (var set in PropertySets)
        {
            if (inherited.Element(set) is not { } source) continue;
            var target = declaration.Element(set);
            foreach (var attribute in source.Attributes())
            {
                if (target?.Attribute(attribute.Name) is not null || !_neutral.TryGetValue(attribute.Name, out var neutral)) continue;
                target ??= placement.Insert(declaration, set, PropertySets);
                target.SetAttributeValue(attribute.Name, neutral);
            }
        }
    }
}

internal sealed class OdfPropertySetPlacement
{
    public XElement Insert(XElement style, XName set, IReadOnlyList<XName> order)
    {
        var created = new XElement(set);
        var rank = IndexOf(order, set);
        var following = style.Elements().FirstOrDefault(element => IndexOf(order, element.Name) > rank);
        if (following is null) style.Add(created);
        else following.AddBeforeSelf(created);
        return created;
    }

    private int IndexOf(IReadOnlyList<XName> order, XName name)
    {
        for (var index = 0; index < order.Count; index++)
            if (order[index] == name) return index;
        return int.MaxValue;
    }
}
