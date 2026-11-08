using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed class OdfTextAnchors
{
    private readonly TokenMap<TextAnchor> _map = new(TextAnchor.Top, "top",
        (TextAnchor.Top, "top"),
        (TextAnchor.Middle, "middle"),
        (TextAnchor.Bottom, "bottom"),
        (TextAnchor.Justified, "justify"));

    public string Token(TextAnchor anchor) => _map.Token(anchor);

    public TextAnchor? Find(string? token) => _map.Find(token);
}

internal sealed class OdfTextFrameWriter(OdfUnits units, OdfTextAnchors anchors) : OdfXml
{
    public IEnumerable<XAttribute> Attributes(TextFrame frame, Twip height)
    {
        yield return new XAttribute(Draw + "textarea-vertical-align", anchors.Token(frame.Anchor));
        yield return new XAttribute(Fo + "padding-left", Length(frame.Insets.Left));
        yield return new XAttribute(Fo + "padding-top", Length(frame.Insets.Top));
        yield return new XAttribute(Fo + "padding-right", Length(frame.Insets.Right));
        yield return new XAttribute(Fo + "padding-bottom", Length(frame.Insets.Bottom));
        yield return new XAttribute(Draw + "auto-grow-height", frame.Autofit == TextAutofit.ResizeShape ? "true" : "false");
        yield return new XAttribute(Draw + "auto-grow-width", !frame.WordWrap && frame.Autofit == TextAutofit.ResizeShape ? "true" : "false");
        yield return new XAttribute(Fo + "wrap-option", frame.WordWrap ? "wrap" : "no-wrap");
        if (frame.Autofit == TextAutofit.ResizeShape && height.Value > 0)
            yield return new XAttribute(Fo + "min-height", units.Cm(height));
        if (frame.Autofit == TextAutofit.ShrinkText)
        {
            yield return new XAttribute(Draw + "fit-to-size", "shrink-to-fit");
            yield return new XAttribute(Style + "shrink-to-fit", "true");
        }
    }

    private readonly ParagraphMargins _margins = new();

    public IEnumerable<XAttribute> Indents(TextParagraph paragraph)
    {
        if (_margins.Written(paragraph.MarginLeft, paragraph.Indent) is { } margin) yield return new XAttribute(Fo + "margin-left", Length(margin));
        if (paragraph.Indent is { } indent) yield return new XAttribute(Fo + "text-indent", Length(indent));
    }

    private string Length(double points) => units.Cm(new Twip((int)Math.Round(points * 20)));
}

internal sealed class OdfTextFrameReader(OdfValues values, OdfTextAnchors anchors) : OdfXml
{

    public TextFrameLayer Read(XElement? style)
    {
        var properties = style?.Element(Style + "graphic-properties");
        if (properties is null) return TextFrameLayer.Empty;

        var fit = (string?)properties.Attribute(Draw + "fit-to-size");
        var shrink = (string?)properties.Attribute(Style + "shrink-to-fit");
        var grow = (string?)properties.Attribute(Draw + "auto-grow-height");
        TextAutofit? autofit = fit == "shrink-to-fit" || shrink == "true" ? TextAutofit.ShrinkText
            : grow == "true" ? TextAutofit.ResizeShape
            : grow == "false" || fit == "false" || shrink == "false" ? TextAutofit.None
            : null;

        return new TextFrameLayer
        {
            Anchor = anchors.Find((string?)properties.Attribute(Draw + "textarea-vertical-align")),
            Left = Length(properties, "padding-left") ?? Length(properties, "padding"),
            Top = Length(properties, "padding-top") ?? Length(properties, "padding"),
            Right = Length(properties, "padding-right") ?? Length(properties, "padding"),
            Bottom = Length(properties, "padding-bottom") ?? Length(properties, "padding"),
            Autofit = autofit,
            Wrap = (string?)properties.Attribute(Fo + "wrap-option") switch
            {
                "no-wrap" => false,
                "wrap" => true,
                _ => (string?)properties.Attribute(Draw + "auto-grow-width") == "true" && grow == "true" ? false : null,
            },
        };
    }

    private readonly ParagraphMargins _margins = new();

    public (double? Margin, double? Indent) Indents(XElement? paragraphProperties) =>
        (_margins.Read(Length(paragraphProperties, "margin-left")), Length(paragraphProperties, "text-indent"));

    private double? Length(XElement? properties, string name) =>
        (string?)properties?.Attribute(Fo + name) is { Length: > 0 } value ? values.Length(value) : null;
}

internal sealed class OdfTextFrameStyles(
    IReadOnlyDictionary<string, XElement> automatic,
    IReadOnlyDictionary<string, XElement> common,
    OdfTextFrameReader reader) : OdfXml
{
    private const int DeepestParent = 8;

    public TextFrameLayer Of(XElement frame) =>
        Chain((string?)frame.Attribute(Draw + "style-name"))
            .Over(Chain((string?)frame.Attribute(Presentation + "style-name")));

    public TextFrameLayer Chain(string? name)
    {
        if (name is null) return TextFrameLayer.Empty;

        var scope = automatic;
        var layer = TextFrameLayer.Empty;
        for (var depth = 0; name is not null && depth < DeepestParent; depth++)
        {
            if (!scope.TryGetValue(name, out var style) && !common.TryGetValue(name, out style)) break;
            layer = layer.Over(reader.Read(style));
            name = (string?)style.Attribute(Style + "parent-style-name");
            scope = common;
        }
        return layer;
    }
}

internal sealed class OdfTextFrameStylesBuilder(OdfTextFrameReader reader) : OdfXml
{

    public OdfTextFrameStyles Build(XElement? content, XElement? styles)
    {
        var automatic = content is not null
            ? Table(content.Element(Office + "automatic-styles"))
            : Table(styles?.Element(Office + "automatic-styles"));
        return new OdfTextFrameStyles(automatic, Table(styles?.Element(Office + "styles")), reader);
    }

    private Dictionary<string, XElement> Table(XElement? root)
    {
        var table = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var style in root?.Elements(Style + "style") ?? [])
            if ((string?)style.Attribute(Style + "family") is "graphic" or "presentation"
                && (string?)style.Attribute(Style + "name") is { } name)
                table[name] = style;
        return table;
    }
}
