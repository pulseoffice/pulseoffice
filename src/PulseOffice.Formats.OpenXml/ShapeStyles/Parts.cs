using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingColours
{
    public A.RgbColorModelHex Of(string hex, double opacity)
    {
        var digits = hex.TrimStart('#').PadLeft(6, '0')[^6..].ToUpperInvariant();
        var colour = new A.RgbColorModelHex { Val = digits.All(char.IsAsciiHexDigit) ? digits : "000000" };
        if (opacity < 1) colour.AppendChild(new A.Alpha { Val = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 100000) });
        return colour;
    }

    public int Angle(double degrees) => (int)Math.Round(((degrees % 360) + 360) % 360 * 60000);
}

internal abstract class FillBuilder(DrawingColours colours)
{
    protected DrawingColours Colours { get; } = colours;

    public abstract bool Builds(ShapeFill fill);

    public abstract OpenXmlElement Build(ShapeFill fill);
}

internal sealed class NoFillBuilder(DrawingColours colours) : FillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) => fill.Kind is ShapeFillKind.None;

    public override OpenXmlElement Build(ShapeFill fill) => new A.NoFill();
}

internal sealed class SolidFillBuilder(DrawingColours colours) : FillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) => fill.Kind is ShapeFillKind.Solid;

    public override OpenXmlElement Build(ShapeFill fill) => new A.SolidFill(Colours.Of(fill.ColorHex, fill.Opacity));
}

internal abstract class GradientFillBuilder(DrawingColours colours) : FillBuilder(colours)
{
    public override OpenXmlElement Build(ShapeFill fill)
    {
        var stops = new A.GradientStopList();
        foreach (var stop in fill.Gradient.Stops.OrderBy(stop => stop.Position))
            stops.AppendChild(new A.GradientStop(Colours.Of(stop.ColorHex, fill.Opacity))
            {
                Position = (int)Math.Round(Math.Clamp(stop.Position, 0, 1) * 100000),
            });

        var gradient = new A.GradientFill(stops);
        gradient.AppendChild(Shade(fill.Gradient));
        return gradient;
    }

    protected abstract OpenXmlElement Shade(ShapeGradient gradient);
}

internal sealed class LinearGradientBuilder(DrawingColours colours) : GradientFillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) =>
        fill.Kind is ShapeFillKind.Gradient && fill.Gradient.Kind is GradientKind.Linear or GradientKind.Axial;

    protected override OpenXmlElement Shade(ShapeGradient gradient) =>
        new A.LinearGradientFill { Angle = Colours.Angle(gradient.AngleDegrees), Scaled = true };
}

internal sealed class PathGradientBuilder(DrawingColours colours) : GradientFillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) => fill.Kind is ShapeFillKind.Gradient;

    protected override OpenXmlElement Shade(ShapeGradient gradient) =>
        new A.PathGradientFill(new A.FillToRectangle
        {
            Left = (int)Math.Round(gradient.CentreX * 100000),
            Top = (int)Math.Round(gradient.CentreY * 100000),
            Right = (int)Math.Round((1 - gradient.CentreX) * 100000),
            Bottom = (int)Math.Round((1 - gradient.CentreY) * 100000),
        })
        {
            Path = new A.PathShadeValues(gradient.Kind is GradientKind.Rectangular or GradientKind.Square ? "rect" : "circle"),
        };
}

internal sealed class BitmapFillBuilder(DrawingColours colours, OoxmlPendingImages pending) : FillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) => fill.Kind is ShapeFillKind.Bitmap && fill.Bitmap is not null;

    public override OpenXmlElement Build(ShapeFill fill)
    {
        var bitmap = fill.Bitmap!;
        var blip = pending.Blip(bitmap.Data);
        if (fill.Opacity < 1) blip.PrependChild(new A.AlphaModulationFixed { Amount = (int)Math.Round(Math.Clamp(fill.Opacity, 0, 1) * 100000) });
        var result = new A.BlipFill(blip) { RotateWithShape = true };
        result.AppendChild<OpenXmlElement>(bitmap.Tiled
            ? new A.Tile { HorizontalOffset = 0, VerticalOffset = 0, HorizontalRatio = 100000, VerticalRatio = 100000, Flip = A.TileFlipValues.None, Alignment = A.RectangleAlignmentValues.TopLeft }
            : new A.Stretch(new A.FillRectangle()));
        return result;
    }
}

internal sealed class PatternFillBuilder(DrawingColours colours, DrawingVocabulary vocabulary) : FillBuilder(colours)
{
    public override bool Builds(ShapeFill fill) => fill.Kind is ShapeFillKind.Hatch;

    public override OpenXmlElement Build(ShapeFill fill) =>
        new A.PatternFill(
            new A.ForegroundColor(Colours.Of(fill.Hatch.ColorHex, fill.Opacity)),
            new A.BackgroundColor(Colours.Of(fill.Hatch.BackgroundHex ?? "#FFFFFF", fill.Hatch.BackgroundHex is null ? 0 : fill.Opacity)))
        {
            Preset = new A.PresetPatternValues(vocabulary.Hatches.Token(fill.Hatch.Kind)),
        };
}

internal abstract class EffectBuilder(DrawingColours colours)
{
    protected DrawingColours Colours { get; } = colours;

    public abstract OpenXmlElement? Build(ShapeStyle style);
}

internal sealed class GlowEffectBuilder(DrawingColours colours) : EffectBuilder(colours)
{
    public override OpenXmlElement? Build(ShapeStyle style) => style.Glow.Enabled
        ? new A.Glow(Colours.Of(style.Glow.ColorHex, style.Glow.Opacity)) { Radius = Emu.FromTwips(style.Glow.Radius).Value }
        : null;
}

internal sealed class ShadowEffectBuilder(DrawingColours colours) : EffectBuilder(colours)
{
    public override OpenXmlElement? Build(ShapeStyle style)
    {
        var shadow = style.Shadow;
        if (!shadow.Enabled) return null;

        var distance = Math.Sqrt(Math.Pow(shadow.OffsetX.Value, 2) + Math.Pow(shadow.OffsetY.Value, 2));
        var direction = Math.Atan2(shadow.OffsetY.Value, shadow.OffsetX.Value) * 180 / Math.PI;

        return new A.OuterShadow(Colours.Of(shadow.ColorHex, shadow.Opacity))
        {
            BlurRadius = Emu.FromTwips(shadow.Blur).Value,
            Distance = Emu.FromTwips(new Twip((int)Math.Round(distance))).Value,
            Direction = Colours.Angle(direction),
            RotateWithShape = false,
        };
    }
}

internal sealed class SoftEdgeEffectBuilder(DrawingColours colours) : EffectBuilder(colours)
{
    public override OpenXmlElement? Build(ShapeStyle style) =>
        style.SoftEdge.Value > 0 ? new A.SoftEdge { Radius = Emu.FromTwips(style.SoftEdge).Value } : null;
}
