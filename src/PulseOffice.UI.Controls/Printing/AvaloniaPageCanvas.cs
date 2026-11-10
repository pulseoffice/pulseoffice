using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using LineSegment = PulseOffice.Drawing.Shapes.LineSegment;
using TextAlignment = PulseOffice.Printing.TextAlignment;
using TextBaseline = PulseOffice.Printing.TextBaseline;

namespace PulseOffice.UI.Controls.Printing;

public interface IPageCanvasFactory
{
    IPageCanvas Create(DrawingContext context, DocumentSize pageSize, double scale);
}

public sealed class AvaloniaPageCanvasFactory(PulseOffice.UI.Controls.Fonts.IDisplayFonts fonts, IPageImageBitmaps bitmaps) : IPageCanvasFactory
{
    public AvaloniaPageCanvasFactory(PulseOffice.UI.Controls.Fonts.IDisplayFonts fonts)
        : this(fonts, new PageImageBitmaps())
    {
    }

    public IPageCanvas Create(DrawingContext context, DocumentSize pageSize, double scale) =>
        new AvaloniaPageCanvas(context, pageSize, scale, fonts, bitmaps);
}

public interface IPageImageBitmaps
{
    Avalonia.Media.Imaging.Bitmap? For(PageImage image);
}

public sealed class PageImageBitmaps : IPageImageBitmaps
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<PageImage, Avalonia.Media.Imaging.WriteableBitmap> _cache = new();

    public Avalonia.Media.Imaging.Bitmap? For(PageImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0 || image.Rgb.Length < (long)image.Width * image.Height * 3) return null;

        return _cache.GetValue(image, Build);
    }

    private static Avalonia.Media.Imaging.WriteableBitmap Build(PageImage image)
    {
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            new PixelSize(image.Width, image.Height), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

        var row = new byte[image.Width * 4];
        using var locked = bitmap.Lock();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var i = y * image.Width + x;
                var s = i * 3;
                var d = x * 4;
                var alpha = image.Alpha is { } mask && i < mask.Length ? mask[i] : (byte)255;
                row[d] = (byte)(image.Rgb[s + 2] * alpha / 255);
                row[d + 1] = (byte)(image.Rgb[s + 1] * alpha / 255);
                row[d + 2] = (byte)(image.Rgb[s] * alpha / 255);
                row[d + 3] = alpha;
            }

            System.Runtime.InteropServices.Marshal.Copy(row, 0, locked.Address + y * locked.RowBytes, row.Length);
        }

        return bitmap;
    }
}

