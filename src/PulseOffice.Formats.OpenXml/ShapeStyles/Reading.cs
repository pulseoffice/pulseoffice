using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingColourReader
{
    public string? Hex(A.RgbColorModelHex? colour) =>
        colour?.Val?.Value is { Length: 6 } value ? "#" + value.ToUpperInvariant() : null;

    public double Alpha(A.Alpha? alpha) => alpha?.Val?.Value is { } value ? Math.Clamp(value / 100000.0, 0, 1) : 1;
}

internal sealed record DrawingReadContext(
    OpenXmlCompositeElement Properties, OpenXmlCompositeElement? Body, DrawingVocabulary Vocabulary, DrawingColourReader Colours)
{
    public A.EffectList? Effects => Properties.GetFirstChild<A.EffectList>();
}

internal abstract class DrawingPartReader
{
    public abstract ShapeStyle Read(DrawingReadContext context, ShapeStyle style);
}

internal abstract class FillKindReader
{
    public abstract ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context);
}

internal sealed class NoFillReader : FillKindReader
{
    public override ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context) =>
        properties.GetFirstChild<A.NoFill>() is not null ? ShapeFill.None : null;
}

internal sealed class GradientFillReader : FillKindReader
{
    public override ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context)
    {
        if (properties.GetFirstChild<A.GradientFill>() is not { } gradient) return null;

        var colours = context.Colours;
        var stops = gradient.Descendants<A.GradientStop>()
            .Select(stop => new GradientStop(colours.Hex(stop.GetFirstChild<A.RgbColorModelHex>()) ?? "#FFFFFF", (stop.Position?.Value ?? 0) / 100000.0))
            .ToList();
        var linear = gradient.GetFirstChild<A.LinearGradientFill>();
        var path = gradient.GetFirstChild<A.PathGradientFill>();

        return ShapeFill.Default with
        {
            Kind = ShapeFillKind.Gradient,
            Opacity = colours.Alpha(gradient.Descendants<A.Alpha>().FirstOrDefault()),
            Gradient = ShapeGradient.Default with
            {
                Kind = path is null ? GradientKind.Linear : path.Path?.InnerText == "rect" ? GradientKind.Rectangular : GradientKind.Radial,
                AngleDegrees = (linear?.Angle?.Value ?? 5400000) / 60000.0,
                Stops = stops.Count >= 2 ? stops : ShapeGradient.Default.Stops,
                CentreX = (path?.FillToRectangle?.Left?.Value ?? 50000) / 100000.0,
                CentreY = (path?.FillToRectangle?.Top?.Value ?? 50000) / 100000.0,
            },
        };
    }
}

internal sealed class PatternFillReader : FillKindReader
{
    public override ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context)
    {
        if (properties.GetFirstChild<A.PatternFill>() is not { } pattern) return null;

        return ShapeFill.Default with
        {
            Kind = ShapeFillKind.Hatch,
            Hatch = ShapeHatch.Default with
            {
                Kind = context.Vocabulary.Hatches.Value(pattern.Preset?.InnerText),
                ColorHex = context.Colours.Hex(pattern.ForegroundColor?.GetFirstChild<A.RgbColorModelHex>()) ?? ShapeHatch.Default.ColorHex,
                BackgroundHex = pattern.BackgroundColor?.GetFirstChild<A.RgbColorModelHex>() is { } background && context.Colours.Alpha(background.GetFirstChild<A.Alpha>()) > 0
                    ? context.Colours.Hex(background)
                    : null,
            },
        };
    }
}

internal sealed class BitmapFillReader(OoxmlPendingImages pending) : FillKindReader
{
    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    public override ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context)
    {
        if (properties.GetFirstChild<A.BlipFill>() is not { } fill || fill.Blip is not { } blip) return null;
        if ((pending.Data(blip) ?? Embedded(properties, blip.Embed?.Value)) is not { Length: > 0 } data) return null;

        var tile = fill.GetFirstChild<A.Tile>();
        return ShapeFill.Default with
        {
            Kind = ShapeFillKind.Bitmap,
            Opacity = blip.GetFirstChild<A.AlphaModulationFixed>()?.Amount?.Value is { } amount ? Math.Clamp(amount / 100000.0, 0, 1) : 1,
            Bitmap = new ShapeBitmap
            {
                Data = data,
                MediaType = _header.MediaType(data) ?? "image/png",
                Tiled = tile is not null || fill.GetFirstChild<A.Stretch>() is null,
            },
        };
    }

    private byte[]? Embedded(OpenXmlCompositeElement properties, string? id)
    {
        if (id is null || properties.Ancestors<OpenXmlPartRootElement>().FirstOrDefault()?.OpenXmlPart is not { } part) return null;
        if (!part.Parts.Any(pair => pair.RelationshipId == id) || part.GetPartById(id) is not { } image) return null;
        using var stream = image.GetStream();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

internal sealed class SolidFillReader : FillKindReader
{
    public override ShapeFill? Read(OpenXmlCompositeElement properties, DrawingReadContext context)
    {
        var colour = properties.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>();
        return ShapeFill.Default with
        {
            ColorHex = context.Colours.Hex(colour) ?? ShapeFill.Default.ColorHex,
            Opacity = context.Colours.Alpha(colour?.GetFirstChild<A.Alpha>()),
        };
    }
}

internal sealed class FillPartReader : DrawingPartReader
{
    private readonly IReadOnlyList<FillKindReader> _kinds = [new NoFillReader(), new GradientFillReader(), new PatternFillReader(), new BitmapFillReader(new OoxmlPendingImages()), new SolidFillReader()];

    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style) =>
        style with { Fill = _kinds.Select(kind => kind.Read(context.Properties, context)).First(fill => fill is not null)! };
}

