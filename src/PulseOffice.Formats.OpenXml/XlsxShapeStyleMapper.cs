using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxShapeStyleMapper
{
    private const long LargestLineWidth = 20116800;

    private readonly DrawingColours _colours = new();
    private readonly DrawingVocabulary _vocabulary = new();
    private readonly DrawingStyleFidelity _fidelity = new();
    private readonly DrawingStyleReader _reader;
    private readonly IReadOnlyList<FillBuilder> _fills;
    private readonly IReadOnlyList<EffectBuilder> _effects;

    public XlsxShapeStyleMapper()
    {
        _fills =
        [
            new NoFillBuilder(_colours), new LinearGradientBuilder(_colours), new PathGradientBuilder(_colours),
            new PatternFillBuilder(_colours, _vocabulary), new BitmapFillBuilder(_colours, new OoxmlPendingImages()), new SolidFillBuilder(_colours),
        ];
        _effects = [new GlowEffectBuilder(_colours), new ShadowEffectBuilder(_colours), new SoftEdgeEffectBuilder(_colours)];
        _reader = new DrawingStyleReader(_vocabulary);
    }

    public ShapeStyle Read(OpenXmlCompositeElement? properties, OpenXmlCompositeElement? body) => _reader.Read(properties, body);

    public string Arrow(ArrowEnd end) => _vocabulary.Arrows.Token(end);

    public TextVerticalPlacement Anchor(string token) => _vocabulary.Anchors.Value(token);

    public string Connector(ConnectorKind kind) => _vocabulary.Connectors.Token(kind);

    public ConnectorKind Connector(string? preset) => _vocabulary.Connectors.Value(preset);

    public OpenXmlElement Fill(ShapeFill fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        return (_fills.FirstOrDefault(builder => builder.Builds(fill)) ?? _fills[^1]).Build(fill);
    }

    public A.Outline Line(ShapeLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var outline = new A.Outline
        {
            Width = (int)Math.Clamp(Emu.FromTwips(line.Width).Value, 0, LargestLineWidth),
            CapType = new A.LineCapValues(_vocabulary.Caps.Token(line.Cap)),
        };

        if (line.Dash is ShapeStrokeStyle.None)
        {
            outline.AppendChild(new A.NoFill());
            return outline;
        }

        outline.AppendChild(new A.SolidFill(Colour(line.ColorHex, line.Opacity)));

        if (line.CustomDash is { } custom && line.Dash is not ShapeStrokeStyle.Solid)
        {
            var steps = custom.InLineWidths(Math.Max(1, line.Width.Value));
            var dashes = new A.CustomDash();
            for (var index = 0; index + 1 < steps.Count; index += 2)
                dashes.AppendChild(new A.DashStop { DashLength = (int)Math.Round(steps[index] * 100000), SpaceLength = (int)Math.Round(steps[index + 1] * 100000) });
            outline.AppendChild(dashes);
        }
        else
            outline.AppendChild(new A.PresetDash
            {
                Val = new A.PresetLineDashValues(_vocabulary.Dashes.Token(line.Dash)),
            });

        outline.AppendChild(line.Join switch
        {
            LineJoinStyle.Miter => new A.Miter { Limit = 800000 },
            LineJoinStyle.Bevel => (OpenXmlElement)new A.LineJoinBevel(),
            _ => new A.Round(),
        });

        var size = _fidelity.ArrowSize(line.ArrowScale);
        if (line.StartArrow is not ArrowEnd.None)
            outline.AppendChild(new A.HeadEnd { Type = new A.LineEndValues(Arrow(line.StartArrow)), Width = new A.LineEndWidthValues(size), Length = new A.LineEndLengthValues(size) });

        if (line.EndArrow is not ArrowEnd.None)
            outline.AppendChild(new A.TailEnd { Type = new A.LineEndValues(Arrow(line.EndArrow)), Width = new A.LineEndWidthValues(size), Length = new A.LineEndLengthValues(size) });

        return outline;
    }

    public A.EffectList? Effects(ShapeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var built = _effects.Select(effect => effect.Build(style)).OfType<OpenXmlElement>().ToList();
        return built.Count == 0 ? null : new A.EffectList(built);
    }

    public A.Scene3DType? Scene(ShapeStyle style) => style.Bevel.Enabled || style.Extrusion.Enabled ? new A.Scene3DType(
        new A.Camera { Preset = style.Extrusion.Enabled ? A.PresetCameraValues.IsometricOffAxis1Right : A.PresetCameraValues.OrthographicFront },
        new A.LightRig(new A.Rotation { Latitude = 0, Longitude = 0, Revolution = 1200000 })
        {
            Rig = A.LightRigValues.ThreePoints,
            Direction = A.LightRigDirectionValues.Top,
        }) : null;

    public A.Shape3DType? Bevel(ShapeStyle style)
    {
        var bevel = style.Bevel;
        if (!bevel.Enabled && !style.Extrusion.Enabled) return null;

        var shape = new A.Shape3DType();
        if (style.Extrusion.Enabled) shape.ExtrusionHeight = Emu.FromTwips(style.Extrusion.Depth).Value;
        if (bevel.Enabled)
        {
            var depth = Emu.FromTwips(bevel.Depth).Value;
            shape.Append(new A.BevelTop
            {
                Width = depth,
                Height = depth,
                Preset = new A.BevelPresetValues(_vocabulary.Bevels.Token(bevel.Style)),
            });
        }
        return shape;
    }

    public A.BodyProperties Body(ShapeTextStyle text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var inset = (int)Emu.FromTwips(text.Inset).Value;

        var body = new A.BodyProperties
        {
            Anchor = new A.TextAnchoringTypeValues(_vocabulary.Anchors.Token(text.Vertical)),
            LeftInset = inset,
            RightInset = inset,
            TopInset = inset,
            BottomInset = inset,
            Wrap = new A.TextWrappingValues(text.Wrap ? "square" : "none"),
            Rotation = _colours.Angle(text.RotationDegrees),
        };

        return body;
    }

    public A.RunProperties Run(ShapeTextStyle text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var properties = new A.RunProperties
        {
            FontSize = (int)Math.Round(text.Size * 100),
            Bold = text.Bold,
            Italic = text.Italic,
            Underline = new A.TextUnderlineValues(text.Underline ? "sng" : "none"),
        };

        if (text.Language is { Length: > 0 } language) properties.Language = language;
        if (!text.AutomaticColor) properties.AppendChild(new A.SolidFill(Colour(text.ColorHex, 1)));
        properties.AppendChild(new A.LatinFont { Typeface = text.Family });

        return properties;
    }

    public A.ParagraphProperties Paragraph(ShapeTextStyle text) => new()
    {
        Alignment = new A.TextAlignmentTypeValues(_vocabulary.Alignments.Token(text.Horizontal)),
    };

    private A.RgbColorModelHex Colour(string hex, double opacity) => _colours.Of(hex, opacity);
}
