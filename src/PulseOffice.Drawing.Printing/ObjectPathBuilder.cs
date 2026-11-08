using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Drawing.Printing;

public interface IObjectPathBuilder
{
    IEnumerable<PrintPath> Build(EmbeddedObject content, DocumentRect bounds);

    void Paint(IPageCanvas canvas, EmbeddedObject content, DocumentRect bounds);

    bool IsExact(EmbeddedObject content);
}

public sealed class ObjectPathBuilder : IObjectPathBuilder
{
    private readonly IShapeGeometry _geometry;
    private readonly IVectorOutlineReader _vectors;
    private readonly IArrowGeometry _arrows;
    private readonly IReadOnlyDictionary<ShapeShade, (PrintColor Colour, double Opacity)> _veils = new Dictionary<ShapeShade, (PrintColor Colour, double Opacity)>
    {
        [ShapeShade.Darken] = (PrintColor.Black, 0.4),
        [ShapeShade.DarkenLess] = (PrintColor.Black, 0.2),
        [ShapeShade.Lighten] = (PrintColor.White, 0.4),
        [ShapeShade.LightenLess] = (PrintColor.White, 0.2),
    };
    private readonly IShapeTextInk _ink;
    private readonly IShapeTextLists _lists;

    public ObjectPathBuilder(IShapeGeometry geometry, IVectorOutlineReader vectors, IArrowGeometry? arrows = null, IShapeTextInk? ink = null, IShapeTextLists? lists = null)
    {
        _arrows = arrows ?? new ArrowGeometry();
        _ink = ink ?? new ShapeTextInk();
        _lists = lists ?? new ShapeTextLists();
        _geometry = geometry;
        _vectors = vectors;
    }

    public ObjectPathBuilder() : this(new ShapeGeometry(), new VectorOutlineReader()) { }

    public bool IsExact(EmbeddedObject content) => content switch
    {
        ShapeGroupObject group => group.Children.All(child => IsExact(child.Content)),
        ConnectorObject connector => Exact(connector.Style),
        FreeformObject freeform => Exact(freeform.Style),
        ShapeObject shape => shape.Dimension is null && !_lists.Listed(shape) && Exact(shape.Style) && shape.Style.Line.StartArrow is ArrowEnd.None && shape.Style.Line.EndArrow is ArrowEnd.None,
        VectorGraphicObject vector => vector.Asset.SvgContent is { Length: > 0 },
        _ => false,
    };

    private bool Exact(ShapeStyle style) =>
        (style.Fill.Kind is ShapeFillKind.None or ShapeFillKind.Solid
            || (style.Fill.Kind is ShapeFillKind.Gradient && style.Fill.Gradient.Stops.Count <= 2 && style.Fill.Gradient.Border == 0))
        && style.Line.Dash is ShapeStrokeStyle.Solid or ShapeStrokeStyle.None
        && (!style.Line.IsVisible || style.Line.Opacity >= 0.999)
        && !style.Shadow.Enabled && !style.Glow.Enabled
        && style.Bevel.Style is BevelStyle.None && style.Extrusion.Kind is ExtrusionKind.None
        && style.SoftEdge.Value == 0;

    public void Paint(IPageCanvas canvas, EmbeddedObject content, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(content);

        foreach (var path in Build(content, bounds)) canvas.DrawPath(path);

        if (content is ShapeGroupObject group)
        {
            foreach (var child in group.Children)
            {
                var (x, y, width, height) = child.Scaled(bounds.Width.Value, bounds.Height.Value);

                if (child.Content is not ShapeObject { Text.Length: > 0 }) continue;

                PaintText(canvas, (ShapeObject)child.Content, new DocumentRect(
                    new Twip(bounds.Left.Value + (int)Math.Round(x)),
                    new Twip(bounds.Top.Value + (int)Math.Round(y)),
                    new Twip(Math.Max(1, (int)Math.Round(width))),
                    new Twip(Math.Max(1, (int)Math.Round(height)))));
            }

            return;
        }

        if (content is not ShapeObject { Text: { Length: > 0 } text } shape) return;

        PaintText(canvas, shape, bounds);
    }

