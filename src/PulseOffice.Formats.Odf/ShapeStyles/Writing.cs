using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class GraphicDefinitions
{
    private readonly List<XElement> _items = [];

    public IReadOnlyList<XElement> Items => _items;

    public void Add(XElement definition) => _items.Add(definition);

    public bool Has(XName kind, string name) =>
        _items.Any(item => item.Name == kind && (string?)item.Attribute(Namespaces.Draw + "name") == name);
}

internal sealed record GraphicWriteContext(
    XElement Graphic, string Name, GraphicDefinitions Definitions, OdfShapeVocabulary Vocabulary, OdfMeasure Measure)
{
    public void Set(XName attribute, object value) => Graphic.Add(new XAttribute(attribute, value));

    public XElement Definition(XName kind, string name, params object[] content) =>
        new(kind, new XAttribute(Namespaces.Draw + "name", name), new XAttribute(Namespaces.Draw + "display-name", name), content);
}

internal abstract class GraphicPropertyWriter
{
    public abstract void Write(GraphicWriteContext context, ShapeStyle style);
}

internal sealed class FillPropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        var fill = style.Fill;
        var measure = context.Measure;

        context.Set(Namespaces.Draw + "fill", context.Vocabulary.Fills.Token(fill.Kind));
        if (fill.Kind is ShapeFillKind.Solid) context.Set(Namespaces.Draw + "fill-color", measure.Colour(fill.ColorHex));
        if (fill.Opacity < 1) context.Set(Namespaces.Draw + "opacity", measure.Percent(fill.Opacity));

        if (fill.Kind is ShapeFillKind.Gradient) Gradient(context, fill.Gradient);
        if (fill.Kind is ShapeFillKind.Hatch) Hatch(context, fill.Hatch);
        if (fill.Kind is ShapeFillKind.Bitmap && fill.Bitmap is { } bitmap) Bitmap(context, bitmap);
    }

    private void Bitmap(GraphicWriteContext context, ShapeBitmap bitmap)
    {
        var name = $"{context.Name}-immagine";
        context.Set(Namespaces.Draw + "fill-image-name", name);
        context.Set(Namespaces.Style + "repeat", bitmap.Tiled ? "repeat" : "stretch");
        if (bitmap.Tiled && bitmap.TileWidthRatio > 0) context.Set(Namespaces.Draw + "fill-image-width", context.Measure.Number(bitmap.TileWidthRatio * 100) + "%");
        else if (bitmap.Tiled && bitmap.TileWidth.Value > 0) context.Set(Namespaces.Draw + "fill-image-width", context.Measure.Length(bitmap.TileWidth));
        if (bitmap.Tiled && bitmap.TileHeightRatio > 0) context.Set(Namespaces.Draw + "fill-image-height", context.Measure.Number(bitmap.TileHeightRatio * 100) + "%");
        else if (bitmap.Tiled && bitmap.TileHeight.Value > 0) context.Set(Namespaces.Draw + "fill-image-height", context.Measure.Length(bitmap.TileHeight));
        if (!context.Definitions.Has(Namespaces.Draw + "fill-image", name))
            context.Definitions.Add(context.Definition(Namespaces.Draw + "fill-image", name,
                new XElement(Namespaces.Office + "binary-data", Convert.ToBase64String(bitmap.Data))));
    }

    private void Gradient(GraphicWriteContext context, ShapeGradient gradient)
    {
        var measure = context.Measure;
        var name = $"{context.Name}-gradiente";

        context.Definitions.Add(context.Definition(Namespaces.Draw + "gradient", name,
            new XAttribute(Namespaces.Draw + "style", context.Vocabulary.Gradients.Token(gradient.Kind)),
            new XAttribute(Namespaces.Draw + "start-color", measure.Colour(gradient.StartHex)),
            new XAttribute(Namespaces.Draw + "end-color", measure.Colour(gradient.EndHex)),
            new XAttribute(Namespaces.Draw + "start-intensity", "100%"),
            new XAttribute(Namespaces.Draw + "end-intensity", "100%"),
            new XAttribute(Namespaces.Draw + "angle", context.Vocabulary.GradientAngles.Format(context.Vocabulary.GradientAngles.ToOdf(gradient.AngleDegrees))),
            new XAttribute(Namespaces.Draw + "border", measure.Percent(gradient.Border)),
            new XAttribute(Namespaces.Draw + "cx", measure.Percent(gradient.CentreX)),
            new XAttribute(Namespaces.Draw + "cy", measure.Percent(gradient.CentreY)),
            gradient.Stops.Count > 2 ? gradient.Stops.Select(stop => new XElement(Namespaces.Loext + "gradient-stop",
                new XAttribute(Namespaces.Svg + "offset", measure.Number(stop.Position)),
                new XAttribute(Namespaces.Loext + "color-type", "rgb"),
                new XAttribute(Namespaces.Loext + "color-value", measure.Colour(stop.ColorHex)))) : Enumerable.Empty<XElement>()));

        context.Set(Namespaces.Draw + "fill-gradient-name", name);
    }

    private void Hatch(GraphicWriteContext context, ShapeHatch hatch)
    {
        var measure = context.Measure;
        var name = $"{context.Name}-tratteggio";

        context.Definitions.Add(context.Definition(Namespaces.Draw + "hatch", name,
            new XAttribute(Namespaces.Draw + "style", context.Vocabulary.Hatches.Token(hatch.Kind)),
            new XAttribute(Namespaces.Draw + "color", measure.Colour(hatch.ColorHex)),
            new XAttribute(Namespaces.Draw + "distance", measure.Length(hatch.Distance)),
            new XAttribute(Namespaces.Draw + "rotation",
                ((int)Math.Round(hatch.AngleDegrees * 10)).ToString(CultureInfo.InvariantCulture))));

        context.Set(Namespaces.Draw + "fill-hatch-name", name);

        if (hatch.BackgroundHex is not { } background) return;
        context.Set(Namespaces.Draw + "fill-hatch-solid", "true");
        context.Set(Namespaces.Draw + "fill-color", measure.Colour(background));
    }
}

