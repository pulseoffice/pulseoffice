using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed record GraphicReadContext(
    XElement Graphic, XElement Style, IReadOnlyDictionary<string, XElement> Definitions,
    OdfShapeVocabulary Vocabulary, OdfMeasure Measure)
{
    public string? Get(XName attribute) => (string?)Graphic.Attribute(attribute);

    public XElement? Defined(XName attribute) =>
        Get(attribute) is { } name && Definitions.TryGetValue(name, out var found) ? found : null;
}

internal abstract class GraphicPropertyReader
{
    public abstract ShapeStyle Read(GraphicReadContext context, ShapeStyle style);
}

internal sealed class FillPropertyReader : GraphicPropertyReader
{
    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        var kind = context.Vocabulary.Fills.Value(context.Get(Namespaces.Draw + "fill"));
        var fill = ShapeFill.Default with
        {
            Kind = kind,
            ColorHex = context.Get(Namespaces.Draw + "fill-color") ?? ShapeFill.Default.ColorHex,
            Opacity = context.Measure.ParseFraction(context.Get(Namespaces.Draw + "opacity"), 1),
        };

        if (kind is ShapeFillKind.Gradient && context.Defined(Namespaces.Draw + "fill-gradient-name") is { } gradient)
            fill = fill with { Gradient = Gradient(context, gradient) };

        if (kind is ShapeFillKind.Hatch && context.Defined(Namespaces.Draw + "fill-hatch-name") is { } hatch)
            fill = fill with
            {
                Hatch = Hatch(context, hatch) with
                {
                    BackgroundHex = context.Get(Namespaces.Draw + "fill-hatch-solid") == "true" ? context.Get(Namespaces.Draw + "fill-color") : null,
                },
            };

        if (kind is ShapeFillKind.Bitmap)
            fill = Bitmap(context) is { } bitmap ? fill with { Bitmap = bitmap } : fill with { Kind = ShapeFillKind.Solid };