internal sealed class LinePartReader : DrawingPartReader
{
    private readonly DrawingStyleFidelity _fidelity = new();

    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style)
    {
        if (context.Properties.GetFirstChild<A.Outline>() is not { } outline) return style with { Line = ShapeLine.Default };
        if (outline.GetFirstChild<A.NoFill>() is not null) return style with { Line = ShapeLine.None };

        var vocabulary = context.Vocabulary;
        var solid = outline.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>();
        return style with
        {
            Line = ShapeLine.Default with
            {
                ColorHex = context.Colours.Hex(solid) ?? ShapeLine.Default.ColorHex,
                Opacity = context.Colours.Alpha(solid?.GetFirstChild<A.Alpha>()),
                Width = outline.Width?.Value is { } width ? new Emu(width).ToTwips() : ShapeLine.Default.Width,
                Dash = Custom(outline) is { } custom ? custom.NearestPreset() : vocabulary.Dashes.Value(outline.GetFirstChild<A.PresetDash>()?.Val?.InnerText),
                CustomDash = Custom(outline),
                Cap = vocabulary.Caps.Value(outline.CapType?.InnerText),
                Join = outline.GetFirstChild<A.Miter>() is not null ? LineJoinStyle.Miter
                    : outline.GetFirstChild<A.LineJoinBevel>() is not null ? LineJoinStyle.Bevel
                    : LineJoinStyle.Round,
                StartArrow = Arrow(vocabulary, outline.GetFirstChild<A.HeadEnd>()?.Type?.InnerText),
                EndArrow = Arrow(vocabulary, outline.GetFirstChild<A.TailEnd>()?.Type?.InnerText),
                ArrowScale = _fidelity.ArrowScale(outline.GetFirstChild<A.TailEnd>()?.Width?.InnerText ?? outline.GetFirstChild<A.HeadEnd>()?.Width?.InnerText),
            },
        };
    }

    private ArrowEnd Arrow(DrawingVocabulary vocabulary, string? type) => type is null ? ArrowEnd.None : vocabulary.Arrows.Value(type);

    private ShapeDashPattern? Custom(A.Outline outline)
    {
        var stops = outline.GetFirstChild<A.CustomDash>()?.Elements<A.DashStop>()
            .Select(stop => (Dash: (stop.DashLength?.Value ?? 100000) / 100000.0, Space: (stop.SpaceLength?.Value ?? 100000) / 100000.0)).ToList();
        if (stops is not { Count: > 0 }) return null;
        var first = stops.TakeWhile(stop => Math.Abs(stop.Dash - stops[0].Dash) < 1e-9).Count();
        var rest = stops.Skip(first).ToList();
        return new ShapeDashPattern
        {
            Dots1 = first,
            Dots1Length = stops[0].Dash,
            Dots2 = rest.Count,
            Dots2Length = rest.Count > 0 ? rest[0].Dash : 0,
            Distance = stops[0].Space,
            Relative = true,
        };
    }
}

internal sealed class ShadowPartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style)
    {
        if (context.Effects?.GetFirstChild<A.OuterShadow>() is not { } shadow) return style;

        var distance = shadow.Distance?.Value ?? 0;
        var direction = (shadow.Direction?.Value ?? 0) / 60000.0 * Math.PI / 180;
        var colour = shadow.GetFirstChild<A.RgbColorModelHex>();

        return style with
        {
            Shadow = ShapeShadow.Default with
            {
                Enabled = true,
                ColorHex = context.Colours.Hex(colour) ?? ShapeShadow.Default.ColorHex,
                Opacity = context.Colours.Alpha(colour?.GetFirstChild<A.Alpha>()),
                Blur = new Emu(shadow.BlurRadius?.Value ?? 0).ToTwips(),
                OffsetX = new Emu((long)Math.Round(distance * Math.Cos(direction))).ToTwips(),
                OffsetY = new Emu((long)Math.Round(distance * Math.Sin(direction))).ToTwips(),
            },
        };
    }
}

