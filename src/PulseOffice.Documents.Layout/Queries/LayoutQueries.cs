using System.Runtime.CompilerServices;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Queries;

public readonly record struct CaretRect(int Page, double X, double Y, double Height);

public interface ILayoutQueries
{
    DocumentPosition? HitTest(DocumentLayout layout, int page, double x, double y);

    DocumentPosition? CharacterAt(DocumentLayout layout, int page, double x, double y);

    CaretRect? Caret(DocumentLayout layout, DocumentPosition position);

    IReadOnlyList<DocumentRange> Block(DocumentLayout layout, int page, double left, double top, double right, double bottom);
}

public sealed class LayoutQueries(ILineOffsets offsets, ILayoutReadOnlyText readOnly) : ILayoutQueries
{
    public LayoutQueries(ILineOffsets offsets) : this(offsets, new LayoutReadOnlyText())
    {
    }

    private readonly ConditionalWeakTable<DocumentLayout, Dictionary<string, List<(int Page, LineBox Line)>>> _index = [];

    public DocumentPosition? HitTest(DocumentLayout layout, int page, double x, double y)
    {
        if (page < 0 || page >= layout.Pages.Count) return null;
        var box = layout.Pages[page];
        var frames = box.TurnedFrames.ToList();
        if (frames.FirstOrDefault(frame => frame.Lines.Any() && frame.RotatedBounds.Contains(x, y)) is { } turned)
        {
            var (localX, localY) = Untwisted(turned, x, y);
            var own = turned.Lines.ToList();
            var hit = Nearest(own, localX, localY);
            return new DocumentPosition(hit.ParagraphId, offsets.OffsetAt(hit, localX));
        }
        var area = new[] { box.Header, box.Footer, box.Footnotes }.FirstOrDefault(candidate => candidate is not null && Editable(candidate.Lines).Any() && y >= candidate.Bounds.Y && y <= candidate.Bounds.Bottom);
        IReadOnlyList<LineBox> lines = area is null ? box.Lines : [.. Editable(area.Lines)];
        if (lines.Count == 0) lines = [.. Editable(box.AllLines)];
        if (frames.Count > 0)
        {
            var hidden = new HashSet<LineBox>(frames.SelectMany(frame => frame.Lines), ReferenceEqualityComparer.Instance);
            var plain = lines.Where(line => !hidden.Contains(line)).ToList();
            if (plain.Count > 0) lines = plain;
        }
        if (lines.Count == 0) return null;
        var line = Nearest(lines, x, y);
        return new DocumentPosition(line.ParagraphId, offsets.OffsetAt(line, x));
    }

    public DocumentPosition? CharacterAt(DocumentLayout layout, int page, double x, double y)
    {
        if (page < 0 || page >= layout.Pages.Count) return null;
        var frames = layout.Pages[page].TurnedFrames.ToList();
        foreach (var frame in frames.Where(frame => frame.RotatedBounds.Contains(x, y)))
        {
            var (localX, localY) = Untwisted(frame, x, y);
            if (CharacterIn(frame.Lines, localX, localY) is { } inside) return inside;
        }
        var turned = new HashSet<LineBox>(frames.SelectMany(frame => frame.Lines), ReferenceEqualityComparer.Instance);
        return CharacterIn(Editable(layout.Pages[page].AllLines).Where(line => !turned.Contains(line)), x, y);
    }

    private DocumentPosition? CharacterIn(IEnumerable<LineBox> lines, double x, double y)
    {
        foreach (var line in lines)
        {
            if (y < line.Top || y >= line.Top + line.Line.Height || x < line.Left || x > line.Left + line.Line.Width) continue;
            var start = offsets.StartOf(line);
            var end = offsets.EndOf(line);
            var near = offsets.OffsetAt(line, x);
            foreach (var candidate in new[] { near - 1, near })
            {
                if (candidate < start || candidate >= end) continue;
                var (from, to) = (offsets.XOf(line, candidate), offsets.XOf(line, candidate + 1));
                if (x >= Math.Min(from, to) && x < Math.Max(from, to)) return new DocumentPosition(line.ParagraphId, candidate);
            }
        }
        return null;
    }

