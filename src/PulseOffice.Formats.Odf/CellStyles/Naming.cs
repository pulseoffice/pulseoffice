using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfStyleNames
{
    public const string NamedStyleMarker = "Qo";

    public string? NamedStyleOf(string automaticStyleName, IEnumerable<string> known)
    {
        ArgumentNullException.ThrowIfNull(automaticStyleName);
        ArgumentNullException.ThrowIfNull(known);

        var prefix = "ce" + NamedStyleMarker;
        if (!automaticStyleName.StartsWith(prefix, StringComparison.Ordinal)) return null;

        var tail = automaticStyleName[prefix.Length..].TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return known.FirstOrDefault(name => string.Equals(Sanitize(name), tail, StringComparison.Ordinal));
    }

    public string Encode(string name)
    {
        var result = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var plain = char.IsAsciiLetter(c) || (i > 0 && (char.IsAsciiDigit(c) || c is '-' or '.'));
            if (plain) result.Append(c);
            else result.Append('_').Append(((int)c).ToString("x", CultureInfo.InvariantCulture)).Append('_');
        }
        return result.ToString();
    }

    public string Decode(string name)
    {
        if (!name.Contains('_')) return name;

        var result = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            int close;
            if (name[i] == '_' && (close = name.IndexOf('_', i + 1)) > i + 1
                && int.TryParse(name.AsSpan(i + 1, close - i - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
                && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF))
            {
                result.Append(char.ConvertFromUtf32(code));
                i = close;
                continue;
            }

            result.Append(name[i]);
        }
        return result.ToString();
    }

    private string Sanitize(string name) => new([.. name.Where(char.IsLetterOrDigit)]);
}

public sealed record OdfNamedCellStyle(string EncodedName, string Name, string? Parent, CellStyle Style)
{
    public CellStyleProperties Declared { get; init; } = CellStyleProperties.All;
}

internal sealed class OdfConditionOnlyStyles
{
    public bool IsConditionOnly(XElement style)
    {
        if (style.Attribute(Namespaces.Style + "display-name") is not null) return false;
        var name = style.Attribute(Namespaces.Style + "name")?.Value ?? string.Empty;
        foreach (var prefix in (string[])["Condition", "ConditionalStyle_", "ConditionalStyle"])
            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length && name[prefix.Length..].All(char.IsAsciiDigit))
                return true;
        return false;
    }
}

public sealed class OdfCellStyleReader
{
    private const string RootStyle = "Default";

    private readonly OdfCellStyleProperties _properties = new();
    private readonly OdfStyleNames _names = new();
    private readonly OdfConditionOnlyStyles _conditions = new();
    private readonly OdfCellStyleInheritance _inheritance = new();
    private readonly OdfNamedStyleLayer _layer = new(new OdfPropertySetPlacement());

    public IReadOnlyDictionary<string, CellStyle> ReadStyles(XDocument content) => ReadStyles(content, _ => 0);

    public IReadOnlyDictionary<string, CellStyle> ReadStyles(XDocument content, Func<string, int> numberFormat) =>
        ReadStyles(content, numberFormat, null);

    public IReadOnlyDictionary<string, CellStyle> ReadStyles(XDocument content, Func<string, int> numberFormat, XDocument? commonStyles)
    {
        ArgumentNullException.ThrowIfNull(content);

        var common = _inheritance.CommonStyles(commonStyles);
        var styles = new Dictionary<string, CellStyle>(StringComparer.Ordinal);
        foreach (var (name, declared) in CellStyles(content))
        {
            var element = _inheritance.Effective(declared, common);
            var style = Read(element);
            if (element.Attribute(Namespaces.Style + "data-style-name")?.Value is { } dataStyle)
                style = style with { NumberFormatId = numberFormat(dataStyle) };
            styles[name] = style;
            styles[_names.Decode(name)] = style;
        }
        return styles;
    }

    public IReadOnlyList<OdfNamedCellStyle> ReadNamed(XDocument styles, Func<string, int> numberFormat)
    {
        ArgumentNullException.ThrowIfNull(styles);

        var common = styles.Root?.Element(Namespaces.Office + "styles")?.Elements(Namespaces.Style + "style")
            .Where(style => style.Attribute(Namespaces.Style + "family")?.Value == "table-cell"
                && style.Attribute(Namespaces.Style + "name")?.Value is { Length: > 0 })
            .Where(style => !_conditions.IsConditionOnly(style))
            .ToList() ?? [];
        var displays = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in common)
        {
            var encoded = element.Attribute(Namespaces.Style + "name")!.Value;
            displays.TryAdd(encoded, element.Attribute(Namespaces.Style + "display-name")?.Value is { Length: > 0 } display ? display : _names.Decode(encoded));
        }