        return style with { Fill = fill };
    }

    private double? Ratio(string? value) =>
        value is { Length: > 1 } text && text.EndsWith('%')
        && double.TryParse(text[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent) && percent > 0
            ? percent / 100 : null;

    private ShapeBitmap? Bitmap(GraphicReadContext context)
    {
        if (context.Defined(Namespaces.Draw + "fill-image-name")?.Element(Namespaces.Office + "binary-data")?.Value is not { Length: > 0 } encoded) return null;
        byte[] data;
        try
        {
            data = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
        var repeat = context.Get(Namespaces.Style + "repeat") ?? "repeat";
        return new ShapeBitmap
        {
            Data = data,
            MediaType = _header.MediaType(data) ?? "image/png",
            Tiled = repeat == "repeat",
            TileWidth = Ratio(context.Get(Namespaces.Draw + "fill-image-width")) is null ? context.Measure.ParseLength(context.Get(Namespaces.Draw + "fill-image-width"), Foundation.Units.Twip.Zero) : Foundation.Units.Twip.Zero,
            TileHeight = Ratio(context.Get(Namespaces.Draw + "fill-image-height")) is null ? context.Measure.ParseLength(context.Get(Namespaces.Draw + "fill-image-height"), Foundation.Units.Twip.Zero) : Foundation.Units.Twip.Zero,
            TileWidthRatio = Ratio(context.Get(Namespaces.Draw + "fill-image-width")) ?? 0,
            TileHeightRatio = Ratio(context.Get(Namespaces.Draw + "fill-image-height")) ?? 0,
        };
    }

    private ShapeGradient Gradient(GraphicReadContext context, XElement element)
    {
        var measure = context.Measure;
        string? Attr(string name) => (string?)element.Attribute(Namespaces.Draw + name);

        return new ShapeGradient
        {
            Kind = context.Vocabulary.Gradients.Value(Attr("style")),
            AngleDegrees = context.Vocabulary.GradientAngles.FromOdf(context.Vocabulary.GradientAngles.Parse(Attr("angle"))),
            Stops = element.Elements(Namespaces.Loext + "gradient-stop").Select(stop => new GradientStop(
                    (string?)stop.Attribute(Namespaces.Loext + "color-value") ?? "#000000",
                    Math.Clamp(measure.ParseNumber((string?)stop.Attribute(Namespaces.Svg + "offset"), 0), 0, 1))).ToList() is { Count: > 2 } stops
                ? stops
                : [new(Attr("start-color") ?? "#FFFFFF", 0), new(Attr("end-color") ?? "#000000", 1)],
            Border = measure.ParseFraction(Attr("border"), 0),
            CentreX = measure.ParseFraction(Attr("cx"), 0.5),
            CentreY = measure.ParseFraction(Attr("cy"), 0.5),
        };
    }

    private ShapeHatch Hatch(GraphicReadContext context, XElement element)
    {
        var measure = context.Measure;
        string? Attr(string name) => (string?)element.Attribute(Namespaces.Draw + name);

        return new ShapeHatch
        {
            Kind = context.Vocabulary.Hatches.Value(Attr("style")),
            ColorHex = Attr("color") ?? ShapeHatch.Default.ColorHex,
            Distance = measure.ParseLength(Attr("distance"), ShapeHatch.Default.Distance),
            AngleDegrees = measure.ParseNumber(Attr("rotation"), 450) % 3600 / 10,
        };
    }
}

internal sealed class LinePropertyReader(ArrowMarkers markers) : GraphicPropertyReader
{
    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        var measure = context.Measure;

        return style with
        {
            Line = ShapeLine.Default with
            {
                Dash = context.Get(Namespaces.Draw + "stroke") switch
                {
                    "none" => ShapeStrokeStyle.None,
                    "dash" => Dash(context),
                    _ => ShapeStrokeStyle.Solid,
                },
                ColorHex = context.Get(Namespaces.Svg + "stroke-color") ?? ShapeLine.Default.ColorHex,
                Width = measure.ParseLength(context.Get(Namespaces.Svg + "stroke-width"), ShapeLine.Default.Width),
                Opacity = measure.ParseFraction(context.Get(Namespaces.Svg + "stroke-opacity"), 1),
                Join = context.Vocabulary.Joins.Value(context.Get(Namespaces.Svg + "stroke-linejoin")),
                Cap = context.Vocabulary.Caps.Value(context.Get(Namespaces.Svg + "stroke-linecap")),
                StartArrow = markers.Read(context.Get(Namespaces.Draw + "marker-start")),
                EndArrow = markers.Read(context.Get(Namespaces.Draw + "marker-end")),
                ArrowScale = ArrowScale(context),
                CustomDash = CustomDash(context),
            },
        };
    }

    private const double SmallestArrowScale = 0.1;

    private const double LargestArrowScale = 10;

    private readonly OdfDashPatterns _dashes = new();

    private ShapeDashPattern? CustomDash(GraphicReadContext context)
    {
        if (context.Get(Namespaces.Draw + "stroke") != "dash" || context.Defined(Namespaces.Draw + "stroke-dash") is not { } dash) return null;
        var pattern = _dashes.Read(dash, context.Measure);
        return pattern == _dashes.Preset(_dashes.Nearest(pattern)) ? null : pattern;
    }

    private double ArrowScale(GraphicReadContext context)
    {
        var measure = context.Measure;
        var width = measure.ParseLength(context.Get(Namespaces.Svg + "stroke-width"), ShapeLine.Default.Width).Value;
        var marker = context.Get(Namespaces.Draw + "marker-end-width") ?? context.Get(Namespaces.Draw + "marker-start-width");
        if (marker is null || width <= 0) return 1;
        return Math.Clamp(Math.Round(measure.ParseLength(marker, new Foundation.Units.Twip(width * 4)).Value / (width * 4.0), 2), SmallestArrowScale, LargestArrowScale);
    }

    private ShapeStrokeStyle Dash(GraphicReadContext context) =>
        context.Defined(Namespaces.Draw + "stroke-dash") is { } dash ? _dashes.Nearest(_dashes.Read(dash, context.Measure)) : ShapeStrokeStyle.Dashed;
}

internal sealed class ShadowPropertyReader : GraphicPropertyReader
{
    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        if (context.Get(Namespaces.Draw + "shadow") != "visible") return style;

        var measure = context.Measure;
        var fallback = ShapeShadow.Default;
        return style with
        {
            Shadow = fallback with
            {
                Enabled = true,
                OffsetX = measure.ParseLength(context.Get(Namespaces.Draw + "shadow-offset-x"), fallback.OffsetX),
                OffsetY = measure.ParseLength(context.Get(Namespaces.Draw + "shadow-offset-y"), fallback.OffsetY),
                ColorHex = context.Get(Namespaces.Draw + "shadow-color") ?? fallback.ColorHex,
                Opacity = measure.ParseFraction(context.Get(Namespaces.Draw + "shadow-opacity"), fallback.Opacity),
                Blur = measure.ParseLength(context.Get(Namespaces.Loext + "shadow-blur"), fallback.Blur),
            },
        };
    }
}

internal sealed class GlowPropertyReader : GraphicPropertyReader
{
    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        if (context.Get(Namespaces.Loext + "glow-radius") is not { } radius) return style;

        return style with
        {
            Glow = ShapeGlow.Default with
            {
                Enabled = true,
                Radius = context.Measure.ParseLength(radius, ShapeGlow.Default.Radius),
                ColorHex = context.Get(Namespaces.Loext + "glow-color") ?? ShapeGlow.Default.ColorHex,
                Opacity = 1 - context.Measure.ParseFraction(context.Get(Namespaces.Loext + "glow-transparency"), 0.4),
            },
        };
    }
}

