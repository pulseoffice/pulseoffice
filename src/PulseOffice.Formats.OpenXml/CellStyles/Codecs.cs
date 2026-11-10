using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Styling;
using Color = DocumentFormat.OpenXml.Spreadsheet.Color;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlValueMap<TModel, TSdk>(TModel fallback, TSdk fallbackSdk, params (TModel Model, TSdk Sdk)[] entries)
    where TModel : struct, Enum
    where TSdk : struct
{
    public TSdk Sdk(TModel value) =>
        entries.Where(entry => EqualityComparer<TModel>.Default.Equals(entry.Model, value)).Select(entry => entry.Sdk)
            .DefaultIfEmpty(fallbackSdk).First();

    public TModel Model(TSdk? value) =>
        value is not { } sdk ? fallback
        : entries.Where(entry => EqualityComparer<TSdk>.Default.Equals(entry.Sdk, sdk)).Select(entry => (TModel?)entry.Model)
            .FirstOrDefault() ?? fallback;
}

internal sealed class SpreadsheetColours
{
    public string ToArgb(string hex)
    {
        var value = hex.TrimStart('#').ToUpperInvariant();
        if (!value.All(char.IsAsciiHexDigit)) return "FF000000";
        return value.Length switch
        {
            6 => "FF" + value,
            8 => value,
            _ => "FF000000",
        };
    }

    public string FromArgb(string? argb) => Parse(argb) ?? "#000000";

    public string? Parse(string? argb) => argb is { Length: 8 } && argb.All(char.IsAsciiHexDigit) ? "#" + argb[2..] : null;
}

internal sealed class StyleTable<TItem>(OpenXmlCompositeElement container, Action<uint> count) where TItem : OpenXmlElement
{
    private readonly Dictionary<string, uint> _index = new(StringComparer.Ordinal);
    private List<TItem>? _items;
    private uint? _count;

    public OpenXmlCompositeElement Container => container;

    public uint Intern(string key, Func<TItem> build)
    {
        if (_index.TryGetValue(key, out var existing)) return existing;

        var index = _count ??= (uint)container.ChildElements.Count;
        container.AppendChild(build());
        _count = index + 1;
        count(index + 1);
        _items = null;
        _index[key] = index;
        return index;
    }

    public TItem? At(uint? id)
    {
        var items = _items ??= [.. container.Elements<TItem>()];
        return id is { } value && value < items.Count ? items[(int)value] : null;
    }
}

internal sealed class SpreadsheetVocabulary
{
    public OoxmlValueMap<BorderStyle, BorderStyleValues> Borders { get; } = new(BorderStyle.Thin, BorderStyleValues.None,
        (BorderStyle.Thin, BorderStyleValues.Thin), (BorderStyle.Medium, BorderStyleValues.Medium),
        (BorderStyle.Thick, BorderStyleValues.Thick), (BorderStyle.Dashed, BorderStyleValues.Dashed),
        (BorderStyle.Dotted, BorderStyleValues.Dotted), (BorderStyle.Double, BorderStyleValues.Double));

    public OoxmlValueMap<CellHorizontalAlignment, HorizontalAlignmentValues> Horizontal { get; } =
        new(CellHorizontalAlignment.General, HorizontalAlignmentValues.General,
            (CellHorizontalAlignment.Left, HorizontalAlignmentValues.Left), (CellHorizontalAlignment.Center, HorizontalAlignmentValues.Center),
            (CellHorizontalAlignment.Right, HorizontalAlignmentValues.Right), (CellHorizontalAlignment.Justify, HorizontalAlignmentValues.Justify),
            (CellHorizontalAlignment.Fill, HorizontalAlignmentValues.Fill));

    public OoxmlValueMap<CellVerticalAlignment, VerticalAlignmentValues> Vertical { get; } =
        new(CellVerticalAlignment.Bottom, VerticalAlignmentValues.Bottom,
            (CellVerticalAlignment.Top, VerticalAlignmentValues.Top), (CellVerticalAlignment.Middle, VerticalAlignmentValues.Center));