    private IEnumerable<PrintPath> ArrowHeads(ConnectorObject connector, DocumentRect bounds)
    {
        var scaled = connector.Path.Scaled(bounds.Width.Value, bounds.Height.Value);

        var points = new SegmentEnds().Of(scaled).ToList();

        if (points.Count < 2) yield break;

        var scale = connector.Style.Line.ArrowScale is var given && double.IsFinite(given) && given > 0 ? given : 1;
        var size = Math.Max(connector.Style.Line.Width.Value * 4, Twip.FromPoints(6).Value) * scale;
        var colour = PrintColor.FromHex(connector.Style.Line.ColorHex);

        if (connector.StartArrow is not ArrowEnd.None)
            yield return Head(_arrows.Head(connector.StartArrow, points[0], points[1], size), bounds, colour);

        if (connector.EndArrow is not ArrowEnd.None)
            yield return Head(_arrows.Head(connector.EndArrow, points[^1], points[^2], size), bounds, colour);
    }

    private static PrintPath Head(ShapeOutline outline, DocumentRect bounds, PrintColor colour)
    {
        var normalized = new SegmentMapper(point => Fraction(point, bounds)).Map(outline);

        return new PrintPath(normalized, bounds) { Fill = colour, Stroke = colour };
    }

    private static ShapePoint Fraction(ShapePoint point, DocumentRect bounds) =>
        new(point.X / bounds.Width.Value, point.Y / bounds.Height.Value);

    private static PrintPath Painted(PrintPath path, ShapeStyle style) => path with
    {
        Fill = style.Fill.Kind is ShapeFillKind.Solid or ShapeFillKind.Hatch && style.Fill.IsVisible
            ? PrintColor.FromHex(style.Fill.ColorHex)
            : null,
        Gradient = style.Fill.Kind is ShapeFillKind.Gradient && style.Fill.IsVisible
            ? new PrintGradient(
                PrintColor.FromHex(style.Fill.Gradient.StartHex),
                PrintColor.FromHex(style.Fill.Gradient.EndHex),
                style.Fill.Gradient.AngleDegrees,
                style.Fill.Gradient.Kind is GradientKind.Radial or GradientKind.Elliptical)
            : null,
        FillOpacity = style.Fill.Opacity,
        Stroke = style.Line.IsVisible ? PrintColor.FromHex(style.Line.ColorHex) : null,
        StrokeWidth = style.Line.Width,
        Shadow = style.Shadow.Enabled ? PrintColor.FromHex(style.Shadow.ColorHex) : null,
        ShadowOffsetX = style.Shadow.OffsetX,
        ShadowOffsetY = style.Shadow.OffsetY,
        ShadowOpacity = style.Shadow.Opacity,
    };

    private IEnumerable<PrintPath> Figures(ShapeObject shape, ShapeOutline outline, DocumentRect bounds)
    {
        var painted = Painted(new PrintPath(outline, bounds), shape.Style);
        if (outline.Figures.All(figure => figure.Filled && figure.Stroked && figure.Shade is ShapeShade.Normal))
        {
            yield return painted;
            yield break;
        }

        var shadow = painted.Shadow;
        foreach (var shaded in outline.Figures.Where(figure => figure.Filled).GroupBy(figure => figure.Shade))
        {
            var area = new ShapeOutline([.. shaded]);
            yield return painted with { Outline = area, Stroke = null, Shadow = shadow };
            shadow = null;
            if ((painted.Fill is not null || painted.Gradient is not null) && _veils.TryGetValue(shaded.Key, out var veil))
                yield return new PrintPath(area, bounds) { Fill = veil.Colour, FillOpacity = veil.Opacity * painted.FillOpacity };
        }

        var stroked = outline.Figures.Where(figure => figure.Stroked).ToList();
        if (stroked.Count > 0 && painted.Stroke is not null)
            yield return painted with { Outline = new ShapeOutline(stroked), Fill = null, Gradient = null, Shadow = shadow };
    }

