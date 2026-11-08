using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeRenderer
{
    void Render(DrawingContext context, ShapeObject shape, Rect bounds);

    void Render(DrawingContext context, ShapeObject shape, Rect bounds, ShapeSurroundings backdrop);

    void Render(
        DrawingContext context,
        ShapeOutline outline,
        ShapeStyle style,
        Matrix transform,
        string? text,
        Rect bounds);
}

public sealed class ShapeRenderer : IShapeRenderer
{
    public const double MinimumFontSize = 1;

    public const double MaximumFontSize = 1638;

    private readonly IShapeGeometry _geometry;
    private readonly IShapeBrushes _brushes;
    private readonly PulseOffice.UI.Controls.Text.Rich.IHyperlinkAppearance _links;
    private readonly IArrowGeometry _arrows;
    private readonly IShapeTextInk _ink;
    private readonly IShapeEffectBounds _effects;
    private readonly IDimensionLineGeometry _dimensions;
    private readonly IShapeListText _listText;
    private readonly IReadOnlyDictionary<ShapeShade, IBrush> _shades = new Dictionary<ShapeShade, IBrush>
    {
        [ShapeShade.Darken] = new ImmutableSolidColorBrush(Colors.Black, 0.4),
        [ShapeShade.DarkenLess] = new ImmutableSolidColorBrush(Colors.Black, 0.2),
        [ShapeShade.Lighten] = new ImmutableSolidColorBrush(Colors.White, 0.4),
        [ShapeShade.LightenLess] = new ImmutableSolidColorBrush(Colors.White, 0.2),
    };

    public ShapeRenderer(
        IShapeGeometry geometry, IShapeBrushes? brushes = null, IArrowGeometry? arrows = null,
        PulseOffice.UI.Controls.Text.Rich.IHyperlinkAppearance? links = null, IShapeTextInk? ink = null, IShapeEffectBounds? effects = null,
        IDimensionLineGeometry? dimensions = null, IShapeListText? listText = null)
    {
        _listText = listText ?? new ShapeListText();
        _dimensions = dimensions ?? new DimensionLineGeometry();
        _effects = effects ?? new ShapeEffectBounds();
        _ink = ink ?? new ShapeTextInk();
        _geometry = geometry;
        _brushes = brushes ?? new ShapeBrushes();
        _arrows = arrows ?? new ArrowGeometry();
        _links = links ?? new PulseOffice.UI.Controls.Text.Rich.HyperlinkAppearance();
    }

    public ShapeRenderer() : this(new ShapeGeometry()) { }

    private bool Drawable(Rect bounds) =>
        bounds.Width >= 0 && bounds.Height >= 0 && bounds.Width + bounds.Height > 0;

    public void Render(DrawingContext context, ShapeObject shape, Rect bounds) => Render(context, shape, bounds, new ShapeSurroundings());