    public OoxmlValueMap<CellScript, VerticalAlignmentRunValues> Script { get; } =
        new(CellScript.Normal, VerticalAlignmentRunValues.Baseline,
            (CellScript.Superscript, VerticalAlignmentRunValues.Superscript), (CellScript.Subscript, VerticalAlignmentRunValues.Subscript));
}

internal sealed class FontCodec(SpreadsheetColours colours, SpreadsheetVocabulary vocabulary)
{
    public string Key(CellFont font) => string.Create(CultureInfo.InvariantCulture,
        $"{font.Family}|{font.Size}|{font.Bold}|{font.Italic}|{font.Underline}|{font.DoubleUnderline}|{font.Strikethrough}|{font.Shadow}|{font.Outline}|{font.Script}|{font.ColorHex}");

    public Font Build(CellFont font)
    {
        var element = new Font(new FontSize { Val = font.Size }, new FontName { Val = font.Family }, new Color { Rgb = colours.ToArgb(font.ColorHex) });

        if (font.Bold) element.AppendChild(new Bold());
        if (font.Italic) element.AppendChild(new Italic());
        if (font.DoubleUnderline) element.AppendChild(new Underline { Val = UnderlineValues.Double });
        else if (font.Underline) element.AppendChild(new Underline());
        if (font.Strikethrough) element.AppendChild(new Strike());
        if (font.Shadow) element.AppendChild(new Shadow());
        if (font.Outline) element.AppendChild(new Outline());
        if (font.Script != CellScript.Normal) element.AppendChild(new VerticalTextAlignment { Val = vocabulary.Script.Sdk(font.Script) });
        return element;
    }

    public CellFont Read(Font? font) => font is null ? CellFont.Default : new CellFont
    {
        Family = font.FontName?.Val?.Value ?? "Calibri",
        Size = font.FontSize?.Val?.Value ?? 10,
        Bold = font.Bold is not null,
        Italic = font.Italic is not null,
        Underline = font.Underline is not null,
        DoubleUnderline = font.Underline?.Val is not null && font.Underline.Val.Value == UnderlineValues.Double,
        Strikethrough = font.Strike is not null,
        Shadow = font.Shadow is not null,
        Outline = font.Outline is not null,
        Script = vocabulary.Script.Model(font.VerticalTextAlignment?.Val?.Value),
        ColorHex = colours.FromArgb(font.Color?.Rgb?.Value),
    };
}

internal sealed class FillCodec(SpreadsheetColours colours)
{
    public Fill Build(string background) => new(new PatternFill
    {
        PatternType = PatternValues.Solid,
        ForegroundColor = new ForegroundColor { Rgb = colours.ToArgb(background) },
        BackgroundColor = new BackgroundColor { Indexed = 64 },
    });

    public string? Read(Fill? fill) =>
        fill?.PatternFill is { } pattern && pattern.PatternType?.Value == PatternValues.Solid
        && pattern.ForegroundColor?.Rgb?.Value is { } rgb
            ? colours.FromArgb(rgb)
            : null;
}

internal sealed class BorderCodec(SpreadsheetColours colours, SpreadsheetVocabulary vocabulary, IBorderPresets presets)
{
    public string Key(CellBorders borders) =>
        $"{Describe(borders.Left)}|{Describe(borders.Top)}|{Describe(borders.Right)}|{Describe(borders.Bottom)}|{Describe(borders.DiagonalUp)}|{Describe(borders.DiagonalDown)}";

    public Border Build(CellBorders borders)
    {
        var diagonal = borders.DiagonalUp.IsVisible ? borders.DiagonalUp : borders.DiagonalDown;

        var border = new Border(
            Edge<LeftBorder>(borders.Left), Edge<RightBorder>(borders.Right),
            Edge<TopBorder>(borders.Top), Edge<BottomBorder>(borders.Bottom),
            Edge<DiagonalBorder>(diagonal));

        if (borders.DiagonalUp.IsVisible) border.DiagonalUp = true;
        if (borders.DiagonalDown.IsVisible) border.DiagonalDown = true;

        return border;
    }

