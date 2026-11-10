using System.Xml.Linq;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfNamedStyleLayer(OdfPropertySetPlacement placement)
{
    private sealed record Slot(CellStyleProperties Property, XName Set, XName Attribute, string? Neutral, IReadOnlyList<XName> Readers);

    private static readonly XName CellSet = Namespaces.Style + "table-cell-properties";
    private static readonly XName ParagraphSet = Namespaces.Style + "paragraph-properties";
    private static readonly XName TextSet = Namespaces.Style + "text-properties";
    private static readonly XName DataStyleName = Namespaces.Style + "data-style-name";
    private static readonly XName[] PropertySets = [CellSet, ParagraphSet, TextSet];

    private readonly IReadOnlyList<Slot> _slots =
    [
        Text(CellStyleProperties.FontFamily, Namespaces.Style + "font-name", CellFont.Default.Family, Namespaces.Fo + "font-family"),
        Text(CellStyleProperties.FontSize, Namespaces.Fo + "font-size", CellFont.Default.Size.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "pt"),
        Text(CellStyleProperties.Bold, Namespaces.Fo + "font-weight", "normal"),
        Text(CellStyleProperties.Italic, Namespaces.Fo + "font-style", "normal"),
        Text(CellStyleProperties.Underline, Namespaces.Style + "text-underline-style", "none"),
        Text(CellStyleProperties.DoubleUnderline, Namespaces.Style + "text-underline-type", "single"),
        Text(CellStyleProperties.Overline, Namespaces.Style + "text-overline-style", "none"),
        Text(CellStyleProperties.Strikethrough, Namespaces.Style + "text-line-through-style", "none"),
        Text(CellStyleProperties.Shadow, Namespaces.Fo + "text-shadow", "none"),
        Text(CellStyleProperties.Outline, Namespaces.Style + "text-outline", "false"),
        Text(CellStyleProperties.Script, Namespaces.Style + "text-position", "0% 100%"),
        Text(CellStyleProperties.FontColor, Namespaces.Fo + "color", CellFont.Default.ColorHex),
        Text(CellStyleProperties.Language, Namespaces.Fo + "language", LanguageProperty.NoLanguage, Namespaces.Fo + "country"),
        Cell(CellStyleProperties.Background, Namespaces.Fo + "background-color", "transparent"),
        Cell(CellStyleProperties.BorderLeft, Namespaces.Fo + "border-left", "none", Namespaces.Fo + "border"),
        Cell(CellStyleProperties.BorderTop, Namespaces.Fo + "border-top", "none", Namespaces.Fo + "border"),
        Cell(CellStyleProperties.BorderRight, Namespaces.Fo + "border-right", "none", Namespaces.Fo + "border"),
        Cell(CellStyleProperties.BorderBottom, Namespaces.Fo + "border-bottom", "none", Namespaces.Fo + "border"),
        Cell(CellStyleProperties.DiagonalUp, Namespaces.Style + "diagonal-bl-tr", "none"),
        Cell(CellStyleProperties.DiagonalDown, Namespaces.Style + "diagonal-tl-br", "none"),
        Cell(CellStyleProperties.Horizontal, Namespaces.Style + "text-align-source", "value-type", Namespaces.Style + "repeat-content"),
        new(CellStyleProperties.Horizontal, ParagraphSet, Namespaces.Fo + "text-align", null, []),
        Cell(CellStyleProperties.Vertical, Namespaces.Style + "vertical-align", "bottom"),
        Cell(CellStyleProperties.Wrap, Namespaces.Fo + "wrap-option", "no-wrap"),
        Cell(CellStyleProperties.Rotation, Namespaces.Style + "rotation-angle", "0"),
        Cell(CellStyleProperties.Locked, Namespaces.Style + "cell-protect", "protected"),
        new(CellStyleProperties.Indent, ParagraphSet, Namespaces.Fo + "margin-left", "0pt", []),
    ];

    public CellStyleProperties Declared(XElement style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var result = style.Attribute(DataStyleName) is not null ? CellStyleProperties.NumberFormat : CellStyleProperties.None;
        foreach (var slot in _slots)
        {
            if (style.Element(slot.Set) is not { } set) continue;
            if (set.Attribute(slot.Attribute) is not null || slot.Readers.Any(reader => set.Attribute(reader) is not null))
                result |= slot.Property;
        }
        return result;
    }

    public void Limit(XElement style, CellStyleProperties declared, Func<string> generalDataStyle)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(generalDataStyle);

        if ((declared & CellStyleProperties.NumberFormat) == 0) style.Attribute(DataStyleName)?.Remove();
        else if (style.Attribute(DataStyleName) is null) style.SetAttributeValue(DataStyleName, generalDataStyle());

        foreach (var slot in _slots)
        {
            var set = style.Element(slot.Set);
            if ((declared & slot.Property) == 0)
            {
                set?.Attribute(slot.Attribute)?.Remove();
                foreach (var reader in slot.Readers.Where(reader => reader.LocalName != "border")) set?.Attribute(reader)?.Remove();
                continue;
            }
            if (slot.Neutral is null || set?.Attribute(slot.Attribute) is not null) continue;
            if (slot.Readers.Any(reader => set?.Attribute(reader) is not null)) continue;
            set ??= placement.Insert(style, slot.Set, PropertySets);
            set.SetAttributeValue(slot.Attribute, slot.Neutral);
        }

        foreach (var set in style.Elements().Where(element => PropertySets.Contains(element.Name)).ToList())
            if (!set.HasAttributes && !set.HasElements) set.Remove();
    }

    private static Slot Text(CellStyleProperties property, XName attribute, string neutral, params XName[] readers) =>
        new(property, TextSet, attribute, neutral, readers);

    private static Slot Cell(CellStyleProperties property, XName attribute, string neutral, params XName[] readers) =>
        new(property, CellSet, attribute, neutral, readers);
}
