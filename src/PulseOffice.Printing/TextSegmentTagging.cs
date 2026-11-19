using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public sealed record PrintTextSegment(PrintTag Tag, string Text)
{
    public PrintTag? Label { get; init; }
}

public sealed record PrintTextSegments(IReadOnlyList<PrintTextSegment> Segments, bool AdvanceOnMiss);

public interface ITextSegmentTagging
{
    IPageCanvas Wrap(IPageCanvas canvas, PrintTextSegments segments);
}

public sealed class TextSegmentTagging : ITextSegmentTagging
{
    public IPageCanvas Wrap(IPageCanvas canvas, PrintTextSegments segments)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(segments);
        return segments.Segments.Count == 0 ? canvas : new SegmentTaggingCanvas(canvas, segments);
    }
}

public sealed class SegmentTaggingCanvas(IPageCanvas inner, PrintTextSegments segments) : IPageCanvas
{
    private int _position;
    private int _offset;

    public DocumentSize PageSize => inner.PageSize;

    public Twip MeasureText(string text, PrintFont font) => inner.MeasureText(text, font);

    public void FillRectangle(DocumentRect rect, PrintColor color) => Artifact(() => inner.FillRectangle(rect, color));

    public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) =>
        Artifact(() => inner.DrawLine(from, to, color, thickness));

    public void DrawText(
        string text, DocumentRect bounds, PrintFont font, PrintColor color,
        TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle)
    {
        inner.Tag(Select(text));
        inner.DrawText(text, bounds, font, color, alignment, baseline);
    }

    public void DrawGlyphs(PrintGlyphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        inner.Tag(Select(run.Text));
        inner.DrawGlyphs(run);
    }

    public void DrawPath(PrintPath path) => Artifact(() => inner.DrawPath(path));

    public void DrawImage(PageImage image, DocumentRect bounds) => inner.DrawImage(image, bounds);

    public IDisposable Clip(DocumentRect area) => inner.Clip(area);

    public void MarkHeading(int level, string title, double topPoints) => inner.MarkHeading(level, title, topPoints);

    public void MarkTarget(string name, double topPoints) => inner.MarkTarget(name, topPoints);

    public void MarkLink(DocumentRect area, string target) => inner.MarkLink(area, target);

    public void MarkNote(DocumentRect area, PrintNote note) => inner.MarkNote(area, note);

    public void Tag(PrintTag? tag) => inner.Tag(tag);

    public void BeginArtifact() => inner.BeginArtifact();

    public void EndArtifact() => inner.EndArtifact();

    private void Artifact(Action paint)
    {
        inner.BeginArtifact();
        paint();
        inner.EndArtifact();
    }

    private PrintTag Select(string? text)
    {
        var list = segments.Segments;
        var wanted = (text ?? string.Empty).Trim();
        if (wanted.Length == 0) return list[_position].Tag;

        var here = list[_position].Text.IndexOf(wanted, _offset, StringComparison.Ordinal);
        if (here >= 0)
        {
            _offset = here + wanted.Length;
            return list[_position].Tag;
        }

        for (var next = _position + 1; next < list.Count; next++)
        {
            var found = list[next].Text.IndexOf(wanted, StringComparison.Ordinal);
            if (found < 0) continue;
            _position = next;
            _offset = found + wanted.Length;
            return list[next].Tag;
        }

        var pending = _offset == 0 ? _position : Math.Min(list.Count - 1, _position + 1);
        if (list[pending].Label is { } label)
        {
            _position = pending;
            _offset = 0;
            return label;
        }

        if (segments.AdvanceOnMiss && _offset > 0 && _position + 1 < list.Count) _position++;
        _offset = segments.AdvanceOnMiss ? list[_position].Text.Length : _offset;
        return list[_position].Tag;
    }
}
