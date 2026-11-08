using PulseOffice.Formats;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal abstract class OdfCellProperty
{
    public abstract bool Write(CellStyle style, XElement properties);

    public abstract CellStyle Read(XElement properties, CellStyle style);
}

internal sealed class BackgroundProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.BackgroundHex is not { } background) return false;
        properties.Add(new XAttribute(Namespaces.Fo + "background-color", background));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        style with { BackgroundHex = properties.Attribute(Namespaces.Fo + "background-color")?.Value is { } color && color.StartsWith('#') ? color : null };
}

internal sealed class WrapProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (!style.WrapText) return false;
        properties.Add(new XAttribute(Namespaces.Fo + "wrap-option", "wrap"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        style with { WrapText = properties.Attribute(Namespaces.Fo + "wrap-option")?.Value == "wrap" };
}

internal sealed class VerticalAlignmentProperty : OdfCellProperty
{
    private readonly TokenMap<CellVerticalAlignment> _tokens = new(CellVerticalAlignment.Bottom, "bottom",
        (CellVerticalAlignment.Top, "top"), (CellVerticalAlignment.Middle, "middle"));

    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Vertical == CellVerticalAlignment.Bottom) return false;
        properties.Add(new XAttribute(Namespaces.Style + "vertical-align", _tokens.Token(style.Vertical)));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        style with { Vertical = _tokens.Value(properties.Attribute(Namespaces.Style + "vertical-align")?.Value) };
}

internal sealed class AlignSourceProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Horizontal == CellHorizontalAlignment.General) return false;
        properties.Add(new XAttribute(Namespaces.Style + "text-align-source", "fix"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) => style;
}

internal sealed class RepeatContentProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Horizontal != CellHorizontalAlignment.Fill) return false;
        properties.Add(new XAttribute(Namespaces.Style + "repeat-content", "true"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) => style;

    public bool Repeats(XElement? properties) => properties?.Attribute(Namespaces.Style + "repeat-content")?.Value == "true";
}

internal sealed class RotationProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.RotationDegrees == 0) return false;
        var degrees = ((style.RotationDegrees % 360) + 360) % 360;
        properties.Add(new XAttribute(Namespaces.Style + "rotation-angle", degrees.ToString(CultureInfo.InvariantCulture)));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style)
    {
        var text = properties.Attribute(Namespaces.Style + "rotation-angle")?.Value?.Replace("deg", "");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) || !double.IsFinite(degrees)) return style;
        var normalised = (int)Math.Round(((degrees % 360) + 360) % 360);
        return style with { RotationDegrees = normalised > 180 ? normalised - 360 : normalised };
    }
}

