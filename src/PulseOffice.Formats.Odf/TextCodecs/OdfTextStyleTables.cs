using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed record ParagraphProperties(
    TextAlign? Align = null, double? FontSize = null, bool? Bold = null, bool? Italic = null, uint? Color = null,
    double? LineSpacing = null, double? SpaceBefore = null, double? SpaceAfter = null, double? MarginLeft = null,
    double? Indent = null, double? MarginRight = null, IReadOnlyList<TabStop>? TabStops = null)
{
    public static ParagraphProperties Empty { get; } = new();

    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }

    public ParagraphProperties Over(ParagraphProperties under) => new(
        Align ?? under.Align, FontSize ?? under.FontSize, Bold ?? under.Bold, Italic ?? under.Italic, Color ?? under.Color,
        LineSpacing ?? under.LineSpacing, SpaceBefore ?? under.SpaceBefore, SpaceAfter ?? under.SpaceAfter,
        MarginLeft ?? under.MarginLeft, Indent ?? under.Indent, MarginRight ?? under.MarginRight, TabStops ?? under.TabStops)
    {
        Direction = Direction ?? under.Direction,
    };

    public ParagraphFormat Resolve() => new(
        Align ?? TextAlign.Left, FontSize ?? 18, Bold ?? false, Italic ?? false, Color, LineSpacing ?? 1.0,
        SpaceBefore ?? 0, SpaceAfter ?? 0, MarginLeft, Indent, MarginRight, TabStops)
    {
        Direction = Direction,
    };
}

internal sealed record ParagraphFormat(
    TextAlign Align, double FontSize, bool Bold, bool Italic, uint? Color, double LineSpacing,
    double SpaceBefore, double SpaceAfter, double? MarginLeft, double? Indent, double? MarginRight, IReadOnlyList<TabStop>? TabStops)
{
    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }
}

internal sealed class OdfParagraphLevels(IReadOnlyList<ParagraphProperties> levels, XElement? inheritedList = null)
{
    public static OdfParagraphLevels None { get; } = new([]);

    public XElement? InheritedList => inheritedList;

    public ParagraphProperties At(int depth) =>
        levels.Count == 0 ? ParagraphProperties.Empty : levels[Math.Clamp(depth, 0, levels.Count - 1)];
}

internal sealed record SpanProperties(
    double? FontSize, string? Font, bool Bold, bool Italic,
    bool Underline, bool Strikethrough, TextBaseline Baseline, uint? Color,
    bool DoubleUnderline, bool Overline, bool Shadow, bool Outline, bool SmallCaps, uint? Highlight = null,
    string? AsianFont = null, string? Language = null, string? AsianLanguage = null);

internal sealed record OdfTextStyles(
    IReadOnlyDictionary<string, ParagraphProperties> Paragraphs,
    IReadOnlyDictionary<string, SpanProperties> Spans,
    IReadOnlyDictionary<string, XElement> Lists,
    IReadOnlyDictionary<string, string> DataStyles);

internal sealed class OdfFontSizeBounds
{
    private const double Smallest = 1;

    private const double Largest = 4000;

    public double? Bounded(double points) => double.IsFinite(points) && points > 0 ? Math.Clamp(points, Smallest, Largest) : null;
}

internal sealed class OdfParagraphPropertiesReader(OdfValues values, OdfTextFrameReader frames) : OdfXml
{
    private readonly OdfFontSizeBounds _sizes = new();

    private readonly OdfTabStops _tabs = new();

    private readonly PulseOffice.Text.Rich.Bidi.AlignMirroring _aligns = new();

