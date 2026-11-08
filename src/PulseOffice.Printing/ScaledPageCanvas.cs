using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public sealed class ScaledPageCanvas(IPageCanvas inner, double scale, DocumentPoint origin, DocumentPoint offset)
    : IPageCanvas
{
    private readonly IPageCanvas _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly double _scale = double.IsFinite(scale) && scale > 0 ? scale : 1;

    public DocumentSize PageSize => _inner.PageSize;

    public Twip MeasureText(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        var measured = _inner.MeasureText(text, Font(font));
        return new Twip((int)Math.Round(measured.Value / _scale));
    }

    public void FillRectangle(DocumentRect rect, PrintColor color) => _inner.FillRectangle(Rect(rect), color);

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) =>
        _inner.DrawLine(Point(from), Point(to), color, Length(thickness));

    public void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
    {
        ArgumentNullException.ThrowIfNull(font);
        _inner.DrawText(text, Rect(bounds), Font(font), color, alignment, baseline);
    }

    public void DrawPath(PrintPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _inner.DrawPath(path with
        {
            Bounds = Rect(path.Bounds),
            StrokeWidth = Length(path.StrokeWidth),
            ShadowOffsetX = Length(path.ShadowOffsetX),
            ShadowOffsetY = Length(path.ShadowOffsetY),
        });
    }

    public void DrawImage(PageImage image, DocumentRect bounds) => _inner.DrawImage(image, Rect(bounds));

    public IDisposable Clip(DocumentRect area) => _inner.Clip(Rect(area));

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _inner.DrawGlyphs(run with
        {
            X = origin.X.ToPoints() + offset.X.ToPoints() + (run.X - origin.X.ToPoints()) * _scale,
            Baseline = origin.Y.ToPoints() + offset.Y.ToPoints() + (run.Baseline - origin.Y.ToPoints()) * _scale,
            Font = Font(run.Font),
            Advances = [.. run.Advances.Select(advance => advance * _scale)],
        });
    }

    public void MarkHeading(int level, string title, double topPoints) => _inner.MarkHeading(level, title, Vertical(topPoints));

    public void MarkTarget(string name, double topPoints) => _inner.MarkTarget(name, Vertical(topPoints));

    public void MarkLink(DocumentRect area, string target) => _inner.MarkLink(Rect(area), target);

    public void MarkNote(DocumentRect area, PrintNote note) => _inner.MarkNote(Rect(area), note);

    private double Vertical(double points) => origin.Y.ToPoints() + offset.Y.ToPoints() + (points - origin.Y.ToPoints()) * _scale;

    private PrintFont Font(PrintFont font) => font with { SizePoints = font.SizePoints * _scale };

    private Twip Length(Twip value) => new((int)Math.Round(value.Value * _scale));

    private Twip Coordinate(Twip value, Twip from, Twip shift) =>
        new((int)Math.Round(from.Value + shift.Value + (value.Value - from.Value) * _scale));

    private DocumentPoint Point(DocumentPoint point) =>
        new(Coordinate(point.X, origin.X, offset.X), Coordinate(point.Y, origin.Y, offset.Y));

    private DocumentRect Rect(DocumentRect rect) =>
        new(Coordinate(rect.Left, origin.X, offset.X), Coordinate(rect.Top, origin.Y, offset.Y),
            Length(rect.Width), Length(rect.Height));
}