    public CellBorders Read(Border? border)
    {
        if (border is null) return CellBorders.None;

        var diagonal = Edge(border.DiagonalBorder);

        return new CellBorders(
            Edge(border.LeftBorder), Edge(border.TopBorder), Edge(border.RightBorder), Edge(border.BottomBorder))
        {
            DiagonalUp = border.DiagonalUp?.Value == true ? diagonal : BorderEdge.None,
            DiagonalDown = border.DiagonalDown?.Value == true ? diagonal : BorderEdge.None,
        };
    }

    private T Edge<T>(BorderEdge edge) where T : BorderPropertiesType, new()
    {
        var result = new T();
        if (!edge.IsVisible) return result;

        result.Style = vocabulary.Borders.Sdk(edge.Style);
        result.AppendChild(new Color { Rgb = colours.ToArgb(edge.ColorHex) });
        return result;
    }

    private BorderEdge Edge(BorderPropertiesType? edge)
    {
        var style = edge?.Style?.Value;
        if (style is null || style == BorderStyleValues.None) return BorderEdge.None;
        var model = vocabulary.Borders.Model(style.Value);
        return new BorderEdge(model, colours.FromArgb(edge?.Color?.Rgb?.Value), presets.Pen(model, string.Empty).Width);
    }

    private string Describe(BorderEdge edge) => $"{edge.Style}:{edge.ColorHex}:{edge.Width.Value}";
}

internal sealed class AlignmentCodec(SpreadsheetVocabulary vocabulary)
{
    public Alignment? Build(PulseOffice.Sheets.Styling.CellStyle style) =>
        style.Horizontal != CellHorizontalAlignment.General || style.Vertical != CellVerticalAlignment.Bottom
        || style.WrapText || style.Indent > 0 || style.RotationDegrees != 0
            ? new Alignment
            {
                Horizontal = vocabulary.Horizontal.Sdk(style.Horizontal),
                Vertical = vocabulary.Vertical.Sdk(style.Vertical),
                WrapText = style.WrapText,
                Indent = (uint)style.Indent,
                TextRotation = style.RotationDegrees != 0 ? TextRotationOf(style.RotationDegrees) : null,
            }
            : null;

    public static uint TextRotationOf(int degrees)
    {
        var clamped = Math.Clamp(degrees, -90, 90);
        return clamped >= 0 ? (uint)clamped : (uint)(90 - clamped);
    }

    public static int DegreesOf(uint? rotation) => rotation switch
    {
        null or 255 => 0,
        > 90 => 90 - (int)rotation.Value,
        _ => (int)rotation.Value,
    };
}

public sealed class XlsxStyleReader
{
    private readonly FontCodec _fonts;
    private readonly FillCodec _fills;
    private readonly BorderCodec _borders;
    private readonly SpreadsheetVocabulary _vocabulary = new();

    public XlsxStyleReader()
    {
        var colours = new SpreadsheetColours();
        _fonts = new FontCodec(colours, _vocabulary);
        _fills = new FillCodec(colours);
        _borders = new BorderCodec(colours, _vocabulary, new BorderPresets());
    }

    public IReadOnlyList<PulseOffice.Sheets.Styling.CellStyle> ReadStyles(Stylesheet? stylesheet)
    {
        if (stylesheet?.CellFormats is null) return [PulseOffice.Sheets.Styling.CellStyle.Default];

        var read = Reading(stylesheet);
        var styles = stylesheet.CellFormats.Elements<CellFormat>().Select(read).ToList();

        return styles.Count == 0 ? [PulseOffice.Sheets.Styling.CellStyle.Default] : styles;
    }

    public IReadOnlyList<XlsxNamedStyle> ReadNamedStyles(Stylesheet? stylesheet)
    {
        if (stylesheet?.CellStyles is null || stylesheet.CellStyleFormats is null) return [];

        var read = Reading(stylesheet);
        var formats = stylesheet.CellStyleFormats.Elements<CellFormat>().ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<XlsxNamedStyle>();
        foreach (var style in stylesheet.CellStyles.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>())
        {
            if (ModelName(style) is not { } name || style.FormatId?.Value is not { } id || id >= formats.Count) continue;
            if (seen.Add(name)) result.Add(new XlsxNamedStyle(name, read(formats[(int)id]), _includes.Read(formats[(int)id])));
        }
        return result;
    }