    private void PaintText(IPageCanvas canvas, ShapeObject shape, DocumentRect bounds) =>
        canvas.DrawText(
            shape.Text!, bounds,
            new PrintFont(shape.Style.Text.Family, shape.Style.Text.Size, shape.Style.Text.Bold, shape.Style.Text.Italic),
            PrintColor.FromHex(_ink.Resolve(shape.Style, ShapeTextInk.PaperHex)),
            TextAlignment.Center);

    public IEnumerable<PrintPath> Build(EmbeddedObject content, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(content);

        switch (content)
        {
            case ShapeGroupObject group:
            {
                foreach (var child in group.Children)
                {
                    var (x, y, width, height) = child.Scaled(
                        bounds.Width.Value, bounds.Height.Value);

                    var childBounds = new DocumentRect(
                        new Twip(bounds.Left.Value + (int)Math.Round(x)),
                        new Twip(bounds.Top.Value + (int)Math.Round(y)),
                        new Twip(Math.Max(1, (int)Math.Round(width))),
                        new Twip(Math.Max(1, (int)Math.Round(height))));

                    foreach (var path in Build(child.Content, childBounds))
                        yield return group.RotationDegrees == 0
                            ? path
                            : path with { RotationDegrees = path.RotationDegrees + group.RotationDegrees };
                }

                break;
            }

            case ConnectorObject connector when connector.Path.Figures.Count > 0:
            {
                yield return new PrintPath(connector.Path, bounds)
                {
                    Fill = null,
                    Stroke = PrintColor.FromHex(connector.Style.Line.ColorHex),
                    StrokeWidth = connector.Style.Line.Width,
                };

                foreach (var head in ArrowHeads(connector, bounds))
                    yield return head;

                break;
            }

            case FreeformObject freeform:
            {
                yield return Painted(new PrintPath(freeform.Path.ToOutline(), bounds), freeform.Style) with
                {
                    RotationDegrees = freeform.RotationDegrees,
                };

                break;
            }

            case ShapeObject shape:
            {
                foreach (var path in Figures(shape, _geometry.Outline(shape, bounds.Width.Value, bounds.Height.Value), bounds))
                    yield return path with
                    {
                        RotationDegrees = shape.RotationDegrees,
                        FlipHorizontal = shape.FlipHorizontal,
                        FlipVertical = shape.FlipVertical,
                    };

                break;
            }

            case VectorGraphicObject vector when vector.Asset.SvgContent is { Length: > 0 } svg:
            {
                var drawing = _vectors.Read(svg).Recoloured(vector.RecolorHex);
                if (drawing.IsEmpty) break;

                var side = new Twip(Math.Min(bounds.Width.Value, bounds.Height.Value));

                var square = new DocumentRect(
                    new Twip(bounds.Left.Value + (bounds.Width.Value - side.Value) / 2),
                    new Twip(bounds.Top.Value + (bounds.Height.Value - side.Value) / 2),
                    side, side);

                foreach (var path in drawing.Paths)
                    yield return new PrintPath(path.Outline, square)
                    {
                        Fill = path.Paint.FillHex is { } paintFill ? PrintColor.FromHex(paintFill) : null,
                        Stroke = path.Paint.StrokeHex is { } paintStroke ? PrintColor.FromHex(paintStroke) : null,
                        StrokeWidth = new Twip((int)Math.Max(1, path.Paint.StrokeWidth * side.Value)),
                        RotationDegrees = vector.RotationDegrees,
                        FlipHorizontal = vector.FlipHorizontal,
                        FlipVertical = vector.FlipVertical,
                    };

                break;
            }

        }
    }
}