internal sealed class SoftEdgePropertyReader : GraphicPropertyReader
{
    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style) =>
        style with { SoftEdge = context.Measure.ParseLength(context.Get(Namespaces.Loext + "softedge-radius"), Foundation.Units.Twip.Zero) };
}

internal sealed class BevelPropertyReader : GraphicPropertyReader
{
    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        if (context.Get(Namespaces.Draw + "extrusion") != "true") return style;

        var depth = context.Get(Namespaces.Draw + "extrusion-depth")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return style with
        {
            Bevel = ShapeBevel.Default with
            {
                Style = context.Get(context.Vocabulary.PulseShape + "bevel-style") is { } bevel ? context.Vocabulary.Bevels.Value(bevel) : BevelStyle.Round,
                Depth = context.Measure.ParseLength(depth, ShapeBevel.Default.Depth),
            },
        };
    }
}

internal sealed class TextPropertyReader : GraphicPropertyReader
{
    private const double SmallestSize = 1;

    private const double LargestSize = 4000;

    public override ShapeStyle Read(GraphicReadContext context, ShapeStyle style)
    {
        var text = context.Style.Element(Namespaces.Style + "text-properties");
        var paragraph = context.Style.Element(Namespaces.Style + "paragraph-properties");
        var measure = context.Measure;
        var fallback = ShapeTextStyle.Default;

        return style with
        {
            Text = fallback with
            {
                ColorHex = (string?)text?.Attribute(Namespaces.Fo + "color") ?? fallback.ColorHex,
                AutomaticColor = text?.Attribute(Namespaces.Fo + "color") is null || (string?)text.Attribute(Namespaces.Style + "use-window-font-color") == "true",
                Family = (string?)text?.Attribute(Namespaces.Fo + "font-family") ?? fallback.Family,
                Size = measure.ParseNumber(((string?)text?.Attribute(Namespaces.Fo + "font-size"))?.Replace("pt", string.Empty), fallback.Size) is > 0 and var size
                    ? Math.Min(Math.Max(size, SmallestSize), LargestSize)
                    : fallback.Size,
                Bold = (string?)text?.Attribute(Namespaces.Fo + "font-weight") == "bold",
                Italic = (string?)text?.Attribute(Namespaces.Fo + "font-style") == "italic",
                Underline = text?.Attribute(Namespaces.Style + "text-underline-style") is not null,
                Wrap = context.Get(Namespaces.Fo + "wrap-option") != "no-wrap",
                Inset = measure.ParseLength(context.Get(Namespaces.Fo + "padding"), fallback.Inset),
                Horizontal = context.Vocabulary.ParagraphAlignment.Value((string?)paragraph?.Attribute(Namespaces.Fo + "text-align")),
                Vertical = context.Vocabulary.VerticalAlignment.Value(context.Get(Namespaces.Draw + "textarea-vertical-align")),
                RotationDegrees = measure.ParseAngle(context.Get(Namespaces.Style + "text-rotation-angle"), 0),
                Language = Language(text),
            },
        };
    }

    private static string? Language(XElement? text)
    {
        if ((string?)text?.Attribute(Namespaces.Fo + "language") is not { Length: > 0 } language || language is "zxx" or "none") return null;
        var parts = new[] { language, (string?)text!.Attribute(Namespaces.Fo + "script"), (string?)text.Attribute(Namespaces.Fo + "country") };
        return string.Join('-', parts.Where(part => part is { Length: > 0 } and not "none"));
    }
}

public sealed class OdfShapeStyleReader
{
    private readonly OdfShapeVocabulary _vocabulary = new();
    private readonly OdfMeasure _measure = new();
    private readonly IReadOnlyList<GraphicPropertyReader> _readers;
    private readonly FillPropertyReader _fill = new();

    public OdfShapeStyleReader()
    {
        var markers = new ArrowMarkers();
        _readers =
        [
            _fill, new LinePropertyReader(markers), new ShadowPropertyReader(),
            new GlowPropertyReader(), new BevelPropertyReader(), new SoftEdgePropertyReader(), new TextPropertyReader(),
        ];
    }

    private readonly OdfGraphicStyleInheritance _inheritance = new();

    public ShapeFill ReadFill(XElement properties, IReadOnlyDictionary<string, XElement> definitions) =>
        _fill.Read(new GraphicReadContext(properties, properties, definitions, _vocabulary, _measure), ShapeStyle.Default).Fill;

    public ShapeStyle Read(XElement? style, IReadOnlyDictionary<string, XElement> definitions)
    {
        if (style is not null) style = _inheritance.Effective(style, definitions);
        if (style?.Element(Namespaces.Style + "graphic-properties") is not { } graphic) return ShapeStyle.Default;

        var context = new GraphicReadContext(graphic, style, definitions, _vocabulary, _measure);
        return _readers.Aggregate(ShapeStyle.Default, (current, reader) => reader.Read(context, current));
    }
}
