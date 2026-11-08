using Avalonia;
using Avalonia.Media;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;

namespace PulseOffice.Documents.UI.Rendering;

public enum PageStroke
{
    Solid,
    Dashed,
    Dotted,
}

public interface IPageSurface
{
    void Fill(LayoutRect rect, uint argb);

    void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid);

    void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid);

    void Glyphs(LayoutGlyphRun run, double x, double baseline, uint argb);

    void RotatedGlyphs(LayoutGlyphRun run, double x, double baseline, uint argb, double degrees);

    void Embedded(PulseOffice.Document.Embedding.EmbeddedObject content, LayoutRect bounds, ObjectTransform transform);

    void Backdrop(uint argb)
    {
    }

    void BeginObject(LayoutRect bounds, double rotationDegrees)
    {
    }

    void EndObject()
    {
    }
}

public interface IPageSurfaceFactory
{
    IPageSurface Screen(DrawingContext context, string? language = null);

    IPageSurface Print(IPageCanvas canvas, string? language = null);
}

public sealed class PageSurfaceFactory(
    IRunPainting runs, PulseOffice.UI.Embedding.IContentPainter painter, IEmbeddedRasterizer rasterizer, PulseOffice.Drawing.Printing.IObjectPathBuilder paths) : IPageSurfaceFactory
{
    public IPageSurface Screen(DrawingContext context, string? language = null) => new DrawingSurface(context, runs, painter, language);

    public IPageSurface Print(IPageCanvas canvas, string? language = null) => new CanvasSurface(canvas, rasterizer, paths, language);
}

public sealed class DrawingSurface(DrawingContext context, IRunPainting runs, PulseOffice.UI.Embedding.IContentPainter painter, string? language = null) : IPageSurface
{
    private readonly Stack<DrawingContext.PushedState?> _turns = new();

    private PulseOffice.UI.Controls.Shapes.ShapeSurroundings _backdrop = new() { Language = language };

    public void BeginObject(LayoutRect bounds, double rotationDegrees) =>
        _turns.Push(rotationDegrees == 0 ? null : context.PushTransform(new ObjectTransform(rotationDegrees).Around(Rect(bounds))));

    public void EndObject()
    {
        if (_turns.TryPop(out var turn)) turn?.Dispose();
    }

    public void Backdrop(uint argb) => _backdrop = new PulseOffice.UI.Controls.Shapes.ShapeSurroundings(argb) { Language = language };

    public void Embedded(PulseOffice.Document.Embedding.EmbeddedObject content, LayoutRect bounds, ObjectTransform transform)
    {
        var rect = Rect(bounds);
        using var turn = context.PushTransform(transform.Around(rect));
        painter.Paint(context, content, rect, 1, _backdrop);
    }

    public void Fill(LayoutRect rect, uint argb) => context.FillRectangle(Brush(argb), Rect(rect));

    public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid) =>
        context.DrawLine(Pen(argb, thickness, stroke), new Point(x1, y1), new Point(x2, y2));

    public void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid) =>
        context.DrawRectangle(null, Pen(argb, thickness, stroke), Rect(rect));

    public void RotatedGlyphs(LayoutGlyphRun run, double x, double baseline, uint argb, double degrees)
    {
        using var turn = context.PushTransform(Matrix.CreateTranslation(-x, -baseline) * Matrix.CreateRotation(-degrees * Math.PI / 180) * Matrix.CreateTranslation(x, baseline));
        Glyphs(run, x, baseline, argb);
    }

    public void Glyphs(LayoutGlyphRun run, double x, double baseline, uint argb)
    {
        var scale = run.Style?.ScaleWidth is { } percent && percent > 0 && Math.Abs(percent - 100) > 0.01 ? percent / 100 : 1;
        if (scale == 1)
        {
            runs.Paint(context, run, new Point(x, baseline), Brush(argb));
            return;
        }
        using var squeeze = context.PushTransform(Matrix.CreateTranslation(-x, 0) * Matrix.CreateScale(scale, 1) * Matrix.CreateTranslation(x, 0));
        runs.Paint(context, run with { Advances = [.. run.Advances.Select(advance => advance / scale)] }, new Point(x, baseline), Brush(argb));
    }

    private Pen Pen(uint argb, double thickness, PageStroke stroke) => stroke switch
    {
        PageStroke.Dashed => new Pen(Brush(argb), thickness, new DashStyle([3, 2], 0)),
        PageStroke.Dotted => new Pen(Brush(argb), thickness, new DashStyle([0.5, 3], 0), PenLineCap.Round),
        _ => new Pen(Brush(argb), thickness),
    };

    private Rect Rect(LayoutRect rect) => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));

    private IBrush Brush(uint argb) => new SolidColorBrush(Color.FromUInt32(argb));
}