        var result = new List<OdfNamedCellStyle>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in common)
        {
            var encoded = element.Attribute(Namespaces.Style + "name")!.Value;
            var display = displays[encoded];
            if (!seen.Add(display)) continue;
            var style = Read(element);
            if (element.Attribute(Namespaces.Style + "data-style-name")?.Value is { } dataStyle)
                style = style with { NumberFormatId = numberFormat(dataStyle) };
            var parent = element.Attribute(Namespaces.Style + "parent-style-name")?.Value is not { Length: > 0 } parentName ? null
                : string.Equals(parentName, RootStyle, StringComparison.Ordinal) ? (string.Equals(display, RootStyle, StringComparison.OrdinalIgnoreCase) ? null : RootStyle)
                : displays.TryGetValue(parentName, out var parentDisplay) && !string.Equals(parentDisplay, display, StringComparison.OrdinalIgnoreCase) ? parentDisplay
                : null;
            result.Add(new OdfNamedCellStyle(encoded, display, parent, style) { Declared = _layer.Declared(element) });
        }
        return result;
    }

    public IReadOnlyDictionary<string, string> ReadParents(XDocument content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, element) in CellStyles(content))
            if (element.Attribute(Namespaces.Style + "parent-style-name")?.Value is { } parent)
                parents[name] = parent;
        return parents;
    }

    private IEnumerable<(string Name, XElement Element)> CellStyles(XDocument content) =>
        content.Descendants(Namespaces.Style + "style")
            .Where(style => style.Attribute(Namespaces.Style + "family")?.Value == "table-cell")
            .Select(style => (Name: style.Attribute(Namespaces.Style + "name")?.Value, Element: style))
            .Where(pair => pair.Name is not null)
            .Select(pair => (pair.Name!, pair.Element));

    private CellStyle Read(XElement element)
    {
        var style = new CellStyle();

        var cell = element.Element(Namespaces.Style + "table-cell-properties");
        if (cell is not null)
            style = _properties.Cell.Aggregate(style, (current, property) => property.Read(cell, current));

        var paragraph = element.Element(Namespaces.Style + "paragraph-properties");
        var fromValueType = cell?.Attribute(Namespaces.Style + "text-align-source")?.Value == "value-type";
        style = style with
        {
            Horizontal = _properties.Cell.OfType<RepeatContentProperty>().Any(repeat => repeat.Repeats(cell))
                ? CellHorizontalAlignment.Fill
                : !fromValueType && paragraph?.Attribute(Namespaces.Fo + "text-align")?.Value is { } align
                ? _properties.Horizontal.Value(align)
                : CellHorizontalAlignment.General,
            Indent = _properties.Indent.Levels(paragraph?.Attribute(Namespaces.Fo + "margin-left")?.Value),
        };

        if (element.Element(Namespaces.Style + "text-properties") is { } text)
            style = _properties.Text.Aggregate(style, (current, property) => property.Read(text, current));

        return style;
    }
}

internal sealed class OdfCellStyleBuilder(OdfCellStyleProperties properties)
{
    public XElement Build(string name, CellStyle style, string? dataStyleName = null)
    {
        var element = new XElement(Namespaces.Style + "style",
            new XAttribute(Namespaces.Style + "name", name),
            new XAttribute(Namespaces.Style + "family", "table-cell"),
            new XAttribute(Namespaces.Style + "parent-style-name", "Default"));
        if (dataStyleName is not null) element.Add(new XAttribute(Namespaces.Style + "data-style-name", dataStyleName));

        var cell = new XElement(Namespaces.Style + "table-cell-properties");
        if (properties.Cell.Select(property => property.Write(style, cell)).ToList().Any(written => written)) element.Add(cell);

        var paragraph = new XElement(Namespaces.Style + "paragraph-properties");
        if (style.Horizontal != CellHorizontalAlignment.General)
            paragraph.Add(new XAttribute(Namespaces.Fo + "text-align", properties.Horizontal.Token(style.Horizontal)));
        if (style.Indent > 0)
            paragraph.Add(new XAttribute(Namespaces.Fo + "margin-left", properties.Indent.Length(style.Indent)));
        if (paragraph.HasAttributes) element.Add(paragraph);

        var text = new XElement(Namespaces.Style + "text-properties");
        if (properties.Text.Select(property => property.Write(style, text)).ToList().Any(written => written)) element.Add(text);

        return element;
    }
}

internal sealed class OdfIndent
{
    private const double PointsPerLevel = 10;

    private readonly OdfLength _length = new();

    public string Length(int levels) =>
        (levels * PointsPerLevel).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt";

    public int Levels(string? margin) =>
        _length.Parse(margin) is { } length ? (int)Math.Round(Math.Max(0, length.ToPoints()) / PointsPerLevel) : 0;
}
