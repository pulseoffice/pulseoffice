using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public sealed class ClippedPageCanvas(IPageCanvas inner, DocumentRect clip) : IPageCanvas
{
    private readonly IPageCanvas _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public DocumentSize PageSize => _inner.PageSize;

    public Twip MeasureText(string text, PrintFont font) => _inner.MeasureText(text, font);

    public void FillRectangle(DocumentRect rect, PrintColor color)
    {
        if (Intersection(rect) is { } visible) _inner.FillRectangle(visible, color);
    }

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
    {
        if (from.X == to.X)
        {
            if (from.X.Value < clip.Left.Value || from.X.Value > clip.Right.Value) return;
            var top = Math.Max(Math.Min(from.Y.Value, to.Y.Value), clip.Top.Value);
            var bottom = Math.Min(Math.Max(from.Y.Value, to.Y.Value), clip.Bottom.Value);
            if (top < bottom) _inner.DrawLine(new DocumentPoint(from.X, new Twip(top)), new DocumentPoint(from.X, new Twip(bottom)), color, thickness);
            return;
        }

        if (from.Y == to.Y)
        {
            if (from.Y.Value < clip.Top.Value || from.Y.Value > clip.Bottom.Value) return;
            var left = Math.Max(Math.Min(from.X.Value, to.X.Value), clip.Left.Value);
            var right = Math.Min(Math.Max(from.X.Value, to.X.Value), clip.Right.Value);
            if (left < right) _inner.DrawLine(new DocumentPoint(new Twip(left), from.Y), new DocumentPoint(new Twip(right), from.Y), color, thickness);
            return;
        }

        if (clip.Contains(from) && clip.Contains(to)) _inner.DrawLine(from, to, color, thickness);
    }

    public void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (string.IsNullOrEmpty(text)) return;

        var height = Twip.FromPoints(font.SizePoints).Value;
        var top = baseline switch
        {
            TextBaseline.Top => bounds.Top.Value,
            TextBaseline.Bottom => bounds.Bottom.Value - height,
            _ => bounds.Top.Value + (bounds.Height.Value - height) / 2,
        };

        var width = Math.Min(_inner.MeasureText(text, font).Value, bounds.Width.Value);
        var left = alignment switch
        {
            TextAlignment.Center => bounds.Left.Value + (bounds.Width.Value - width) / 2,
            TextAlignment.Right => bounds.Right.Value - width,
            _ => bounds.Left.Value,
        };

        var line = new DocumentRect(new Twip(left), new Twip(top), new Twip(width), new Twip(height));
        if (InsideAcross(line)) _inner.DrawText(text, bounds, font, color, alignment, baseline);
    }

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Text.Length == 0) return;

        var scale = run.HorizontalScale > 0 ? run.HorizontalScale : 1;
        var radians = -run.RotationDegrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var count = Math.Min(run.Text.Length, run.Advances.Count);
        var visible = new bool[run.Text.Length];
        var offsets = new double[run.Text.Length + 1];
        for (var index = 0; index < run.Text.Length; index++)
        {
            var advance = index < count ? run.Advances[index] * scale : 0;
            offsets[index + 1] = offsets[index] + advance;
            var middle = offsets[index] + advance / 2;
            var height = -run.Font.SizePoints * 0.3;
            var x = run.X + middle * cos - height * sin;
            var y = run.Baseline + middle * sin + height * cos;
            visible[index] = InsidePoint(Twip.FromPoints(x).Value, Twip.FromPoints(y).Value);
        }

        if (visible.All(shown => shown))
        {
            _inner.DrawGlyphs(run);
            return;
        }

        var start = 0;
        while (start < run.Text.Length)
        {
            if (!visible[start] || char.IsLowSurrogate(run.Text[start]))
            {
                start++;
                continue;
            }
            var end = start;
            while (end < run.Text.Length && (visible[end] || char.IsLowSurrogate(run.Text[end]))) end++;
            var shift = offsets[start];
            _inner.DrawGlyphs(run with
            {
                Text = run.Text[start..end],
                X = run.X + shift * cos,
                Baseline = run.Baseline + shift * sin,
                Advances = [.. run.Advances.Skip(start).Take(Math.Max(0, Math.Min(end, count) - start))],
            });
            start = end;
        }
    }

    public void MarkHeading(int level, string title, double topPoints) => _inner.MarkHeading(level, title, topPoints);

    public void MarkTarget(string name, double topPoints) => _inner.MarkTarget(name, topPoints);

    public void MarkLink(DocumentRect area, string target)
    {
        if (Intersection(area) is { } visible) _inner.MarkLink(visible, target);
    }

    public void MarkNote(DocumentRect area, PrintNote note)
    {
        if (area.IntersectsWith(clip)) _inner.MarkNote(area, note);
    }

    private bool InsidePoint(double x, double y) =>
        x >= clip.Left.Value && x <= clip.Right.Value && y >= clip.Top.Value && y <= clip.Bottom.Value;

    public void DrawPath(PrintPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var reach = Reach(path);
        if (!reach.IntersectsWith(clip)) return;

        if (path.RotationDegrees % 360 == 0 && Inside(reach))
        {
            _inner.DrawPath(path);
            return;
        }

        using var clipped = _inner.Clip(clip);
        _inner.DrawPath(path);
    }

    public void DrawImage(PageImage image, DocumentRect bounds)
    {
        if (!bounds.IntersectsWith(clip)) return;

        if (Inside(bounds))
        {
            _inner.DrawImage(image, bounds);
            return;
        }

        using var clipped = _inner.Clip(clip);
        _inner.DrawImage(image, bounds);
    }

    public IDisposable Clip(DocumentRect area) =>
        _inner.Clip(Intersection(area) ?? new DocumentRect(area.Left, area.Top, Twip.Zero, Twip.Zero));

    private DocumentRect Reach(PrintPath path)
    {
        var bounds = path.Bounds;
        var half = path.Stroke is null ? 0 : (path.StrokeWidth.Value + 1) / 2;
        var left = bounds.Left.Value - half;
        var top = bounds.Top.Value - half;
        var right = bounds.Right.Value + half;
        var bottom = bounds.Bottom.Value + half;
        if (path.Shadow is not null)
        {
            left = Math.Min(left, left + path.ShadowOffsetX.Value);
            right = Math.Max(right, right + path.ShadowOffsetX.Value);
            top = Math.Min(top, top + path.ShadowOffsetY.Value);
            bottom = Math.Max(bottom, bottom + path.ShadowOffsetY.Value);
        }
        return new DocumentRect(new Twip(left), new Twip(top), new Twip(right - left), new Twip(bottom - top));
    }

    private bool InsideAcross(DocumentRect line) =>
        line.Left.Value >= clip.Left.Value && line.Right.Value <= clip.Right.Value
        && line.Top.Value + line.Height.Value / 2 >= clip.Top.Value && line.Top.Value + line.Height.Value / 2 <= clip.Bottom.Value;

    private bool Inside(DocumentRect rect) =>
        rect.Left.Value >= clip.Left.Value && rect.Right.Value <= clip.Right.Value
        && rect.Top.Value >= clip.Top.Value && rect.Bottom.Value <= clip.Bottom.Value;

    private DocumentRect? Intersection(DocumentRect rect)
    {
        var left = Math.Max(rect.Left.Value, clip.Left.Value);
        var top = Math.Max(rect.Top.Value, clip.Top.Value);
        var right = Math.Min(rect.Right.Value, clip.Right.Value);
        var bottom = Math.Min(rect.Bottom.Value, clip.Bottom.Value);

        return right > left && bottom > top
            ? new DocumentRect(new Twip(left), new Twip(top), new Twip(right - left), new Twip(bottom - top))
            : null;
    }
}