    private readonly XlsxStyleIncludes _includes = new();

    private Func<CellFormat, PulseOffice.Sheets.Styling.CellStyle> Reading(Stylesheet stylesheet)
    {
        var fonts = new StyleTable<Font>(stylesheet.Fonts ?? new Fonts(), _ => { });
        var fills = new StyleTable<Fill>(stylesheet.Fills ?? new Fills(), _ => { });
        var borders = new StyleTable<Border>(stylesheet.Borders ?? new Borders(), _ => { });

        return format => new PulseOffice.Sheets.Styling.CellStyle
        {
            Font = _fonts.Read(fonts.At(format.FontId?.Value)),
            BackgroundHex = _fills.Read(fills.At(format.FillId?.Value)),
            Borders = _borders.Read(borders.At(format.BorderId?.Value)),
            NumberFormatId = (int)(format.NumberFormatId?.Value ?? 0),
            Horizontal = _vocabulary.Horizontal.Model(format.Alignment?.Horizontal?.Value),
            WrapText = format.Alignment?.WrapText?.Value ?? false,
            Indent = (int)(format.Alignment?.Indent?.Value ?? 0),
            Vertical = _vocabulary.Vertical.Model(format.Alignment?.Vertical?.Value),
            RotationDegrees = AlignmentCodec.DegreesOf(format.Alignment?.TextRotation?.Value),
            Locked = format.Protection?.Locked?.Value ?? true,
        };
    }

    public IReadOnlyList<string?> ReadStyleNames(Stylesheet? stylesheet)
    {
        if (stylesheet?.CellFormats is null) return [];

        var names = new Dictionary<uint, string>();
        foreach (var style in stylesheet.CellStyles?.Elements<DocumentFormat.OpenXml.Spreadsheet.CellStyle>() ?? [])
            if (style.FormatId?.Value is { } id && ModelName(style) is { } name)
                names.TryAdd(id, name);

        return [.. stylesheet.CellFormats.Elements<CellFormat>().Select(format =>
            format.FormatId?.Value is { } id && names.TryGetValue(id, out var name) && !string.Equals(name, DefaultStyle, StringComparison.Ordinal)
                ? name
                : null)];
    }

    private const string DefaultStyle = "Default";

    private string? ModelName(DocumentFormat.OpenXml.Spreadsheet.CellStyle style) =>
        style.Name?.Value?.Trim() is not { Length: > 0 } name ? null
        : string.Equals(name, "Normal", StringComparison.Ordinal) && style.BuiltinId?.Value == 0 ? DefaultStyle
        : name;
}

public sealed record XlsxNamedStyle(string Name, PulseOffice.Sheets.Styling.CellStyle Style, CellStyleProperties Explicit);

internal sealed class XlsxStyleIncludes
{
    public CellStyleProperties Read(CellFormat format)
    {
        var result = CellStyleProperties.None;
        if (format.ApplyFont?.Value != false) result |= CellStyleProperties.Font;
        if (format.ApplyFill?.Value != false) result |= CellStyleProperties.Background;
        if (format.ApplyBorder?.Value != false) result |= CellStyleProperties.Borders;
        if (format.ApplyAlignment?.Value != false) result |= CellStyleProperties.Alignment;
        if (format.ApplyNumberFormat?.Value != false) result |= CellStyleProperties.NumberFormat;
        if (format.ApplyProtection?.Value != false) result |= CellStyleProperties.Locked;
        return result | CellStyleProperties.Language;
    }

    public void Write(CellFormat format, CellStyleProperties declared)
    {
        format.ApplyFont = (declared & CellStyleProperties.Font) != 0;
        format.ApplyFill = (declared & CellStyleProperties.Background) != 0;
        format.ApplyBorder = (declared & CellStyleProperties.Borders) != 0;
        format.ApplyAlignment = (declared & CellStyleProperties.Alignment) != 0;
        format.ApplyNumberFormat = (declared & CellStyleProperties.NumberFormat) != 0;
        format.ApplyProtection = (declared & CellStyleProperties.Locked) != 0;
    }
}