    public ParagraphProperties Read(XElement style)
    {
        var paragraph = style.Element(Style + "paragraph-properties");
        var text = style.Element(Style + "text-properties");
        var (margin, indent) = frames.Indents(paragraph);
        var direction = Attribute(paragraph, Style + "writing-mode") switch
        {
            "rl-tb" or "rl" => PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft,
            "lr-tb" or "lr" => PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight,
            _ => (PulseOffice.Text.Rich.Bidi.TextDirection?)null,
        };
        var mirrored = direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var right = Attribute(paragraph, Fo + "margin-right") is { } end ? values.Length(end) : (double?)null;

        return new ParagraphProperties(
            Attribute(paragraph, Fo + "text-align") is { } align ? Aligned(align, mirrored) : null,
            Attribute(text, Fo + "font-size") is { } size && !size.EndsWith('%') ? _sizes.Bounded(values.Points(size)) : null,
            Attribute(text, Fo + "font-weight") is { } weight ? weight == "bold" : null,
            Attribute(text, Fo + "font-style") is { } posture ? posture == "italic" : null,
            values.Color(Attribute(text, Fo + "color")),
            Attribute(paragraph, Fo + "line-height") is { } height ? values.LineHeight(height) : null,
            Attribute(paragraph, Fo + "margin-top") is { } top ? values.Length(top) : null,
            Attribute(paragraph, Fo + "margin-bottom") is { } bottom ? values.Length(bottom) : null,
            mirrored ? right : margin,
            indent,
            mirrored ? margin : right,
            _tabs.Read(paragraph, length => length is null ? null : values.Length(length)))
        {
            Direction = direction,
        };
    }

    private TextAlign Aligned(string align, bool mirrored) =>
        mirrored && align is "left" or "right" ? _aligns.Mirror(values.Align(align)) : values.Align(align);

    private string? Attribute(XElement? element, XName name) => (string?)element?.Attribute(name);
}

internal sealed class ParagraphStyleTable(OdfParagraphPropertiesReader reader) : AutomaticStyleTable<ParagraphProperties>("paragraph")
{
    protected override ParagraphProperties? Describe(XElement style, XElement automatic) => reader.Read(style);
}

internal sealed class TextStyleTable(OdfValues values, OdfLanguageCodec languages, OdfFontFaceNames faces) : AutomaticStyleTable<SpanProperties>("text")
{
    private readonly OdfFontSizeBounds _sizes = new();

    protected override SpanProperties? Describe(XElement style, XElement automatic)
    {
        var text = style.Element(Style + "text-properties");
        var position = (string?)text?.Attribute(Style + "text-position") ?? string.Empty;

        return new SpanProperties(
            (string?)text?.Attribute(Fo + "font-size") is { } size && !size.EndsWith('%') ? _sizes.Bounded(values.Points(size)) : null,
            (string?)text?.Attribute(Style + "font-name"),
            (string?)text?.Attribute(Fo + "font-weight") == "bold",
            (string?)text?.Attribute(Fo + "font-style") == "italic",
            (string?)text?.Attribute(Style + "text-underline-style") is { } underline && underline != "none",
            (string?)text?.Attribute(Style + "text-line-through-style") is { } through && through != "none",
            position.StartsWith("super", StringComparison.Ordinal) ? TextBaseline.Superscript
                : position.StartsWith("sub", StringComparison.Ordinal) ? TextBaseline.Subscript : TextBaseline.Normal,
            values.Color((string?)text?.Attribute(Fo + "color")),
            (string?)text?.Attribute(Style + "text-underline-type") == "double",
            (string?)text?.Attribute(Style + "text-overline-style") is { } overline && overline != "none",
            (string?)text?.Attribute(Fo + "text-shadow") is { } shadow && shadow != "none",
            (string?)text?.Attribute(Style + "text-outline") == "true",
            (string?)text?.Attribute(Fo + "font-variant") == "small-caps",
            (string?)text?.Attribute(Fo + "background-color") is { } background && background != "transparent" ? values.Color(background) : null,
            faces.Unquoted((string?)text?.Attribute(Style + "font-family-asian")) ?? faces.Family(text, (string?)text?.Attribute(Style + "font-name-asian")),
            languages.Read(text, OdfScript.Western),
            languages.Read(text, OdfScript.Asian));
    }
}

internal sealed class ListStyleTable : OdfXml
{
    public IReadOnlyDictionary<string, XElement> Read(params XElement?[] roots)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var root in roots.OfType<XElement>())
            foreach (var style in root.Descendants(Text + "list-style"))
                if ((string?)style.Attribute(Style + "name") is { } name)
                    result.TryAdd(name, style);
        return result;
    }
}