internal sealed class LinePropertyWriter(ArrowMarkers markers) : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        var line = style.Line;
        var measure = context.Measure;

        context.Set(Namespaces.Draw + "stroke", line.Dash switch
        {
            ShapeStrokeStyle.None => "none",
            ShapeStrokeStyle.Solid => "solid",
            _ => "dash",
        });

        if (line.Dash is not ShapeStrokeStyle.None and not ShapeStrokeStyle.Solid) Dash(context, line.CustomDash ?? _dashes.Preset(line.Dash));

        context.Set(Namespaces.Svg + "stroke-color", measure.Colour(line.ColorHex));
        context.Set(Namespaces.Svg + "stroke-width", measure.Length(line.Width));
        if (line.Opacity < 1) context.Set(Namespaces.Svg + "stroke-opacity", measure.Percent(line.Opacity));
        context.Set(Namespaces.Svg + "stroke-linejoin", context.Vocabulary.Joins.Token(line.Join));
        context.Set(Namespaces.Svg + "stroke-linecap", context.Vocabulary.Caps.Token(line.Cap));

        Marker(context, line, line.StartArrow, "start");
        Marker(context, line, line.EndArrow, "end");
    }

    private readonly OdfDashPatterns _dashes = new();

    private void Dash(GraphicWriteContext context, ShapeDashPattern pattern)
    {
        var name = $"{context.Name}-tratto";
        context.Definitions.Add(context.Definition(Namespaces.Draw + "stroke-dash", name, _dashes.Attributes(pattern, context.Measure)));
        context.Set(Namespaces.Draw + "stroke-dash", name);
    }

    private void Marker(GraphicWriteContext context, ShapeLine line, ArrowEnd arrow, string side)
    {
        if (arrow is ArrowEnd.None) return;

        var marker = markers.For(arrow);
        if (!context.Definitions.Has(Namespaces.Draw + "marker", marker.Name))
            context.Definitions.Add(context.Definition(Namespaces.Draw + "marker", marker.Name,
                new XAttribute(Namespaces.Svg + "viewBox", "0 0 20 30"),
                new XAttribute(Namespaces.Svg + "d", marker.Path)));

        context.Set(Namespaces.Draw + $"marker-{side}", marker.Name);
        context.Set(Namespaces.Draw + $"marker-{side}-width",
            context.Measure.Length(new Twip((int)(line.Width.Value * 4 * line.ArrowScale))));
    }
}