public sealed class CanvasSurface(IPageCanvas canvas, IEmbeddedRasterizer rasterizer, PulseOffice.Drawing.Printing.IObjectPathBuilder paths, string? language = null) : IPageSurface
{
    private readonly ObjectTurns _turns = new();

    public void BeginObject(LayoutRect bounds, double rotationDegrees) => _turns.Push(bounds, rotationDegrees);

    public void EndObject() => _turns.Pop();

    private (double X, double Y) Turned(double x, double y) => _turns.Apply(x, y);

    private double TurnedDegrees => _turns.Degrees;

    public void Embedded(PulseOffice.Document.Embedding.EmbeddedObject content, LayoutRect bounds, ObjectTransform transform)
    {
        if (_turns.Count > 0)
        {
            var (centerX, centerY) = Turned(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            bounds = new LayoutRect(centerX - bounds.Width / 2, centerY - bounds.Height / 2, bounds.Width, bounds.Height);
            transform = transform with { RotationDegrees = transform.RotationDegrees + TurnedDegrees };
        }
        var rotationDegrees = transform.RotationDegrees;
        if (!transform.Flipped && Vector(content, rotationDegrees))
        {
            var target = Rect(bounds);
            foreach (var path in paths.Build(content, target))
                canvas.DrawPath(rotationDegrees == 0 ? path : path with { RotationDegrees = path.RotationDegrees + rotationDegrees });
            if (rotationDegrees == 0) paths.Paint(new PathlessCanvas(canvas), content, target);
            return;
        }
        if (rasterizer.Rasterize(content, bounds, transform, out var area, language) is { } image) canvas.DrawImage(image, Rect(area));
    }

    public void Fill(LayoutRect rect, uint argb) => canvas.FillRectangle(Rect(_turns.Bounds(rect)), Color(argb));

    public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
    {
        (x1, y1) = Turned(x1, y1);
        (x2, y2) = Turned(x2, y2);
        if (stroke == PageStroke.Solid)
        {
            canvas.DrawLine(Point(x1, y1), Point(x2, y2), Color(argb), Twip.FromPoints(thickness));
            return;
        }
        var length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (length <= 0) return;
        var (on, gap) = stroke == PageStroke.Dashed ? (3.0, 2.0) : (0.6, 2.9);
        for (var start = 0.0; start < length; start += on + gap)
        {
            var end = Math.Min(length, start + on);
            canvas.DrawLine(
                Point(x1 + (x2 - x1) * start / length, y1 + (y2 - y1) * start / length),
                Point(x1 + (x2 - x1) * end / length, y1 + (y2 - y1) * end / length),
                Color(argb), Twip.FromPoints(thickness));
        }
    }

    public void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
    {
        Line(rect.X, rect.Y, rect.Right, rect.Y, argb, thickness, stroke);
        Line(rect.Right, rect.Y, rect.Right, rect.Bottom, argb, thickness, stroke);
        Line(rect.Right, rect.Bottom, rect.X, rect.Bottom, argb, thickness, stroke);
        Line(rect.X, rect.Bottom, rect.X, rect.Y, argb, thickness, stroke);
    }

    public void Glyphs(LayoutGlyphRun run, double x, double baseline, uint argb) => RotatedGlyphs(run, x, baseline, argb, 0);

    public void RotatedGlyphs(LayoutGlyphRun run, double x, double baseline, uint argb, double degrees)
    {
        (x, baseline) = Turned(x, baseline);
        degrees -= TurnedDegrees;
        canvas.DrawGlyphs(new PrintGlyphRun(run.Text, x, baseline, new PrintFont(run.Font.Family, run.Font.Size, run.Font.Bold, run.Font.Italic), Color(argb), run.Advances)
        {
            HorizontalScale = run.Style?.ScaleWidth is { } percent && percent > 0 ? percent / 100 : 1,
            RotationDegrees = degrees,
        });
    }

    private bool Vector(PulseOffice.Document.Embedding.EmbeddedObject content, double rotationDegrees) => paths.IsExact(content) && content switch
    {
        PulseOffice.Drawing.Shapes.ShapeObject { Text.Length: > 0 } => rotationDegrees == 0,
        PulseOffice.Drawing.Shapes.ShapeObject or PulseOffice.Drawing.Freeform.FreeformObject or PulseOffice.Drawing.Shapes.ConnectorObject => true,
        PulseOffice.Drawing.Shapes.ShapeGroupObject group => rotationDegrees == 0 && group.Children.All(child => Vector(child.Content, 0)),
        _ => false,
    };

    private DocumentPoint Point(double x, double y) => new(Twip.FromPoints(x), Twip.FromPoints(y));

    private DocumentRect Rect(LayoutRect rect) =>
        new(Twip.FromPoints(rect.X), Twip.FromPoints(rect.Y), Twip.FromPoints(Math.Max(0, rect.Width)), Twip.FromPoints(Math.Max(0, rect.Height)));

    private PrintColor Color(uint argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}

public sealed class PathlessCanvas(IPageCanvas inner) : IPageCanvas
{
    public DocumentSize PageSize => inner.PageSize;

    public Twip MeasureText(string text, PrintFont font) => inner.MeasureText(text, font);

    public void FillRectangle(DocumentRect rect, PrintColor color) => inner.FillRectangle(rect, color);

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) => inner.DrawLine(from, to, color, thickness);

    public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment = PulseOffice.Printing.TextAlignment.Left,
        PulseOffice.Printing.TextBaseline baseline = PulseOffice.Printing.TextBaseline.Middle) => inner.DrawText(text, bounds, font, color, alignment, baseline);

