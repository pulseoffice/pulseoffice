namespace PulseOffice.Sheets.Styling;

[Flags]
public enum CellStyleProperties : long
{
    None = 0,
    FontFamily = 1L << 0,
    FontSize = 1L << 1,
    Bold = 1L << 2,
    Italic = 1L << 3,
    Underline = 1L << 4,
    DoubleUnderline = 1L << 5,
    Overline = 1L << 6,
    Strikethrough = 1L << 7,
    Shadow = 1L << 8,
    Outline = 1L << 9,
    Script = 1L << 10,
    FontColor = 1L << 11,
    Background = 1L << 12,
    BorderLeft = 1L << 13,
    BorderTop = 1L << 14,
    BorderRight = 1L << 15,
    BorderBottom = 1L << 16,
    DiagonalUp = 1L << 17,
    DiagonalDown = 1L << 18,
    Horizontal = 1L << 19,
    Vertical = 1L << 20,
    Wrap = 1L << 21,
    Rotation = 1L << 22,
    Indent = 1L << 23,
    NumberFormat = 1L << 24,
    Locked = 1L << 25,
    Language = 1L << 26,

    Font = FontFamily | FontSize | Bold | Italic | Underline | DoubleUnderline | Overline | Strikethrough | Shadow | Outline | Script | FontColor,
    Borders = BorderLeft | BorderTop | BorderRight | BorderBottom | DiagonalUp | DiagonalDown,
    Alignment = Horizontal | Vertical | Wrap | Rotation | Indent,
    All = Font | Background | Borders | Alignment | NumberFormat | Locked | Language,
}

public interface ICellStyleLayering
{
    IReadOnlyList<CellStyleProperties> Properties { get; }

    CellStyleProperties Differences(CellStyle style, CellStyle basis);

    CellStyle Overlay(CellStyle basis, CellStyle layer, CellStyleProperties properties);

    bool IsFlag(CellStyleProperties property);

    bool Flag(CellStyle style, CellStyleProperties property);

    CellStyle WithFlag(CellStyle style, CellStyleProperties property, bool value);
}

public sealed class CellStyleLayering : ICellStyleLayering
{
    private sealed record Layer(CellStyleProperties Property, Func<CellStyle, CellStyle, bool> Same, Func<CellStyle, CellStyle, CellStyle> Copy)
    {
        public Func<CellStyle, bool>? Get { get; init; }

        public Func<CellStyle, bool, CellStyle>? Set { get; init; }
    }

