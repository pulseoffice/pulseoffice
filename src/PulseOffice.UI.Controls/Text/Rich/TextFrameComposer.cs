using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record ComposedTextFrame(TextFrameArea Area, IReadOnlyList<TypesetLine> Lines, IReadOnlyList<double> Tops)
{
    public Point Origin(int line, TypesetSegment segment) => new(Area.Inner.X + segment.X, Tops[line] + segment.Y);

    public int ContentEnd(int line) => Lines[line].Segments.Count == 0 ? Lines[line].Offset : Lines[line].Segments.Max(segment => segment.End);
}

public interface ITextFrameComposer
{
    ComposedTextFrame Compose(RichText text, Rect bounds, ITextTheme theme, TextFields fields, double scale);
}

public sealed class TextFrameComposer(IListLabels labels) : ITextFrameComposer
{
    private const double SmallestFactor = 0.25;

    private readonly RunText _runText = new();
    private readonly TextFrameGeometry _geometry = new();
    private readonly TextFramePlacement _placement = new();

    public ComposedTextFrame Compose(RichText text, Rect bounds, ITextTheme theme, TextFields fields, double scale)
    {
        ArgumentNullException.ThrowIfNull(text);

        var frame = text.Frame;
        var area = _geometry.Area(bounds, frame, scale);
        var block = new TextBlockTypesetter(theme, labels, _runText);
        var paragraphs = text.Paragraphs.Count > 0 ? text.Paragraphs : [new TextParagraph(string.Empty)];

        var lines = block.Typeset(paragraphs, area, fields, 1.0, frame.WordWrap);
        var height = block.Height(lines);
        if (frame.Autofit == TextAutofit.ShrinkText && height > area.Inner.Height)
            (lines, height) = Shrink(block, paragraphs, area, fields, frame.WordWrap);

        return new ComposedTextFrame(area, lines, _placement.Tops(lines, area, frame.Anchor, height));
    }

    private (IReadOnlyList<TypesetLine> Lines, double Height) Shrink(
        TextBlockTypesetter block, IReadOnlyList<TextParagraph> paragraphs, TextFrameArea area, TextFields fields, bool wrap)
    {
        var (low, high) = (SmallestFactor, 1.0);
        var best = block.Typeset(paragraphs, area, fields, low, wrap);
        for (var step = 0; step < 7; step++)
        {
            var middle = (low + high) / 2;
            var attempt = block.Typeset(paragraphs, area, fields, middle, wrap);
            if (block.Height(attempt) <= area.Inner.Height)
            {
                low = middle;
                best = attempt;
            }
            else high = middle;
        }
        return (best, block.Height(best));
    }
}

public interface ITextCaretGeometry
{
    Rect Caret(ComposedTextFrame frame, TextPosition position);

    IReadOnlyList<Geometry> Selection(ComposedTextFrame frame, TextPosition from, TextPosition to);

    TextPosition Hit(ComposedTextFrame frame, Point point);

    IReadOnlyList<CaretRow> Rows(ComposedTextFrame frame, Func<TextPosition, int> index);
}