internal sealed class CellProtectProperty : OdfCellProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Locked) return false;
        properties.Add(new XAttribute(Namespaces.Style + "cell-protect", "none"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        properties.Attribute(Namespaces.Style + "cell-protect")?.Value is { } protect
            ? style with { Locked = protect.Contains("protected", StringComparison.Ordinal) }
            : style;
}

internal sealed class BordersProperty(IBorderPresets presets) : OdfCellProperty
{
    private const double WidestBorder = 72;

    private readonly TokenMap<BorderStyle> _styles = new(BorderStyle.Thin, "solid",
        (BorderStyle.Dashed, "dashed"), (BorderStyle.Dotted, "dotted"), (BorderStyle.Double, "double"));

    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Borders.IsEmpty) return false;

        Edge(properties, "border-left", style.Borders.Left);
        Edge(properties, "border-top", style.Borders.Top);
        Edge(properties, "border-right", style.Borders.Right);
        Edge(properties, "border-bottom", style.Borders.Bottom);

        if (style.Borders.DiagonalUp.IsVisible)
            properties.Add(new XAttribute(Namespaces.Style + "diagonal-bl-tr", Describe(style.Borders.DiagonalUp)));

        if (style.Borders.DiagonalDown.IsVisible)
            properties.Add(new XAttribute(Namespaces.Style + "diagonal-tl-br", Describe(style.Borders.DiagonalDown)));

        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        style with
        {
            Borders = new CellBorders(
                Edge(properties, "border-left"), Edge(properties, "border-top"),
                Edge(properties, "border-right"), Edge(properties, "border-bottom"))
            {
                DiagonalUp = Parse(properties.Attribute(Namespaces.Style + "diagonal-bl-tr")?.Value),
                DiagonalDown = Parse(properties.Attribute(Namespaces.Style + "diagonal-tl-br")?.Value),
            },
        };

    private void Edge(XElement properties, string attribute, BorderEdge edge) =>
        properties.Add(new XAttribute(Namespaces.Fo + attribute, edge.IsVisible ? Describe(edge) : "none"));

    private string Describe(BorderEdge edge) =>
        $"{edge.Width.ToPoints().ToString("0.##", CultureInfo.InvariantCulture)}pt {_styles.Token(edge.Style)} {edge.ColorHex}";

    private BorderEdge Edge(XElement properties, string attribute) =>
        Parse(properties.Attribute(Namespaces.Fo + attribute)?.Value ?? properties.Attribute(Namespaces.Fo + "border")?.Value);

    private BorderEdge Parse(string? value)
    {
        if (value is null or "none") return BorderEdge.None;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return BorderEdge.None;

        var width = Twip.FromPoints(double.TryParse(parts[0].TrimEnd('p', 't'), NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && double.IsFinite(points)
            ? Math.Clamp(points, 0, WidestBorder) : 1);
        var style = _styles.Value(parts[1]);
        return new BorderEdge(style == BorderStyle.Thin ? Solid(width) : style, parts[2], width);
    }

    private BorderStyle Solid(Twip width) =>
        width.Value >= presets.Pen(BorderStyle.Thick, string.Empty).Width.Value ? BorderStyle.Thick
        : width.Value >= presets.Pen(BorderStyle.Medium, string.Empty).Width.Value ? BorderStyle.Medium
        : BorderStyle.Thin;
}

internal abstract class OdfTextProperty
{
    public abstract bool Write(CellStyle style, XElement properties);

    public abstract CellStyle Read(XElement properties, CellStyle style);

    protected CellStyle WithFont(CellStyle style, CellFont font) => style with { Font = font };
}

internal sealed class FontToggleProperty(
    XName attribute, string written, Func<CellFont, bool> get, Func<CellFont, bool, CellFont> set, Func<string?, bool> read)
    : OdfTextProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (!get(style.Font)) return false;
        properties.Add(new XAttribute(attribute, written));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        WithFont(style, set(style.Font, read(properties.Attribute(attribute)?.Value)));
}

internal sealed class LanguageProperty : OdfTextProperty
{
    public const string NoLanguage = "zxx";

    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Language is not { Length: > 0 } language) return false;

        var parts = language.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        properties.Add(new XAttribute(Namespaces.Fo + "language", parts[0].ToLowerInvariant()));
        properties.Add(new XAttribute(Namespaces.Fo + "country", parts.Length > 1 ? parts[1].ToUpperInvariant() : "none"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style)
    {
        if (properties.Attribute(Namespaces.Fo + "language")?.Value is not { Length: > 0 } language) return style;

        if (string.Equals(language, NoLanguage, StringComparison.OrdinalIgnoreCase)) return style with { Language = null };
        var country = properties.Attribute(Namespaces.Fo + "country")?.Value;
        return style with { Language = country is { Length: > 0 } and not "none" ? $"{language}-{country}" : language };
    }
}

internal sealed class ScriptProperty : OdfTextProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Font.Script == CellScript.Normal) return false;
        properties.Add(new XAttribute(Namespaces.Style + "text-position",
            style.Font.Script == CellScript.Superscript ? "super 58%" : "sub 58%"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style)
    {
        var position = properties.Attribute(Namespaces.Style + "text-position")?.Value;
        var script = position is null ? CellScript.Normal
            : position.StartsWith("super", StringComparison.OrdinalIgnoreCase) ? CellScript.Superscript
            : position.StartsWith("sub", StringComparison.OrdinalIgnoreCase) ? CellScript.Subscript
            : CellScript.Normal;
        return WithFont(style, style.Font with { Script = script });
    }
}

internal sealed class ColourProperty : OdfTextProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Font.ColorHex == CellFont.Default.ColorHex) return false;
        properties.Add(new XAttribute(Namespaces.Fo + "color", style.Font.ColorHex));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        WithFont(style, style.Font with { ColorHex = properties.Attribute(Namespaces.Fo + "color")?.Value ?? CellFont.Default.ColorHex });
}

