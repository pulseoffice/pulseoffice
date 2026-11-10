using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfStyleMapper(string dataStylePrefix = "N")
{
    public const string NamedStyleMarker = OdfStyleNames.NamedStyleMarker;

    private readonly Dictionary<string, string> _namesByKey = new(StringComparer.Ordinal);
    private readonly List<XElement> _dataDeclarations = [];
    private readonly List<XElement> _cellDeclarations = [];
    private readonly HashSet<string> _fontFamilies = new(StringComparer.Ordinal);
    private readonly OdfCellStyleBuilder _builder = new(new OdfCellStyleProperties());
    private readonly OdfCellStyleHyperlink _links = new();
    private readonly OdfStyleNames _names = new();
    private readonly OdfCellStyleOverrides _overrides = new(new OdfCellStyleBuilder(new OdfCellStyleProperties()), new OdfPropertySetPlacement());

    public IReadOnlyList<XElement> Declarations => [.. Enumerable.Reverse(_dataDeclarations), .. _cellDeclarations];

    public IReadOnlyCollection<string> FontFamilies => _fontFamilies;

    private readonly OdfNumberStyleWriter _numbers = new();
    private readonly Dictionary<string, string> _dataStyles = new(StringComparer.Ordinal);

    public string? DataStyleFor(string? numberFormatCode)
    {
        if (numberFormatCode is null or "General") return null;
        if (_dataStyles.TryGetValue(numberFormatCode, out var existing)) return existing;
        var name = $"{dataStylePrefix}{_dataStyles.Count + 100}";
        _dataStyles[numberFormatCode] = name;
        _dataDeclarations.AddRange(_numbers.Build(name, numberFormatCode).Reverse());
        return name;
    }

    public string? NameFor(int styleId, CellStyle style, string? parent = null, string? numberFormatCode = null, string? hyperlink = null, int inferredFormatId = 0, CellStyle? parentStyle = null)
    {
        if (style.IsDefault && parent is null && hyperlink is null) return null;

        var key = parent is null ? styleId.ToString(CultureInfo.InvariantCulture) : $"{styleId}@{parent}";
        if (inferredFormatId != 0) key += "~" + inferredFormatId.ToString(CultureInfo.InvariantCulture);
        if (hyperlink is not null) key += "#" + hyperlink;
        if (_namesByKey.TryGetValue(key, out var existing)) return existing;

        var name = $"ce{_dataDeclarations.Count + _cellDeclarations.Count + 1}";
        _namesByKey[key] = name;
        var dataStyle = DataStyleFor(numberFormatCode) ?? (parent is not null && parentStyle is { NumberFormatId: not 0 } ? GeneralDataStyle() : null);
        var declaration = _builder.Build(name, style, dataStyle);
        if (parent is not null) declaration.SetAttributeValue(Namespaces.Style + "parent-style-name", _names.Encode(parent));
        if (parent is not null && parentStyle is not null) _overrides.Reset(declaration, parentStyle);
        if (hyperlink is not null) _links.Attach(declaration, hyperlink);
        _cellDeclarations.Add(declaration);

        if (style.Font.Family != CellFont.Default.Family) _fontFamilies.Add(style.Font.Family);
        return name;
    }

    private string GeneralDataStyle()
    {
        if (_generalDataStyle is not null) return _generalDataStyle;
        _generalDataStyle = $"{dataStylePrefix}{_dataStyles.Count + 100}G";
        _dataDeclarations.AddRange(_numbers.Build(_generalDataStyle, "General").Reverse());
        return _generalDataStyle;
    }

    private string? _generalDataStyle;

    public XElement DeclareNamed(string name, CellStyle style, string? parent, string? numberFormatCode = null) =>
        DeclareNamed(name, style, parent, numberFormatCode, null);

    public XElement DeclareNamed(string name, CellStyle style, string? parent, string? numberFormatCode, CellStyleProperties? declared)
    {
        var element = _builder.Build(_names.Encode(name), style, DataStyleFor(numberFormatCode));
        element.SetAttributeValue(Namespaces.Style + "parent-style-name", parent is null ? null : _names.Encode(parent));
        element.SetAttributeValue(Namespaces.Style + "display-name", name);
        if (declared is { } properties) _layer.Limit(element, properties, GeneralDataStyle);
        return element;
    }

    private readonly OdfNamedStyleLayer _layer = new(new OdfPropertySetPlacement());
}

internal sealed class OdfCellStyleHyperlink
{
    private static readonly XName Properties = Namespaces.Style + "table-cell-properties";

    public void Attach(XElement style, string href)
    {
        if (style.Element(Properties) is not { } properties)
        {
            properties = new XElement(Properties);
            style.AddFirst(properties);
        }
        properties.Add(
            new XElement(Namespaces.Style + "hyperlink", new XAttribute(Namespaces.Xlink + "href", href), new XAttribute(Namespaces.Xlink + "type", "simple")),
            new XElement(Namespaces.Loext + "hyperlink", new XAttribute(Namespaces.Xlink + "href", href), new XAttribute(Namespaces.Xlink + "type", "simple")));
    }

    public IReadOnlyDictionary<string, string> Read(XDocument content)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in content.Descendants(Namespaces.Style + "style"))
        {
            if (style.Attribute(Namespaces.Style + "name")?.Value is not { Length: > 0 } name) continue;
            if (style.Element(Properties) is not { } properties) continue;
            var link = (properties.Element(Namespaces.Style + "hyperlink") ?? properties.Element(Namespaces.Loext + "hyperlink"))
                ?.Attribute(Namespaces.Xlink + "href")?.Value;
            if (link is { Length: > 0 }) links[name] = link;
        }
        return links;
    }
}
