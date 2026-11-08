using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using LineSegment = PulseOffice.Drawing.Shapes.LineSegment;

namespace PulseOffice.UI.Controls.Shapes.Painting;

public abstract class RotatingPainter
{
    protected Matrix RotationAround(Point centre, double degrees) =>
        Math.Abs(degrees) < 0.001
            ? Matrix.Identity
            : Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateRotation(degrees * Math.PI / 180)
                * Matrix.CreateTranslation(centre.X, centre.Y);
}

public sealed class FreeformPainter(IShapeRenderer shapes) : RotatingPainter
{
    public void Paint(DrawingContext context, FreeformObject freeform, Rect bounds) =>
        shapes.Render(
            context,
            freeform.Path.ToOutline(),
            freeform.Style,
            Matrix.CreateTranslation(bounds.X, bounds.Y) * RotationAround(bounds.Center, freeform.RotationDegrees),
            null,
            bounds);
}

public sealed class VectorPainter(IVectorOutlineReader vectors) : RotatingPainter
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, VectorDrawing> _drawings = new();

    public void Paint(DrawingContext context, VectorGraphicObject vector, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var drawing = _drawings.GetValue(vector.Asset.SvgContent ?? string.Empty, vectors.Read).Recoloured(vector.RecolorHex);
        if (drawing.IsEmpty) return;

        var side = Math.Min(bounds.Width, bounds.Height);
        using (context.PushTransform(Placement(vector, bounds, side)))
            foreach (var path in drawing.Scaled(side, side).Paths)
                context.DrawGeometry(Brush(path.Paint.FillHex), Stroke(path.Paint, side), ShapeRenderer.Build(path.Outline));
    }

    private Matrix Placement(VectorGraphicObject vector, Rect bounds, double side)
    {
        var centre = bounds.Center;
        var transform = Matrix.CreateTranslation(bounds.X + (bounds.Width - side) / 2, bounds.Y + (bounds.Height - side) / 2);

        if (vector.FlipHorizontal || vector.FlipVertical)
            transform = Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateScale(vector.FlipHorizontal ? -1 : 1, vector.FlipVertical ? -1 : 1)
                * Matrix.CreateTranslation(centre.X, centre.Y)
                * transform;

        return transform * RotationAround(centre, vector.RotationDegrees);
    }

    private IBrush? Brush(string? hex) =>
        hex is not null && Color.TryParse(hex, out var colour) ? new SolidColorBrush(colour) : null;

    private Pen? Stroke(VectorPaint paint, double side) =>
        Brush(paint.StrokeHex) is { } brush
            ? new Pen(brush, Math.Max(0.5, paint.StrokeWidth * side)) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }
            : null;
}