internal sealed class FamilyProperty : OdfTextProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (style.Font.Family == CellFont.Default.Family) return false;
        properties.Add(new XAttribute(Namespaces.Style + "font-name", style.Font.Family));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style) =>
        WithFont(style, style.Font with { Family = properties.Attribute(Namespaces.Style + "font-name")?.Value ?? CellFont.Default.Family });
}

internal sealed class SizeProperty : OdfTextProperty
{
    public override bool Write(CellStyle style, XElement properties)
    {
        if (Math.Abs(style.Font.Size - CellFont.Default.Size) <= 0.01) return false;
        properties.Add(new XAttribute(Namespaces.Fo + "font-size", style.Font.Size.ToString("0.####", CultureInfo.InvariantCulture) + "pt"));
        return true;
    }

    public override CellStyle Read(XElement properties, CellStyle style)
    {
        var size = properties.Attribute(Namespaces.Fo + "font-size")?.Value?.Trim();
        return WithFont(style, style.Font with { Size = Points(size) is > 0 and var points && double.IsFinite(points) ? Math.Round(Math.Min(points, LargestSize), 4) : CellFont.Default.Size });
    }

    private const double LargestSize = 4000;

    private readonly OdfLength _lengths = new();

    private double Points(string? size)
    {
        if (string.IsNullOrEmpty(size)) return CellFont.Default.Size;
        if (size.EndsWith('%'))
            return double.TryParse(size[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                ? CellFont.Default.Size * percent / 100 : CellFont.Default.Size;
        return _lengths.Parse(size)?.ToPoints() ?? CellFont.Default.Size;
    }
}

internal sealed class OdfCellStyleProperties
{
    public OdfIndent Indent { get; } = new();

    public TokenMap<CellHorizontalAlignment> Horizontal { get; } = new(CellHorizontalAlignment.General, "start",
        (CellHorizontalAlignment.Left, "start"), (CellHorizontalAlignment.Center, "center"),
        (CellHorizontalAlignment.Right, "end"), (CellHorizontalAlignment.Justify, "justify"),
        (CellHorizontalAlignment.Left, "left"), (CellHorizontalAlignment.Right, "right"));

    public IReadOnlyList<OdfCellProperty> Cell { get; } =
        [new BackgroundProperty(), new BordersProperty(new BorderPresets()), new WrapProperty(), new VerticalAlignmentProperty(),
         new AlignSourceProperty(), new RepeatContentProperty(), new RotationProperty(), new CellProtectProperty()];

    public IReadOnlyList<OdfTextProperty> Text { get; } =
    [
        new LanguageProperty(),
        new FontToggleProperty(Namespaces.Fo + "font-weight", "bold", f => f.Bold, (f, v) => f with { Bold = v }, OdfFontValues.IsBold),
        new FontToggleProperty(Namespaces.Fo + "font-style", "italic", f => f.Italic, (f, v) => f with { Italic = v }, v => v is "italic" or "oblique"),
        new FontToggleProperty(Namespaces.Style + "text-underline-style", "solid", f => f.Underline, (f, v) => f with { Underline = v }, OdfFontValues.IsLine),
        new FontToggleProperty(Namespaces.Style + "text-line-through-style", "solid", f => f.Strikethrough, (f, v) => f with { Strikethrough = v }, OdfFontValues.IsLine),
        new FontToggleProperty(Namespaces.Style + "text-underline-type", "double", f => f.DoubleUnderline, (f, v) => f with { DoubleUnderline = v }, v => v == "double"),
        new FontToggleProperty(Namespaces.Style + "text-overline-style", "solid", f => f.Overline, (f, v) => f with { Overline = v }, OdfFontValues.IsLine),
        new FontToggleProperty(Namespaces.Fo + "text-shadow", "1pt 1pt", f => f.Shadow, (f, v) => f with { Shadow = v }, v => v is not null and not "none"),
        new FontToggleProperty(Namespaces.Style + "text-outline", "true", f => f.Outline, (f, v) => f with { Outline = v }, v => v == "true"),
        new ScriptProperty(),
        new ColourProperty(),
        new FamilyProperty(),
        new SizeProperty(),
    ];
}

internal static class OdfFontValues
{
    public static bool IsLine(string? value) => value is not null and not "none";

    public static bool IsBold(string? value) =>
        value == "bold" || (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var weight) && weight >= 600);
}