    public CaretRect? Caret(DocumentLayout layout, DocumentPosition position)
    {
        if (!Index(layout).TryGetValue(position.ParagraphId, out var lines) || lines.Count == 0) return null;
        var chosen = lines[^1];
        foreach (var entry in lines)
        {
            var composed = entry.Line.Line;
            if (position.Offset < composed.End || composed.IsLast || composed.Forced && position.Offset < composed.End)
            {
                chosen = entry;
                break;
            }
        }
        var line = chosen.Line;
        return new CaretRect(chosen.Page, offsets.XOf(line, position.Offset), line.Top, line.Line.CaretHeight);
    }

    public IReadOnlyList<DocumentRange> Block(DocumentLayout layout, int page, double left, double top, double right, double bottom)
    {
        if (page < 0 || page >= layout.Pages.Count) return [];
        var ranges = new List<DocumentRange>();
        foreach (var line in layout.Pages[page].Lines
            .Where(line => line.Story == StoryKind.Body && line.Top + line.Line.Height > top && line.Top < bottom)
            .OrderBy(line => line.Top))
        {
            if (offsets.SpanBetween(line, left, right) is not { } span) continue;
            ranges.Add(new DocumentRange(
                new DocumentPosition(line.ParagraphId, span.Start),
                new DocumentPosition(line.ParagraphId, span.End)));
        }
        return ranges;
    }

    private IEnumerable<LineBox> Editable(IEnumerable<LineBox> lines) => lines.Where(line => !readOnly.IsReadOnly(line));

    private (double X, double Y) Untwisted(ITurnedFrame frame, double x, double y)
    {
        var radians = -frame.RotationDegrees * Math.PI / 180;
        var (cx, cy) = (frame.Bounds.X + frame.Bounds.Width / 2, frame.Bounds.Y + frame.Bounds.Height / 2);
        var (dx, dy) = (x - cx, y - cy);
        return (cx + dx * Math.Cos(radians) - dy * Math.Sin(radians), cy + dx * Math.Sin(radians) + dy * Math.Cos(radians));
    }

    private LineBox Nearest(IReadOnlyList<LineBox> lines, double x, double y)
    {
        var column = lines.Where(line => x >= line.Left && x <= line.Left + line.Line.Width).ToList();
        var pool = column.Count > 0 ? column : lines;
        var best = pool[0];
        var bestScore = (Vertical: double.MaxValue, Horizontal: double.MaxValue);
        foreach (var line in pool)
        {
            var bottom = line.Top + line.Line.Height;
            var dy = y < line.Top ? line.Top - y : y >= bottom ? y - bottom + 0.0001 : 0;
            var right = line.Left + line.Line.Width;
            var dx = x < line.Left ? line.Left - x : x > right ? x - right : 0;
            if (dy < bestScore.Vertical - 1e-9 || Math.Abs(dy - bestScore.Vertical) <= 1e-9 && dx < bestScore.Horizontal)
            {
                best = line;
                bestScore = (dy, dx);
            }
        }
        return best;
    }

    private Dictionary<string, List<(int Page, LineBox Line)>> Index(DocumentLayout layout) =>
        _index.GetValue(layout, Build);

    private Dictionary<string, List<(int Page, LineBox Line)>> Build(DocumentLayout layout)
    {
        var index = new Dictionary<string, List<(int, LineBox)>>(StringComparer.Ordinal);
        foreach (var page in layout.Pages)
        {
            foreach (var line in Editable(page.AllLines))
            {
                if (!index.TryGetValue(line.ParagraphId, out var list)) index[line.ParagraphId] = list = [];
                list.Add((page.Index, line));
            }
        }
        return index;
    }
}