    private readonly IReadOnlyList<Layer> _layers =
    [
        Font(CellStyleProperties.FontFamily, (a, b) => a.Family == b.Family, (to, from) => to with { Family = from.Family }),
        Font(CellStyleProperties.FontSize, (a, b) => a.Size.Equals(b.Size), (to, from) => to with { Size = from.Size }),
        Flag(CellStyleProperties.Bold, style => style.Font.Bold, (style, value) => style with { Font = style.Font with { Bold = value } }),
        Flag(CellStyleProperties.Italic, style => style.Font.Italic, (style, value) => style with { Font = style.Font with { Italic = value } }),
        Flag(CellStyleProperties.Underline, style => style.Font.Underline, (style, value) => style with { Font = style.Font with { Underline = value } }),
        Flag(CellStyleProperties.DoubleUnderline, style => style.Font.DoubleUnderline, (style, value) => style with { Font = style.Font with { DoubleUnderline = value } }),
        Flag(CellStyleProperties.Overline, style => style.Font.Overline, (style, value) => style with { Font = style.Font with { Overline = value } }),
        Flag(CellStyleProperties.Strikethrough, style => style.Font.Strikethrough, (style, value) => style with { Font = style.Font with { Strikethrough = value } }),
        Flag(CellStyleProperties.Shadow, style => style.Font.Shadow, (style, value) => style with { Font = style.Font with { Shadow = value } }),
        Flag(CellStyleProperties.Outline, style => style.Font.Outline, (style, value) => style with { Font = style.Font with { Outline = value } }),
        Font(CellStyleProperties.Script, (a, b) => a.Script == b.Script, (to, from) => to with { Script = from.Script }),
        Font(CellStyleProperties.FontColor, (a, b) => string.Equals(a.ColorHex, b.ColorHex, StringComparison.OrdinalIgnoreCase), (to, from) => to with { ColorHex = from.ColorHex }),
        new(CellStyleProperties.Background, (a, b) => a.BackgroundHex == b.BackgroundHex, (to, from) => to with { BackgroundHex = from.BackgroundHex }),
        Border(CellStyleProperties.BorderLeft, borders => borders.Left, (to, from) => to with { Left = from.Left }),
        Border(CellStyleProperties.BorderTop, borders => borders.Top, (to, from) => to with { Top = from.Top }),
        Border(CellStyleProperties.BorderRight, borders => borders.Right, (to, from) => to with { Right = from.Right }),
        Border(CellStyleProperties.BorderBottom, borders => borders.Bottom, (to, from) => to with { Bottom = from.Bottom }),
        Border(CellStyleProperties.DiagonalUp, borders => borders.DiagonalUp, (to, from) => to with { DiagonalUp = from.DiagonalUp }),
        Border(CellStyleProperties.DiagonalDown, borders => borders.DiagonalDown, (to, from) => to with { DiagonalDown = from.DiagonalDown }),
        new(CellStyleProperties.Horizontal, (a, b) => a.Horizontal == b.Horizontal, (to, from) => to with { Horizontal = from.Horizontal }),
        new(CellStyleProperties.Vertical, (a, b) => a.Vertical == b.Vertical, (to, from) => to with { Vertical = from.Vertical }),
        Flag(CellStyleProperties.Wrap, style => style.WrapText, (style, value) => style with { WrapText = value }),
        new(CellStyleProperties.Rotation, (a, b) => a.RotationDegrees == b.RotationDegrees, (to, from) => to with { RotationDegrees = from.RotationDegrees }),
        new(CellStyleProperties.Indent, (a, b) => a.Indent == b.Indent, (to, from) => to with { Indent = from.Indent }),
        new(CellStyleProperties.NumberFormat, (a, b) => a.NumberFormatId == b.NumberFormatId, (to, from) => to with { NumberFormatId = from.NumberFormatId }),
        Flag(CellStyleProperties.Locked, style => style.Locked, (style, value) => style with { Locked = value }),
        new(CellStyleProperties.Language, (a, b) => a.Language == b.Language, (to, from) => to with { Language = from.Language }),
    ];

    public IReadOnlyList<CellStyleProperties> Properties => [.. _layers.Select(layer => layer.Property)];

    public CellStyleProperties Differences(CellStyle style, CellStyle basis)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(basis);

        var result = CellStyleProperties.None;
        foreach (var layer in _layers)
            if (!layer.Same(style, basis)) result |= layer.Property;
        return result;
    }

    public CellStyle Overlay(CellStyle basis, CellStyle layer, CellStyleProperties properties)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(layer);

        var result = basis;
        foreach (var candidate in _layers)
            if ((properties & candidate.Property) != 0) result = candidate.Copy(result, layer);
        return result;
    }

    public bool IsFlag(CellStyleProperties property) => LayerOf(property).Get is not null;

    public bool Flag(CellStyle style, CellStyleProperties property)
    {
        ArgumentNullException.ThrowIfNull(style);
        return (LayerOf(property).Get ?? throw new ArgumentException("The property is not a flag.", nameof(property)))(style);
    }

    public CellStyle WithFlag(CellStyle style, CellStyleProperties property, bool value)
    {
        ArgumentNullException.ThrowIfNull(style);
        return (LayerOf(property).Set ?? throw new ArgumentException("The property is not a flag.", nameof(property)))(style, value);
    }

    private Layer LayerOf(CellStyleProperties property) =>
        _layers.FirstOrDefault(layer => layer.Property == property) ?? throw new ArgumentOutOfRangeException(nameof(property), property, "Not a single cell style property.");

    private static Layer Flag(CellStyleProperties property, Func<CellStyle, bool> get, Func<CellStyle, bool, CellStyle> set) =>
        new(property, (a, b) => get(a) == get(b), (to, from) => set(to, get(from))) { Get = get, Set = set };

    private static Layer Font(CellStyleProperties property, Func<CellFont, CellFont, bool> same, Func<CellFont, CellFont, CellFont> copy) =>
        new(property, (a, b) => same(a.Font, b.Font), (to, from) => to with { Font = copy(to.Font, from.Font) });

    private static Layer Border(CellStyleProperties property, Func<CellBorders, BorderEdge> edge, Func<CellBorders, CellBorders, CellBorders> copy) =>
        new(property, (a, b) => edge(a.Borders) == edge(b.Borders), (to, from) => to with { Borders = copy(to.Borders, from.Borders) });
}
