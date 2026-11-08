using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.ConditionalIcons;

public sealed class ArrowGlyph(double degrees, IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        var arrow = new OutlineBuilder().Polygon(
            (0.5, 0.03), (0.97, 0.5), (0.75, 0.5), (0.75, 0.97), (0.25, 0.97), (0.25, 0.5), (0.03, 0.5)).Build();
        yield return Filled(Rotated(arrow, degrees), tone);
    }
}

public sealed class TriangleGlyph(int direction, IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        var outline = direction switch
        {
            > 0 => new OutlineBuilder().Polygon((0.5, 0.2), (0.9, 0.8), (0.1, 0.8)).Build(),
            < 0 => new OutlineBuilder().Polygon((0.1, 0.2), (0.9, 0.2), (0.5, 0.8)).Build(),
            _ => new OutlineBuilder().Rectangle(0.15, 0.38, 0.7, 0.24).Build(),
        };
        yield return Filled(outline, tone);
    }
}

public sealed class CircleGlyph(IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return Filled(new OutlineBuilder().Ellipse(0.5, 0.5, 0.46, 0.46).Build(), tone);
    }
}

public sealed class TrafficLightGlyph(IconTone lamp, string housing) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return new IconLayer(new OutlineBuilder().Rectangle(0.02, 0.02, 0.96, 0.96).Build(), housing, null);
        yield return new IconLayer(new OutlineBuilder().Ellipse(0.5, 0.5, 0.34, 0.34).Build(), lamp.Fill, null);
    }
}

public sealed class FlagGlyph(IconTone tone, string pole) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return new IconLayer(new OutlineBuilder().Rectangle(0.13, 0.03, 0.12, 0.94).Build(), pole, null);
        yield return Filled(new OutlineBuilder().Move(0.25, 0.06)
            .Cubic(0.45, -0.02, 0.6, 0.14, 0.95, 0.06).Line(0.95, 0.52)
            .Cubic(0.6, 0.6, 0.45, 0.44, 0.25, 0.52).Close().Build(), tone);
    }
}

public sealed class DiamondGlyph(IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return Filled(new OutlineBuilder().Polygon((0.5, 0.03), (0.97, 0.5), (0.5, 0.97), (0.03, 0.5)).Build(), tone);
    }
}

public sealed class WarningTriangleGlyph(IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return Filled(new OutlineBuilder().Polygon((0.5, 0.06), (0.97, 0.92), (0.03, 0.92)).Build(), tone);
    }
}

public abstract class SymbolGlyph(IconTone tone, string mark, bool circled) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        if (circled) yield return Filled(new OutlineBuilder().Ellipse(0.5, 0.5, 0.46, 0.46).Build(), tone);
        yield return new IconLayer(Mark(), null, circled ? mark : tone.Stroke, circled ? 0.14 : 0.2);
    }

    protected abstract ShapeOutline Mark();
}

public sealed class CheckGlyph(IconTone tone, string mark, bool circled) : SymbolGlyph(tone, mark, circled)
{
    protected override ShapeOutline Mark() => new OutlineBuilder().Polyline((0.26, 0.52), (0.43, 0.69), (0.75, 0.33)).Build();
}

public sealed class CrossGlyph(IconTone tone, string mark, bool circled) : SymbolGlyph(tone, mark, circled)
{
    protected override ShapeOutline Mark() =>
        new OutlineBuilder().Polyline((0.32, 0.32), (0.68, 0.68)).Polyline((0.68, 0.32), (0.32, 0.68)).Build();
}

public sealed class ExclamationGlyph(IconTone tone, string mark, bool circled) : SymbolGlyph(tone, mark, circled)
{
    protected override ShapeOutline Mark() =>
        new OutlineBuilder().Polyline((0.5, 0.22), (0.5, 0.58)).Polyline((0.5, 0.74), (0.5, 0.78)).Build();
}

public sealed class SmileyGlyph(int mood, IconTone tone) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return Filled(new OutlineBuilder().Ellipse(0.5, 0.5, 0.46, 0.46).Build(), tone);
        yield return new IconLayer(new OutlineBuilder().Ellipse(0.35, 0.38, 0.06, 0.07).Ellipse(0.65, 0.38, 0.06, 0.07).Build(), tone.Stroke, null);
        var curve = 0.12 * mood;
        yield return new IconLayer(new OutlineBuilder().Move(0.3, 0.66 - curve / 2)
            .Cubic(0.4, 0.66 + curve, 0.6, 0.66 + curve, 0.7, 0.66 - curve / 2).Build(), null, tone.Stroke, Stroke);
    }
}

public sealed class StarGlyph(double filled, IconTone full, IconTone empty) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        var points = Enumerable.Range(0, 10).Select(index =>
        {
            var radius = index % 2 == 0 ? 0.48 : 0.2;
            var angle = (-90 + index * 36) * Math.PI / 180;
            return (0.5 + radius * Math.Cos(angle), 0.53 + radius * Math.Sin(angle));
        }).ToArray();
        var star = new OutlineBuilder().Polygon(points).Build();

        if (filled >= 1) { yield return Filled(star, full); yield break; }
        yield return Filled(star, empty);
        if (filled <= 0) yield break;

        var half = points.Select(point => (Math.Min(point.Item1, 0.5), point.Item2)).ToArray();
        yield return Filled(new OutlineBuilder().Polygon(half).Build(), full);
    }
}

public sealed class PieGlyph(double filled, IconTone body, string paper) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        yield return new IconLayer(new OutlineBuilder().Ellipse(0.5, 0.5, 0.45, 0.45).Build(), paper, body.Stroke, Stroke);
        if (filled <= 0) yield break;
        if (filled >= 1) { yield return Filled(new OutlineBuilder().Ellipse(0.5, 0.5, 0.45, 0.45).Build(), body); yield break; }

        var steps = Math.Max(2, (int)Math.Ceiling(filled * 32));
        var points = new List<(double, double)> { (0.5, 0.5) };
        for (var step = 0; step <= steps; step++)
        {
            var angle = (-90 + 360 * filled * step / steps) * Math.PI / 180;
            points.Add((0.5 + 0.45 * Math.Cos(angle), 0.5 + 0.45 * Math.Sin(angle)));
        }
        yield return Filled(new OutlineBuilder().Polygon([.. points]).Build(), body);
    }
}

public sealed class BarsGlyph(int lit, string on, string off) : IconGlyph
{
    protected override IEnumerable<IconLayer> Layers()
    {
        for (var bar = 0; bar < 4; bar++)
        {
            var height = 0.375 + bar * 0.1875;
            var rectangle = new OutlineBuilder().Rectangle(0.03 + bar * 0.25, 0.97 - height, 0.16, height).Build();
            yield return bar < lit ? new IconLayer(rectangle, on, on, Stroke) : new IconLayer(rectangle, null, off, Stroke);
        }
    }
}

public sealed class SquaresGlyph(int lit, string on, string off) : IconGlyph
{
    private readonly (double X, double Y)[] _order = [(0.03, 0.03), (0.56, 0.03), (0.56, 0.56), (0.03, 0.56)];

    protected override IEnumerable<IconLayer> Layers()
    {
        for (var square = 0; square < 4; square++)
        {
            var rectangle = new OutlineBuilder().Rectangle(_order[square].X, _order[square].Y, 0.41, 0.41).Build();
            yield return square < lit ? new IconLayer(rectangle, on, on, Stroke) : new IconLayer(rectangle, null, off, Stroke);
        }
    }
}