public sealed class RasterPainter(IPixelFilterCatalog filters, PulseOffice.UI.Controls.Objects.IBitmapPixels pixels, PulseOffice.Drawing.Assets.Gif.IGifCodec gifs)
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<(ImageFilter, ImageDuotone?), Avalonia.Media.Imaging.Bitmap>> _bitmaps = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<(ImageFilter, ImageDuotone?), AnimatedFrames>> _animations = new();
    private readonly System.Threading.Lock _gate = new();

    public bool IsAnimated(RasterImageObject raster) => AnimationOf(raster) is { Frames.Count: > 1 };

    public void Paint(DrawingContext context, RasterImageObject raster, Rect bounds)
    {
        if (raster.Data.IsEmpty && raster.LinkPath is { Length: > 0 } link)
        {
            PaintUnloadedLink(context, link, bounds);
            return;
        }
        if ((AnimationOf(raster) is { Frames.Count: > 1 } animated ? animated.At(Environment.TickCount64) : BitmapOf(raster)) is not { } bitmap) return;

        var (left, top, right, bottom) = raster.Crop;
        left = Math.Clamp(double.IsFinite(left) ? left : 0, 0, 0.99);
        top = Math.Clamp(double.IsFinite(top) ? top : 0, 0, 0.99);
        right = Math.Clamp(double.IsFinite(right) ? right : 0, 0, 0.99 - left);
        bottom = Math.Clamp(double.IsFinite(bottom) ? bottom : 0, 0, 0.99 - top);
        var source = left <= 0 && top <= 0 && right <= 0 && bottom <= 0
            ? new Rect(bitmap.Size)
            : new Rect(
                bitmap.Size.Width * left, bitmap.Size.Height * top,
                Math.Max(1, bitmap.Size.Width * (1 - left - right)),
                Math.Max(1, bitmap.Size.Height * (1 - top - bottom)));

        context.DrawImage(bitmap, source, bounds);
    }

    private void PaintUnloadedLink(DrawingContext context, string link, Rect bounds)
    {
        var ink = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        context.DrawRectangle(null, new Pen(ink, 1, new DashStyle([4, 3], 0)), bounds);
        var label = new FormattedText(System.IO.Path.GetFileName(link), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, Math.Clamp(bounds.Height / 12, 8, 14), ink)
        {
            MaxTextWidth = Math.Max(1, bounds.Width - 8),
            MaxLineCount = 2,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        context.DrawText(label, new Point(bounds.X + 4, bounds.Y + 4));
    }

    private Avalonia.Media.Imaging.Bitmap? BitmapOf(RasterImageObject raster)
    {
        var identity = PulseOffice.UI.Controls.Objects.RasterIdentity.Of(raster);

        lock (_gate)
        {
            var variants = _bitmaps.GetValue(identity, _ => []);
            if (variants.TryGetValue((raster.Filter, raster.Duotone), out var cached)) return cached;

            if (pixels.Decode(raster.Data.ToArray()) is not { } bitmap) return null;
            var result = Apply(bitmap, raster.Filter, raster.Duotone);

            foreach (var stale in variants.Where(entry => entry.Key != (ImageFilter.None, null)).ToList())
            {
                variants.Remove(stale.Key);
                stale.Value.Dispose();
            }

            variants[(raster.Filter, raster.Duotone)] = result;
            return result;
        }
    }

    private AnimatedFrames? AnimationOf(RasterImageObject raster)
    {
        if (raster.MediaType != "image/gif") return null;

        var identity = PulseOffice.UI.Controls.Objects.RasterIdentity.Of(raster);

        lock (_gate)
        {
            var variants = _animations.GetValue(identity, _ => []);
            if (variants.TryGetValue((raster.Filter, raster.Duotone), out var cached)) return cached;

            var animation = gifs.Decode(raster.Data.Span);
            var frames = new AnimatedFrames(animation is null
                ? []
                : [.. animation.Frames.Select(frame => (Apply((Avalonia.Media.Imaging.Bitmap)pixels.Create(frame.Image), raster.Filter, raster.Duotone), Math.Max(20, frame.DelayMilliseconds)))]);

            foreach (var stale in variants.Where(entry => entry.Key != (ImageFilter.None, null)).ToList())
            {
                variants.Remove(stale.Key);
                stale.Value.Dispose();
            }

            variants[(raster.Filter, raster.Duotone)] = frames;
            return frames;
        }
    }

    private Avalonia.Media.Imaging.Bitmap Apply(Avalonia.Media.Imaging.Bitmap bitmap, ImageFilter filter, ImageDuotone? duotone)
    {
        if (filter == ImageFilter.None && duotone is null) return bitmap;

        var filtered = pixels.Edit(bitmap, image =>
        {
            if (filter != ImageFilter.None) filters.For(filter).Apply(image);
            if (duotone is not null) filters.Duotone(duotone).Apply(image);
        });
        if (!ReferenceEquals(filtered, bitmap)) bitmap.Dispose();
        return filtered;
    }

    private sealed class AnimatedFrames(IReadOnlyList<(Avalonia.Media.Imaging.Bitmap Bitmap, int Delay)> frames) : IDisposable
    {
        private readonly int _duration = frames.Sum(frame => frame.Delay);

        public IReadOnlyList<(Avalonia.Media.Imaging.Bitmap Bitmap, int Delay)> Frames => frames;

        public Avalonia.Media.Imaging.Bitmap? At(long milliseconds)
        {
            if (frames.Count == 0) return null;
            var time = _duration <= 0 ? 0 : milliseconds % _duration;
            foreach (var (bitmap, delay) in frames)
            {
                time -= delay;
                if (time < 0) return bitmap;
            }
            return frames[^1].Bitmap;
        }

        public void Dispose()
        {
            foreach (var (bitmap, _) in frames) bitmap.Dispose();
        }
    }
}

public sealed class ConnectorPainter(IShapeBrushes brushes, IArrowGeometry arrows)
{
    public void Paint(DrawingContext context, ConnectorObject connector, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || connector.Path.Figures.Count == 0) return;
        if (brushes.Stroke(connector.Style.Line) is not { } pen) return;

        var scaled = connector.Path.Scaled(bounds.Width, bounds.Height);
        var stroke = pen.Brush ?? Brushes.Black;
        var width = Math.Max(1, connector.Style.Line.Width.ToPoints());

        using (context.PushTransform(Matrix.CreateTranslation(bounds.X, bounds.Y)))
        {
            context.DrawGeometry(null, pen, ShapeRenderer.Build(scaled));
            foreach (var head in Heads(connector, scaled, width))
                context.DrawGeometry(stroke, new Pen(stroke, 1), ShapeRenderer.Build(head));
        }
    }

    private IEnumerable<ShapeOutline> Heads(ConnectorObject connector, ShapeOutline path, double width)
    {
        var points = new SegmentEnds().Of(path).ToList();

        if (points.Count < 2) yield break;

        var size = Math.Max(6, width * 4);
        if (connector.StartArrow is not ArrowEnd.None) yield return arrows.Head(connector.StartArrow, points[0], points[1], size);
        if (connector.EndArrow is not ArrowEnd.None) yield return arrows.Head(connector.EndArrow, points[^1], points[^2], size);
    }
}

public sealed class GroupPainter : RotatingPainter
{
    public void Paint(DrawingContext context, ShapeGroupObject group, Rect bounds, IDrawingObjectRenderer children, ShapeSurroundings backdrop)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using (context.PushTransform(RotationAround(bounds.Center, group.RotationDegrees)))
            foreach (var child in group.Children)
            {
                var (x, y, width, height) = child.Scaled(bounds.Width, bounds.Height);
                var area = new Rect(bounds.X + x, bounds.Y + y, width, height);
                using (context.PushTransform(RotationAround(area.Center, child.RotationDegrees)))
                    children.Render(context, child.Content, area, backdrop);
            }
    }
}
