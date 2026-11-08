using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using AvaloniaDrawing = Avalonia.Media.Drawing;

namespace PulseOffice.UI.Controls.Printing;

public interface IDrawingReplay
{
    DrawingParts Split(AvaloniaDrawing drawing);
}

public sealed class DrawingParts(IReadOnlyList<Action<IPageCanvas>> vector, AvaloniaDrawing? rest)
{
    public AvaloniaDrawing? Rest { get; } = rest;

    public bool IsEmpty => vector.Count == 0 && Rest is null;

    public void PaintVector(IPageCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        foreach (var paint in vector) paint(canvas);
    }
}

public sealed class DrawingReplay(IPageImageCapture images) : IDrawingReplay
{
    public DrawingReplay() : this(new PageImageCapture())
    {
    }

    public DrawingParts Split(AvaloniaDrawing drawing)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        var vector = new List<Action<IPageCanvas>>();
        var rest = Strip(drawing, Matrix.Identity, 1, null, vector, extractImages: false);
        if (rest is not null && OnlyImages(rest))
        {
            vector.Clear();
            rest = Strip(drawing, Matrix.Identity, 1, null, vector, extractImages: true);
        }
        return new DrawingParts(vector, rest);
    }

    private bool OnlyImages(AvaloniaDrawing drawing) => drawing switch
    {
        DrawingGroup group => group.OpacityMask is null && group.Effect is null && group.Children.All(OnlyImages),
        ImageDrawing { ImageSource: Bitmap } => true,
        _ => false,
    };

    private AvaloniaDrawing? Strip(AvaloniaDrawing drawing, Matrix transform, double opacity, Rect? clip, List<Action<IPageCanvas>> vector, bool extractImages)
    {
        switch (drawing)
        {
            case DrawingGroup group:
            {
                var inner = group.Transform is { } local ? local.Value * transform : transform;
                var innerClip = clip;
                if (group.ClipGeometry is { } geometry)
                {
                    var area = geometry.Bounds.TransformToAABB(inner);
                    innerClip = clip is { } outer ? outer.Intersect(area) : area;
                }
                var kept = new List<AvaloniaDrawing>();
                foreach (var child in group.Children)
                    if (Strip(child, inner, opacity * group.Opacity, innerClip, vector, extractImages && group.OpacityMask is null && group.Effect is null) is { } remaining)
                        kept.Add(remaining);
                if (kept.Count == 0) return null;
                return new DrawingGroup
                {
                    Transform = group.Transform,
                    Opacity = group.Opacity,
                    ClipGeometry = group.ClipGeometry,
                    OpacityMask = group.OpacityMask,
                    Effect = group.Effect,
                    Children = new DrawingCollection(kept),
                };
            }
            case GlyphRunDrawing { GlyphRun: { } run } text when text.Foreground is null or ISolidColorBrush:
            {
                var brush = text.Foreground as ISolidColorBrush;
                vector.Add(canvas => PaintGlyphs(Clipped(canvas, clip), run, brush, transform, opacity));
                return null;
            }
            case ImageDrawing { ImageSource: Bitmap bitmap } picture when extractImages && Upright(transform):
            {
                var area = picture.Rect.TransformToAABB(transform);
                vector.Add(canvas => PaintImage(Clipped(canvas, clip), bitmap, area, opacity));
                return null;
            }
            default:
                return drawing;
        }
    }

    private IPageCanvas Clipped(IPageCanvas canvas, Rect? clip) => clip is { } area ? new ClippedPageCanvas(canvas, Twips(area)) : canvas;

    private bool Upright(Matrix transform) => Math.Abs(transform.M12) < 1e-9 && Math.Abs(transform.M21) < 1e-9 && transform.M11 > 0 && transform.M22 > 0;

    private PrintColor Colour(Color colour) => new(colour.R, colour.G, colour.B);

    private void PaintGlyphs(IPageCanvas canvas, GlyphRun run, ISolidColorBrush? brush, Matrix transform, double opacity)
    {
        var colour = brush?.Color ?? Colors.Black;
        if (colour.A * (brush?.Opacity ?? 1) * opacity < 8) return;

        var text = run.Characters.ToString();
        if (text.Length == 0 || run.GlyphInfos.Count == 0) return;

        var horizontal = Math.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
        var vertical = Math.Sqrt(transform.M21 * transform.M21 + transform.M22 * transform.M22);
        var first = run.GlyphInfos.Min(glyph => glyph.GlyphCluster);
        var advances = new double[text.Length];
        foreach (var glyph in run.GlyphInfos)
        {
            var index = Math.Clamp(glyph.GlyphCluster - first, 0, text.Length - 1);
            advances[index] += glyph.GlyphAdvance * horizontal;
        }

        var origin = run.BaselineOrigin.Transform(transform);
        var typeface = run.GlyphTypeface;
        var font = new PrintFont(typeface.FamilyName, run.FontRenderingEmSize * vertical,
            typeface.Weight >= FontWeight.SemiBold || typeface.FontSimulations.HasFlag(FontSimulations.Bold),
            typeface.Style is not FontStyle.Normal || typeface.FontSimulations.HasFlag(FontSimulations.Oblique));

        canvas.DrawGlyphs(new PrintGlyphRun(text, origin.X, origin.Y, font, Colour(colour), advances)
        {
            HorizontalScale = vertical > 0 ? horizontal / vertical : 1,
            RotationDegrees = -Math.Atan2(transform.M12, transform.M11) * 180 / Math.PI,
        });
    }

    private void PaintImage(IPageCanvas canvas, Bitmap bitmap, Rect area, double opacity)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        if (images.Capture(bitmap, opacity) is { } image) canvas.DrawImage(image, Twips(area));
    }

    private DocumentRect Twips(Rect rect) => new(
        Twip.FromPoints(rect.X), Twip.FromPoints(rect.Y), Twip.FromPoints(Math.Max(0, rect.Width)), Twip.FromPoints(Math.Max(0, rect.Height)));
}