    public void Render(DrawingContext context, ShapeObject shape, Rect bounds, ShapeSurroundings backdrop)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shape);

        if (!Drawable(bounds)) return;

        if (shape.Dimension is { } dimension)
        {
            PaintDimension(context, shape, dimension, bounds, backdrop);
            return;
        }

        Paint(context, _geometry.Outline(shape, bounds.Width, bounds.Height), shape.Style, TransformOf(shape, bounds), shape.Text, shape.Runs, bounds, backdrop, shape.RotationDegrees,
            _listText.Draws(shape) ? shape : null);
    }

    public void Render(
        DrawingContext context,
        ShapeOutline outline,
        ShapeStyle style,
        Matrix transform,
        string? text,
        Rect bounds) => Paint(context, outline, style, transform, text, null, bounds, new ShapeSurroundings(), 0, null);

    private void Paint(
        DrawingContext context,
        ShapeOutline outline,
        ShapeStyle style,
        Matrix transform,
        string? text,
        IReadOnlyList<ShapeTextRun>? runs,
        Rect bounds,
        ShapeSurroundings backdrop,
        double shapeRotation,
        ShapeObject? listed)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(style);

        if (!Drawable(bounds)) return;

        var scaled = outline.Scaled(bounds.Width, bounds.Height);
        var geometry = Build(scaled);

        var fill = _brushes.Fill(style.Fill, bounds);
        var pen = _brushes.Stroke(style.Line);

        var local = new Rect(0, 0, bounds.Width, bounds.Height);

        using (context.PushTransform(transform))
        {
            if (style.Shadow.Enabled) DrawShadow(context, geometry, style.Shadow, local);
            if (style.Extrusion.Enabled) DrawExtrusion(context, geometry, style, local);
            if (style.Glow.Enabled) DrawGlow(context, geometry, style.Glow, local);

            using (SoftEdge(context, style.SoftEdge, local))
            {
                if (scaled.Figures.All(figure => figure.Filled && figure.Stroked && figure.Shade is ShapeShade.Normal))
                {
                    context.DrawGeometry(fill, pen, geometry);
                }
                else
                {
                    foreach (var shaded in scaled.Figures.Where(figure => figure.Filled).GroupBy(figure => figure.Shade))
                    {
                        var area = Build(new ShapeOutline([.. shaded]));
                        context.DrawGeometry(fill, null, area);
                        if (fill is not null && _shades.TryGetValue(shaded.Key, out var veil)) context.DrawGeometry(veil, null, area);
                    }
                    var stroked = scaled.Figures.Where(figure => figure.Stroked).ToList();
                    if (stroked.Count > 0 && pen is not null) context.DrawGeometry(null, pen, Build(new ShapeOutline(stroked)));
                }
            }

            if (pen is not null) DrawArrows(context, scaled, style.Line, pen);

            if (style.Bevel.Enabled) DrawBevel(context, geometry, bounds, style.Bevel);
        }

        var content = runs is { Count: > 0 } ? string.Concat(runs.Select(run => run.Text)) : text;
        if (string.IsNullOrEmpty(content) || bounds.Width <= 0 || bounds.Height <= 0) return;
        if (listed is not null) DrawListText(context, listed, style, bounds, backdrop, shapeRotation);
        else DrawText(context, content, runs, style, bounds, backdrop, shapeRotation);
    }

    private void DrawListText(DrawingContext context, ShapeObject shape, ShapeStyle shapeStyle, Rect bounds, ShapeSurroundings backdrop, double shapeRotation)
    {
        var ink = _brushes.Colour(_ink.Resolve(shapeStyle, Behind(shapeStyle.Fill, backdrop).Hex), Colors.White);
        var rotation = shapeStyle.Text.RotationDegrees + shapeRotation;
        if (Math.Abs(rotation % 360) < 0.001)
        {
            _listText.Draw(context, shape, bounds, ink, backdrop.Language);
            return;
        }
        var centre = bounds.Center;
        using (context.PushTransform(
            Matrix.CreateTranslation(-centre.X, -centre.Y)
            * Matrix.CreateRotation(rotation * Math.PI / 180)
            * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            _listText.Draw(context, shape, bounds, ink, backdrop.Language);
        }
    }

    private void PaintDimension(DrawingContext context, ShapeObject shape, DimensionLine dimension, Rect bounds, ShapeSurroundings backdrop)
    {
        var scaled = _geometry.Outline(shape, bounds.Width, bounds.Height).Scaled(bounds.Width, bounds.Height);
        var pen = _brushes.Stroke(shape.Style.Line);
        var transform = TransformOf(shape, bounds);
        if (pen is not null && scaled.Figures.Count > 0)
            using (context.PushTransform(transform))
            {
                context.DrawGeometry(null, pen, Build(scaled));
                DrawArrows(context, new ShapeOutline([scaled.Figures[0]]), shape.Style.Line, pen);
            }

        var layout = _dimensions.Layout(dimension, bounds.Width, bounds.Height);
        var label = shape.Text is { Length: > 0 } text ? text : _dimensions.Label(dimension, layout.MeasuredLength, System.Globalization.CultureInfo.CurrentCulture);
        if (label.Length == 0) return;
        var size = Legible(shape.Style.Text.Size) ?? ShapeTextStyle.Default.Size;
        var height = size * 1.5;
        Point OnPage(ShapePoint point) => transform.Transform(new Point(point.X, point.Y));
        var centre = OnPage(_dimensions.LabelCentre(dimension, bounds.Width, bounds.Height, height));
        var (from, to) = (OnPage(layout.LineStart), OnPage(layout.LineEnd));
        var readable = _dimensions.ReadableAngle(new ShapePoint(from.X, from.Y), new ShapePoint(to.X, to.Y));
        var width = Math.Max(layout.LineLength, size * label.Length);
        var labelStyle = shape.Style with
        {
            Text = shape.Style.Text with
            {
                AutomaticColor = true,
                Horizontal = TextHorizontalPlacement.Centre,
                Vertical = TextVerticalPlacement.Middle,
                Wrap = true,
                Inset = Twip.Zero,
                RotationDegrees = 0,
            },
        };
        DrawText(context, label, null, labelStyle, new Rect(centre.X - width / 2, centre.Y - height / 2, width, height), backdrop, readable);
    }

    private void DrawArrows(DrawingContext context, ShapeOutline outline, ShapeLine line, Pen pen)
    {
        if (line.StartArrow is ArrowEnd.None && line.EndArrow is ArrowEnd.None) return;
        if (outline.Figures.Count != 1 || outline.Figures[0].IsClosed) return;
        var points = new SegmentEnds().Of(outline).ToList();
        if (points.Count < 2) return;
        var size = Math.Max(6, line.Width.ToPoints() * 4) * line.ArrowScale;
        var brush = pen.Brush ?? Brushes.Black;
        if (line.StartArrow is not ArrowEnd.None) context.DrawGeometry(brush, new Pen(brush, 1), Build(_arrows.Head(line.StartArrow, points[0], points[1], size)));
        if (line.EndArrow is not ArrowEnd.None) context.DrawGeometry(brush, new Pen(brush, 1), Build(_arrows.Head(line.EndArrow, points[^1], points[^2], size)));
    }

    private void DrawGlow(DrawingContext context, Geometry geometry, ShapeGlow glow, Rect local)
    {
        var radius = Math.Max(1, _effects.Radius(glow.Radius.ToPoints()));
        var colour = _brushes.Colour(glow.ColorHex, Colors.SkyBlue);

        using (context.PushEffect(new BlurEffect { Radius = radius }, local.Inflate(radius * 2)))
        using (context.PushOpacity(_effects.Opacity(glow.Opacity)))
        {
            context.DrawGeometry(
                new SolidColorBrush(colour),
                new Pen(new SolidColorBrush(colour), radius),
                geometry);
        }
    }

    private void DrawExtrusion(DrawingContext context, Geometry geometry, ShapeStyle style, Rect local)
    {
        var depth = Math.Clamp(style.Extrusion.Depth.ToPoints(), 1, Math.Max(1, Math.Max(local.Width, local.Height)));
        var steps = (int)Math.Clamp(depth, 2, 48);
        var lathe = style.Extrusion.Kind == ExtrusionKind.Lathe;
        var colour = _brushes.Colour(style.Fill.IsVisible ? style.Fill.ColorHex : style.Line.ColorHex, Colors.SteelBlue);

        for (var step = steps; step >= 1; step--)
        {
            var share = (double)step / steps;
            var dx = lathe ? 0 : depth * share * 0.7;
            var dy = lathe ? depth * share : -depth * share * 0.7;
            var shade = 0.55 + 0.25 * (1 - share);
            var tone = Color.FromArgb(colour.A, (byte)(colour.R * shade), (byte)(colour.G * shade), (byte)(colour.B * shade));
            using (context.PushTransform(Matrix.CreateTranslation(dx, dy)))
                context.DrawGeometry(new SolidColorBrush(tone), null, geometry);
        }

        if (lathe)
        {
            var shading = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new Avalonia.Media.GradientStop(Color.FromArgb(90, 0, 0, 0), 0),
                    new Avalonia.Media.GradientStop(Color.FromArgb(70, 255, 255, 255), 0.35),
                    new Avalonia.Media.GradientStop(Color.FromArgb(0, 0, 0, 0), 0.6),
                    new Avalonia.Media.GradientStop(Color.FromArgb(110, 0, 0, 0), 1),
                },
            };
            using (context.PushTransform(Matrix.CreateTranslation(0, depth)))
                context.DrawGeometry(shading, null, geometry);
        }
    }

    private void DrawShadow(
        DrawingContext context, Geometry geometry, ShapeShadow shadow, Rect local)
    {
        var colour = _brushes.Colour(shadow.ColorHex, Colors.Black);
        var blur = _effects.Radius(shadow.Blur.ToPoints());

        var offset = Matrix.CreateTranslation(
            shadow.OffsetX.ToPoints(), shadow.OffsetY.ToPoints());

        using (context.PushTransform(offset))
        using (context.PushOpacity(_effects.Opacity(shadow.Opacity)))
        using (blur > 0
            ? context.PushEffect(new BlurEffect { Radius = blur }, local.Inflate(blur * 3))
            : context.PushOpacity(1))
        {
            context.DrawGeometry(new SolidColorBrush(colour), null, geometry);
        }
    }

    private DrawingContext.PushedState SoftEdge(
        DrawingContext context, Twip radius, Rect local) =>
        _effects.Radius(radius.ToPoints()) is var points && points > 0
            ? context.PushEffect(
                new BlurEffect { Radius = points }, local.Inflate(points * 2))
            : context.PushOpacity(1);

    private void DrawBevel(
        DrawingContext context, Geometry geometry, Rect bounds, ShapeBevel bevel)
    {
        var depth = Math.Max(1, bevel.Depth.ToPoints());

        var light = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new Avalonia.Media.GradientStop(Color.FromArgb(150, 255, 255, 255), 0),
                new Avalonia.Media.GradientStop(Color.FromArgb(0, 255, 255, 255), 0.45),
                new Avalonia.Media.GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55),
                new Avalonia.Media.GradientStop(Color.FromArgb(120, 0, 0, 0), 1),
            ],
        };

        using (context.PushGeometryClip(geometry))
            context.FillRectangle(light, new Rect(0, 0, bounds.Width, bounds.Height));

        var edge = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), depth / 2);

        using (context.PushGeometryClip(geometry))
            context.DrawGeometry(null, edge, geometry);
    }

    private static Matrix TransformOf(ShapeObject shape, Rect bounds)
    {
        var centre = bounds.Center;

        var transform = Matrix.CreateTranslation(bounds.X, bounds.Y);

        if (shape.FlipHorizontal || shape.FlipVertical)
            transform = Matrix.CreateTranslation(-bounds.Width / 2, -bounds.Height / 2)
                * Matrix.CreateScale(shape.FlipHorizontal ? -1 : 1, shape.FlipVertical ? -1 : 1)
                * Matrix.CreateTranslation(bounds.Width / 2, bounds.Height / 2)
                * transform;

        if (Math.Abs(shape.RotationDegrees) > 0.001)
            transform *= Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateRotation(shape.RotationDegrees * Math.PI / 180)
                * Matrix.CreateTranslation(centre.X, centre.Y);

        return transform;
    }

    private void DrawText(DrawingContext context, string content, IReadOnlyList<ShapeTextRun>? runs, ShapeStyle shapeStyle, Rect bounds, ShapeSurroundings backdrop, double shapeRotation)
    {
        var style = shapeStyle.Text;
        var rotation = style.RotationDegrees + shapeRotation;
        var inset = style.Inset.ToPoints();

        var typeface = new Typeface(
            new FontFamily(style.Family),
            style.Italic ? FontStyle.Italic : FontStyle.Normal,
            style.Bold ? FontWeight.Bold : FontWeight.Normal);

        var text = new FormattedText(
            content,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            Legible(style.Size) ?? ShapeTextStyle.Default.Size,
            new SolidColorBrush(_brushes.Colour(_ink.Resolve(shapeStyle, Behind(shapeStyle.Fill, backdrop).Hex), Colors.White)))
        {
            MaxTextWidth = style.Wrap ? Math.Max(1, bounds.Width - inset * 2) : double.PositiveInfinity,
            TextAlignment = style.Horizontal switch
            {
                TextHorizontalPlacement.Left => TextAlignment.Left,
                TextHorizontalPlacement.Right => TextAlignment.Right,
                TextHorizontalPlacement.Justify => TextAlignment.Justify,
                _ => TextAlignment.Center,
            },
        };

        if (style.Underline)
            text.SetTextDecorations(TextDecorations.Underline);
        if (runs is { Count: > 0 }) Format(text, runs, style);

        var y = style.Vertical switch
        {
            TextVerticalPlacement.Top => bounds.Y + inset,
            TextVerticalPlacement.Bottom => bounds.Bottom - inset - text.Height,
            _ => bounds.Y + (bounds.Height - text.Height) / 2,
        };

        var origin = new Point(bounds.X + inset, y);

        if (Math.Abs(rotation % 360) < 0.001)
        {
            context.DrawText(text, origin);
            return;
        }

        var centre = bounds.Center;

        using (context.PushTransform(
            Matrix.CreateTranslation(-centre.X, -centre.Y)
            * Matrix.CreateRotation(rotation * Math.PI / 180)
            * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            context.DrawText(text, origin);
        }
    }

    private ShapeSurroundings Behind(ShapeFill fill, ShapeSurroundings backdrop)
    {
        if (fill is not { Kind: ShapeFillKind.Bitmap, Bitmap: { } bitmap } || !fill.IsVisible || _brushes.Average(bitmap) is not { } average) return backdrop;
        var under = _brushes.Colour(backdrop.Hex, Colors.White);
        var opacity = Math.Clamp(fill.Opacity, 0, 1);
        byte Mix(byte over, byte below) => (byte)Math.Round(over * opacity + below * (1 - opacity));
        return new ShapeSurroundings(Color.FromRgb(Mix(average.R, under.R), Mix(average.G, under.G), Mix(average.B, under.B))) { Language = backdrop.Language };
    }

    private void Format(FormattedText text, IReadOnlyList<ShapeTextRun> runs, ShapeTextStyle style)
    {
        var start = 0;
        foreach (var run in runs)
        {
            var length = run.Text.Length;
            if (length > 0)
            {
                text.SetFontWeight(run.Bold || style.Bold ? FontWeight.Bold : FontWeight.Normal, start, length);
                text.SetFontStyle(run.Italic || style.Italic ? FontStyle.Italic : FontStyle.Normal, start, length);
                if (Legible(run.Size) is { } size) text.SetFontSize(size, start, length);
                if (run.Family is { Length: > 0 } family) text.SetFontFamily(new FontFamily(family), start, length);
                if (_links.ColorHex(run) is { } colour) text.SetForegroundBrush(new SolidColorBrush(_brushes.Colour(colour, Colors.Black)), start, length);
                if (_links.Underlined(run) && !style.Underline) text.SetTextDecorations(TextDecorations.Underline, start, length);
            }
            start += length;
        }
    }

    private double? Legible(double? size) =>
        size is { } points && double.IsFinite(points) && points > 0 ? Math.Clamp(points, MinimumFontSize, MaximumFontSize) : null;

    public static StreamGeometry Build(ShapeOutline outline) => new OutlineGeometry().Build(outline);

}