    public void DrawPath(PrintPath path)
    {
    }

    public void DrawImage(PageImage image, DocumentRect bounds) => inner.DrawImage(image, bounds);

    public void DrawGlyphs(PrintGlyphRun run) => inner.DrawGlyphs(run);

    public IDisposable Clip(DocumentRect area) => inner.Clip(area);

    public void MarkHeading(int level, string title, double topPoints) => inner.MarkHeading(level, title, topPoints);

    public void MarkTarget(string name, double topPoints) => inner.MarkTarget(name, topPoints);

    public void MarkLink(DocumentRect area, string target) => inner.MarkLink(area, target);

    public void MarkNote(DocumentRect area, PrintNote note) => inner.MarkNote(area, note);
}

public readonly record struct ObjectTurn(double CenterX, double CenterY, double Degrees)
{
    public (double X, double Y) Apply(double x, double y)
    {
        if (Degrees == 0) return (x, y);
        var radians = Degrees * Math.PI / 180;
        var (dx, dy) = (x - CenterX, y - CenterY);
        return (CenterX + dx * Math.Cos(radians) - dy * Math.Sin(radians), CenterY + dx * Math.Sin(radians) + dy * Math.Cos(radians));
    }
}

public sealed class ObjectTurns
{
    private readonly Stack<ObjectTurn> _turns = new();

    public int Count => _turns.Count;

    public double Degrees => _turns.Sum(turn => turn.Degrees);

    public void Push(LayoutRect bounds, double rotationDegrees) =>
        _turns.Push(new ObjectTurn(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, rotationDegrees));

    public void Pop() => _turns.TryPop(out _);

    public (double X, double Y) Apply(double x, double y)
    {
        foreach (var turn in _turns) (x, y) = turn.Apply(x, y);
        return (x, y);
    }

    public LayoutRect Bounds(LayoutRect rect)
    {
        if (_turns.Count == 0) return rect;
        var corners = new[] { Apply(rect.X, rect.Y), Apply(rect.Right, rect.Y), Apply(rect.Right, rect.Bottom), Apply(rect.X, rect.Bottom) };
        var left = corners.Min(corner => corner.X);
        var top = corners.Min(corner => corner.Y);
        return new LayoutRect(left, top, corners.Max(corner => corner.X) - left, corners.Max(corner => corner.Y) - top);
    }
}