internal sealed class ShadowPropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        var shadow = style.Shadow;
        var measure = context.Measure;

        context.Set(Namespaces.Draw + "shadow", shadow.Enabled ? "visible" : "hidden");
        if (!shadow.Enabled) return;

        context.Set(Namespaces.Draw + "shadow-offset-x", measure.Length(shadow.OffsetX));
        context.Set(Namespaces.Draw + "shadow-offset-y", measure.Length(shadow.OffsetY));
        context.Set(Namespaces.Draw + "shadow-color", measure.Colour(shadow.ColorHex));
        context.Set(Namespaces.Draw + "shadow-opacity", measure.Percent(shadow.Opacity));
        context.Set(Namespaces.Loext + "shadow-blur", measure.Length(shadow.Blur));
    }
}

internal sealed class GlowPropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        if (!style.Glow.Enabled) return;

        var measure = context.Measure;
        context.Set(Namespaces.Loext + "glow-radius", measure.Length(style.Glow.Radius));
        context.Set(Namespaces.Loext + "glow-color", measure.Colour(style.Glow.ColorHex));
        context.Set(Namespaces.Loext + "glow-transparency", measure.Percent(1 - style.Glow.Opacity));
    }
}

internal sealed class SoftEdgePropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        if (style.SoftEdge.Value > 0) context.Set(Namespaces.Loext + "softedge-radius", context.Measure.Length(style.SoftEdge));
    }
}

internal sealed class BevelPropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        if (!style.Bevel.Enabled) return;

        context.Set(Namespaces.Draw + "extrusion", "true");
        context.Set(context.Vocabulary.PulseShape + "bevel-style", context.Vocabulary.Bevels.Token(style.Bevel.Style));
        context.Set(Namespaces.Draw + "extrusion-depth", $"{context.Measure.Length(style.Bevel.Depth)} 0");
    }
}

internal sealed class TextAreaPropertyWriter : GraphicPropertyWriter
{
    public override void Write(GraphicWriteContext context, ShapeStyle style)
    {
        var text = style.Text;

        context.Set(Namespaces.Draw + "auto-grow-width", "false");
        context.Set(Namespaces.Draw + "auto-grow-height", "false");
        context.Set(Namespaces.Fo + "wrap-option", text.Wrap ? "wrap" : "no-wrap");
        context.Set(Namespaces.Fo + "padding", context.Measure.Length(text.Inset));
        context.Set(Namespaces.Draw + "textarea-horizontal-align", context.Vocabulary.AreaAlignment.Token(text.Horizontal));
        context.Set(Namespaces.Draw + "textarea-vertical-align", context.Vocabulary.VerticalAlignment.Token(text.Vertical));

        if (Math.Abs(text.RotationDegrees) > 0.001)
            context.Set(Namespaces.Style + "text-rotation-angle", context.Measure.Number(text.RotationDegrees));
    }
}

internal sealed class ShapeTextPropertiesBuilder(OdfMeasure measure)
{
    public XElement Build(ShapeTextStyle text)
    {
        var properties = new XElement(Namespaces.Style + "text-properties",
            new XAttribute(Namespaces.Fo + "color", measure.Colour(text.ColorHex)),
            new XAttribute(Namespaces.Fo + "font-family", text.Family),
            new XAttribute(Namespaces.Fo + "font-size", $"{measure.Number(text.Size)}pt"));

        if (text.AutomaticColor) properties.Add(new XAttribute(Namespaces.Style + "use-window-font-color", "true"));
        if (text.Bold) properties.Add(new XAttribute(Namespaces.Fo + "font-weight", "bold"));
        if (text.Italic) properties.Add(new XAttribute(Namespaces.Fo + "font-style", "italic"));
        if (text.Underline) properties.Add(new XAttribute(Namespaces.Style + "text-underline-style", "solid"));
        if (text.Language is { Length: > 0 } language) AddLanguage(properties, language);
        return properties;
    }

    private static void AddLanguage(XElement properties, string language)
    {
        var parts = language.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        properties.Add(new XAttribute(Namespaces.Fo + "language", parts[0]));
        if (parts.Length > 2) properties.Add(new XAttribute(Namespaces.Fo + "script", parts[1]));
        if (parts.Length > 1) properties.Add(new XAttribute(Namespaces.Fo + "country", parts[^1]));
    }
}