public sealed class AvaloniaPageCanvas(
    DrawingContext context, DocumentSize pageSize, double scale, PulseOffice.UI.Controls.Fonts.IDisplayFonts fonts, IPageImageBitmaps bitmaps,
    ITextDecorationPainter? decorations = null)
    : IPageCanvas
{
    private readonly ITextDecorationPainter _decorations = decorations ?? new TextDecorationPainter();

    private readonly PulseOffice.UI.Controls.Shapes.OutlineGeometry _geometry = new();

    public DocumentSize PageSize => pageSize;

    public Twip MeasureText(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        return Twip.FromPoints(Format(text, font, Brushes.Black).Width / scale);
    }

    public void FillRectangle(DocumentRect rect, PrintColor color) =>
        context.FillRectangle(Brush(color), Area(rect));

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) =>
        context.DrawLine(
            new Pen(Brush(color), Math.Max(0.4, thickness.ToPoints() * scale)),
            Spot(from),
            Spot(to));

    public void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
    {
        ArgumentNullException.ThrowIfNull(font);

        if (string.IsNullOrEmpty(text)) return;

        var formatted = Format(text, font, Brush(color));
        var area = Area(bounds);

        var x = alignment switch
        {
            TextAlignment.Right => area.Right - formatted.Width,
            TextAlignment.Center => area.X + (area.Width - formatted.Width) / 2,
            _ => area.X,
        };

        var y = baseline switch
        {
            TextBaseline.Top => area.Y,
            TextBaseline.Bottom => area.Bottom - formatted.Height,
            _ => area.Y + (area.Height - formatted.Height) / 2,
        };

        using var clip = context.PushClip(area);
        context.DrawText(formatted, new Point(x, y));
        _decorations.Paint(this, new PrintTextSpan(x / scale, (y + formatted.Baseline) / scale, formatted.Width / scale, font, color));
    }

    public void DrawPath(PrintPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var area = Area(path.Bounds);
        if (path.Shadow is { } shadow)
            DrawPath(path with
            {
                Bounds = new DocumentRect(path.Bounds.Left + path.ShadowOffsetX, path.Bounds.Top + path.ShadowOffsetY, path.Bounds.Width, path.Bounds.Height),
                Fill = shadow, Gradient = null, Stroke = null, Shadow = null, FillOpacity = path.ShadowOpacity,
            });

        var filled = path.Fill is not null || path.Gradient is not null;
        var geometry = _geometry.Build(path.Outline, point => Scaled(point, area), filled);

        using var placement = context.PushTransform(Placement(path, area));
        using var opacity = path.FillOpacity < 1 ? context.PushOpacity(Math.Clamp(path.FillOpacity, 0, 1)) : default;

        if (path.Fill is { } fill) context.DrawGeometry(Brush(fill), null, geometry);
        else if (path.Gradient is { } gradient) context.DrawGeometry(Gradient(gradient), null, geometry);

        if (path.Stroke is { } stroke)
            context.DrawGeometry(
                null,
                new Pen(Brush(stroke), Math.Max(0.4, path.StrokeWidth.ToPoints() * scale)),
                geometry);
    }

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Text.Length == 0 || run.Font.SizePoints <= 0) return;

        DrawGlyphDecorations(run);
        var origin = new Point(run.X * scale, run.Baseline * scale);
        var turn = Matrix.CreateTranslation(-origin.X, -origin.Y)
            * Matrix.CreateScale(run.HorizontalScale > 0 ? run.HorizontalScale : 1, 1)
            * Matrix.CreateRotation(-run.RotationDegrees * Math.PI / 180)
            * Matrix.CreateTranslation(origin.X, origin.Y);
        using var transform = context.PushTransform(turn);

        var brush = Brush(run.Color);
        var x = origin.X;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(run.Text);
        while (elements.MoveNext())
        {
            var glyph = (string)elements.Current;
            var advance = run.Advance(elements.ElementIndex, glyph.Length) * scale / (run.HorizontalScale > 0 ? run.HorizontalScale : 1);
            if (!string.IsNullOrWhiteSpace(glyph) && !char.IsControl(glyph[0]) && glyph != "\u00AD")
            {
                var formatted = Format(glyph, run.Font, brush);
                context.DrawText(formatted, new Point(x, origin.Y - formatted.Baseline));
            }
            x += advance;
        }
    }

    private void DrawGlyphDecorations(PrintGlyphRun run) =>
        _decorations.Paint(this, new PrintTextSpan(run.X, run.Baseline, run.Width, run.Font, run.Color, run.RotationDegrees));

    private Matrix Placement(PrintPath path, Rect area)
    {
        var centre = area.Center;
        var flip = Matrix.CreateScale(path.FlipHorizontal ? -1 : 1, path.FlipVertical ? -1 : 1);
        var rotation = Matrix.CreateRotation(path.RotationDegrees * Math.PI / 180);
        return Matrix.CreateTranslation(-centre.X, -centre.Y) * flip * rotation * Matrix.CreateTranslation(centre.X, centre.Y);
    }

    private IBrush Gradient(PrintGradient gradient)
    {
        var stops = new GradientStops
        {
            new Avalonia.Media.GradientStop(Color.FromRgb(gradient.From.R, gradient.From.G, gradient.From.B), 0),
            new Avalonia.Media.GradientStop(Color.FromRgb(gradient.To.R, gradient.To.G, gradient.To.B), 1),
        };
        if (gradient.Radial) return new RadialGradientBrush { GradientStops = stops };

        var radians = gradient.AngleDegrees * Math.PI / 180;
        var (dx, dy) = (Math.Cos(radians) / 2, Math.Sin(radians) / 2);
        return new LinearGradientBrush
        {
            GradientStops = stops,
            StartPoint = new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative),
        };
    }

    public void DrawImage(PageImage image, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (bitmaps.For(image) is { } bitmap) context.DrawImage(bitmap, Area(bounds));
    }

    public IDisposable Clip(DocumentRect area)
    {
        var state = context.PushClip(Area(area));
        return new PageClipScope(state.Dispose);
    }

    private static Point Scaled(ShapePoint point, Rect area) =>
        new(area.X + point.X * area.Width, area.Y + point.Y * area.Height);

    private FormattedText Format(string text, PrintFont font, IBrush brush) =>
        new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                fonts.Resolve(font.Family),
                font.Italic ? FontStyle.Italic : FontStyle.Normal,
                font.Bold ? FontWeight.Bold : FontWeight.Normal),
            font.SizePoints * scale,
            brush);

    private Rect Area(DocumentRect rect) =>
        new(
            rect.Left.ToPoints() * scale,
            rect.Top.ToPoints() * scale,
            Math.Max(0, rect.Width.ToPoints() * scale),
            Math.Max(0, rect.Height.ToPoints() * scale));

    private Point Spot(DocumentPoint point) =>
        new(point.X.ToPoints() * scale, point.Y.ToPoints() * scale);

    private static IBrush Brush(PrintColor color) =>
        new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
}