public interface IPageImageCapture
{
    PageImage? Capture(IImage image, double opacity = 1, int maxSide = 2400);

    PageImage FromBitmap(Bitmap bitmap);
}

public sealed class PageImageCapture(PulseOffice.UI.Controls.Objects.IBitmapPixels pixels) : IPageImageCapture
{
    public PageImageCapture() : this(new PulseOffice.UI.Controls.Objects.BitmapPixels())
    {
    }

    public PageImage? Capture(IImage image, double opacity = 1, int maxSide = 2400)
    {
        ArgumentNullException.ThrowIfNull(image);
        var size = image is Bitmap bitmap ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : image.Size;
        if (size.Width < 1 || size.Height < 1) return null;

        var factor = Math.Min(1, maxSide / Math.Max(size.Width, size.Height));
        var width = Math.Max(1, (int)Math.Round(size.Width * factor));
        var height = Math.Max(1, (int)Math.Round(size.Height * factor));

        using var target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
            context.DrawImage(image, new Rect(0, 0, width, height));

        return FromBitmap(target);
    }

    public PageImage FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var image = pixels.Read(bitmap);
        var (width, height, bgra) = (image.Width, image.Height, image.Pixels);

        var rgb = new byte[width * height * 3];
        var alpha = new byte[width * height];
        var opaque = true;
        for (var pixel = 0; pixel < width * height; pixel++)
        {
            var a = bgra[pixel * 4 + 3];
            alpha[pixel] = a;
            opaque &= a == 255;
            for (var channel = 0; channel < 3; channel++)
            {
                var premultiplied = bgra[pixel * 4 + 2 - channel];
                rgb[pixel * 3 + channel] = a == 0 ? (byte)255 : (byte)Math.Min(255, premultiplied * 255 / a);
            }
        }

        return new PageImage(width, height, rgb) { Alpha = opaque ? null : alpha };
    }
}