internal sealed class GlowPartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style)
    {
        if (context.Effects?.GetFirstChild<A.Glow>() is not { } glow) return style;

        var colour = glow.GetFirstChild<A.RgbColorModelHex>();
        return style with
        {
            Glow = ShapeGlow.Default with
            {
                Enabled = true,
                Radius = new Emu(glow.Radius?.Value ?? 0).ToTwips(),
                ColorHex = context.Colours.Hex(colour) ?? ShapeGlow.Default.ColorHex,
                Opacity = context.Colours.Alpha(colour?.GetFirstChild<A.Alpha>()),
            },
        };
    }
}

internal sealed class SoftEdgePartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style) =>
        style with { SoftEdge = context.Effects?.GetFirstChild<A.SoftEdge>()?.Radius?.Value is { } radius ? new Emu(radius).ToTwips() : Twip.Zero };
}

internal sealed class BevelPartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style) =>
        context.Properties.GetFirstChild<A.Shape3DType>()?.GetFirstChild<A.BevelTop>() is { } bevel
            ? style with
            {
                Bevel = new ShapeBevel
                {
                    Depth = new Emu(bevel.Width?.Value ?? 0).ToTwips(),
                    Style = context.Vocabulary.Bevels.Value(bevel.Preset?.InnerText),
                },
            }
            : style;
}

internal sealed class ExtrusionPartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style) =>
        context.Properties.GetFirstChild<A.Shape3DType>()?.ExtrusionHeight?.Value is { } height and > 0
            ? style with { Extrusion = new ShapeExtrusion { Kind = ExtrusionKind.Extrude, Depth = new Emu(height).ToTwips() } }
            : style;
}

internal sealed class TextPartReader : DrawingPartReader
{
    public override ShapeStyle Read(DrawingReadContext context, ShapeStyle style)
    {
        if (context.Body is not { } body) return style;

        var properties = body.GetFirstChild<A.BodyProperties>();
        var paragraph = body.GetFirstChild<A.Paragraph>();
        var run = paragraph?.GetFirstChild<A.Run>()?.GetFirstChild<A.RunProperties>();
        var fallback = ShapeTextStyle.Default;

        return style with
        {
            Text = fallback with
            {
                Family = run?.GetFirstChild<A.LatinFont>()?.Typeface?.Value ?? fallback.Family,
                Size = (run?.FontSize?.Value ?? 1100) / 100.0,
                Bold = run?.Bold?.Value ?? false,
                Italic = run?.Italic?.Value ?? false,
                Underline = run?.Underline?.InnerText is not null and not "none",
                Language = run?.Language?.Value is { Length: > 0 } language ? language : null,
                ColorHex = context.Colours.Hex(run?.GetFirstChild<A.SolidFill>()?.GetFirstChild<A.RgbColorModelHex>()) ?? fallback.ColorHex,
                AutomaticColor = run?.GetFirstChild<A.SolidFill>() is null,
                Wrap = properties?.Wrap?.InnerText != "none",
                Inset = new Emu(properties?.LeftInset?.Value ?? 0).ToTwips(),
                RotationDegrees = (properties?.Rotation?.Value ?? 0) / 60000.0,
                Vertical = context.Vocabulary.Anchors.Value(properties?.Anchor?.InnerText),
                Horizontal = context.Vocabulary.Alignments.Value(paragraph?.GetFirstChild<A.ParagraphProperties>()?.Alignment?.InnerText),
            },
        };
    }
}

internal sealed class DrawingStyleReader(DrawingVocabulary vocabulary)
{
    private readonly DrawingColourReader _colours = new();
    private readonly IReadOnlyList<DrawingPartReader> _parts =
    [
        new FillPartReader(), new LinePartReader(), new ShadowPartReader(), new GlowPartReader(),
        new BevelPartReader(), new ExtrusionPartReader(), new SoftEdgePartReader(), new TextPartReader(),
    ];

    public ShapeStyle Read(OpenXmlCompositeElement? properties, OpenXmlCompositeElement? body)
    {
        if (properties is null) return ShapeStyle.Default;

        var context = new DrawingReadContext(properties, body, vocabulary, _colours);
        return _parts.Aggregate(new ShapeStyle(), (style, part) => part.Read(context, style));
    }
}