public sealed class TextCaretGeometry : ITextCaretGeometry
{
    public Rect Caret(ComposedTextFrame frame, TextPosition position)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Lines.Count == 0) return new Rect(frame.Area.Inner.TopLeft, new Size(0, frame.Area.Points(18)));

        var line = Math.Clamp(position.Paragraph, 0, frame.Lines.Count - 1);
        var content = Math.Clamp(frame.Lines[line].Offset + Math.Max(0, position.Offset), frame.Lines[line].Offset, frame.ContentEnd(line));
        return CaretAt(frame, line, content);
    }

    public IReadOnlyList<Geometry> Selection(ComposedTextFrame frame, TextPosition from, TextPosition to)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Compare(from, to) > 0) (from, to) = (to, from);

        var rects = new List<Geometry>();
        var last = Math.Min(to.Paragraph, frame.Lines.Count - 1);
        for (var line = Math.Max(0, from.Paragraph); line <= last; line++)
        {
            var typeset = frame.Lines[line];
            var end = frame.ContentEnd(line);
            var start = line == from.Paragraph ? Math.Min(typeset.Offset + from.Offset, end) : typeset.Offset;
            var stop = line == to.Paragraph ? Math.Min(typeset.Offset + to.Offset, end) : end;

            foreach (var segment in typeset.Segments)
            {
                var a = Math.Max(start, segment.Start);
                var b = Math.Min(stop, segment.End);
                if (b <= a) continue;
                var area = segment.Text.BuildHighlightGeometry(frame.Origin(line, segment), a - segment.Start, b - a);
                if (area is not null) rects.Add(area);
            }

            if (line < to.Paragraph && stop <= start)
            {
                var caret = CaretAt(frame, line, end);
                rects.Add(new RectangleGeometry(new Rect(caret.X, caret.Y, Math.Max(2, caret.Height / 4), caret.Height)));
            }
        }
        return rects;
    }

    public TextPosition Hit(ComposedTextFrame frame, Point point)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Lines.Count == 0) return new TextPosition(0, 0);

        var line = 0;
        for (var index = 0; index < frame.Lines.Count; index++)
            if (point.Y >= frame.Tops[index] - frame.Lines[index].Before) line = index;

        var typeset = frame.Lines[line];
        var (best, bestRow, bestColumn) = (typeset.Offset, double.MaxValue, double.MaxValue);
        for (var content = typeset.Offset; content <= frame.ContentEnd(line); content++)
        {
            var caret = CaretAt(frame, line, content);
            var row = point.Y < caret.Top ? caret.Top - point.Y : point.Y > caret.Bottom ? point.Y - caret.Bottom : 0;
            var column = Math.Abs(point.X - caret.X);
            if (row < bestRow - 0.01 || (Math.Abs(row - bestRow) <= 0.01 && column < bestColumn))
                (best, bestRow, bestColumn) = (content, row, column);
        }
        return new TextPosition(line, best - typeset.Offset);
    }

    public IReadOnlyList<CaretRow> Rows(ComposedTextFrame frame, Func<TextPosition, int> index)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(index);

        var rows = new List<CaretRow>();
        for (var line = 0; line < frame.Lines.Count; line++)
        {
            var typeset = frame.Lines[line];
            var bands = typeset.Segments
                .GroupBy(segment => Math.Round(segment.Y, 1))
                .OrderBy(band => band.Key)
                .Select(band => (Start: band.Min(segment => segment.Start), End: band.Max(segment => segment.End)))
                .ToList();
            if (bands.Count == 0) bands.Add((typeset.Offset, typeset.Offset));

            for (var band = 0; band < bands.Count; band++)
            {
                var start = band == 0 ? typeset.Offset : bands[band].Start;
                var last = band + 1 < bands.Count ? Math.Min(bands[band].End, bands[band + 1].Start - 1) : frame.ContentEnd(line);
                var stops = new List<CaretStop>();
                for (var content = start; content <= Math.Max(start, last); content++)
                    stops.Add(new CaretStop(index(new TextPosition(line, content - typeset.Offset)), CaretAt(frame, line, content).X));
                rows.Add(new CaretRow(stops));
            }
        }
        return rows;
    }

    private Rect CaretAt(ComposedTextFrame frame, int line, int content)
    {
        var typeset = frame.Lines[line];
        if (typeset.Segments.Count == 0) return new Rect(frame.Area.Inner.X, frame.Tops[line], 0, frame.Area.Points(18));

        var segment = SegmentFor(typeset, content);
        var origin = frame.Origin(line, segment);
        var length = segment.End - segment.Start;
        var local = Math.Clamp(content - segment.Start, 0, length);

        if (length == 0) return Edge(segment.Text.BuildHighlightGeometry(origin, 0, 1), origin, segment.Text.Height, leading: true);
        return local < length
            ? Edge(segment.Text.BuildHighlightGeometry(origin, local, 1), origin, segment.Text.Height, leading: true)
            : Edge(segment.Text.BuildHighlightGeometry(origin, length - 1, 1), origin, segment.Text.Height, leading: false);
    }

    private Rect Edge(Geometry? geometry, Point origin, double height, bool leading)
    {
        if (geometry is null || geometry.Bounds.Height <= 0) return new Rect(origin.X, origin.Y, 0, height);
        var bounds = geometry.Bounds;
        return new Rect(leading ? bounds.Left : bounds.Right, bounds.Top, 0, bounds.Height);
    }

    private TypesetSegment SegmentFor(TypesetLine line, int content)
    {
        foreach (var segment in line.Segments)
            if (content >= segment.Start && content < segment.End) return segment;

        TypesetSegment? before = null;
        foreach (var segment in line.Segments)
            if (segment.End <= content && (before is null || segment.End >= before.End)) before = segment;
        return before ?? line.Segments[0];
    }

    private int Compare(TextPosition a, TextPosition b) =>
        a.Paragraph != b.Paragraph ? a.Paragraph.CompareTo(b.Paragraph) : a.Offset.CompareTo(b.Offset);
}
