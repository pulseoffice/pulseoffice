using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record ComposedTextFrame(TextFrameArea Area, IReadOnlyList<TypesetLine> Lines, IReadOnlyList<double> Tops)
{
    public IReadOnlyList<double>? Shifts { get; init; }

    public Matrix Placement { get; init; } = Matrix.Identity;

    public bool Stacked { get; init; }

    public double Shift(int line) => Shifts is { } shifts && line < shifts.Count ? shifts[line] : 0;

    public double Left(int line) => Area.Inner.X + Shift(line);

    public Point Origin(int line, TypesetSegment segment) => new(Left(line) + segment.X, Tops[line] + segment.Y);

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
    private readonly TextFlowFrame _flow = new();
    private readonly TextColumns _columns = new();
    private readonly TextStacks _stacks = new();
    private readonly TextLineMirror _mirror = new();

    public ComposedTextFrame Compose(RichText text, Rect bounds, ITextTheme theme, TextFields fields, double scale)
    {
        ArgumentNullException.ThrowIfNull(text);

        var frame = text.Frame;
        var layout = frame.Layout;
        var stacked = _flow.Stacks(layout);
        var outer = _geometry.Area(_flow.Box(bounds, layout), frame, scale);
        var columns = stacked ? 1 : Math.Max(1, layout.Columns);
        var spacing = columns > 1 ? Math.Min(outer.Points(layout.ColumnSpacing), outer.Inner.Width / 2) : 0;
        var area = columns > 1
            ? outer with { Inner = outer.Inner.WithWidth(Math.Max(1, (outer.Inner.Width - spacing * (columns - 1)) / columns)) }
            : outer;
        var block = new TextBlockTypesetter(theme, labels, _runText);
        var paragraphs = text.Paragraphs.Count > 0 ? text.Paragraphs : [new TextParagraph(string.Empty)];
        var placement = _flow.Turn(bounds, _flow.Degrees(layout, frame.RotationDegrees, 0));

        var upright = layout.Flow == TextFlow.EastAsianVertical && !stacked;
        var mirrored = layout.LinesLeftToRight && !stacked;
        var lines = block.Typeset(paragraphs, area, fields, 1.0, frame.WordWrap, stacked, upright, mirrored);
        if (stacked) return _stacks.Arrange(area, lines, frame.Anchor, layout.Flow == TextFlow.StackedRightToLeft) with { Placement = placement, Stacked = true };

        var height = block.Height(lines);
        if (frame.Autofit == TextAutofit.ShrinkText && height > area.Inner.Height)
            (lines, height) = Shrink(block, paragraphs, area, fields, frame.WordWrap, upright, mirrored);

        var composed = columns == 1
            ? new ComposedTextFrame(area, mirrored ? _mirror.Bands(lines) : lines, Ordered(lines, _placement.Tops(lines, area, frame.Anchor, height), mirrored))
            : _columns.Distribute(area, lines, _placement.Tops(lines, area, TextAnchor.Top, height), columns, area.Inner.Width + spacing);
        return composed with { Placement = placement };
    }

    private IReadOnlyList<double> Ordered(IReadOnlyList<TypesetLine> lines, IReadOnlyList<double> tops, bool mirrored)
    {
        if (!mirrored || lines.Count < 2) return tops;
        var first = tops.Min();
        var last = Enumerable.Range(0, lines.Count).Max(index => tops[index] + lines[index].Height);
        return [.. Enumerable.Range(0, lines.Count).Select(index => first + last - tops[index] - lines[index].Height)];
    }

    private (IReadOnlyList<TypesetLine> Lines, double Height) Shrink(
        TextBlockTypesetter block, IReadOnlyList<TextParagraph> paragraphs, TextFrameArea area, TextFields fields, bool wrap, bool upright, bool mirrored)
    {
        var (low, high) = (SmallestFactor, 1.0);
        var best = block.Typeset(paragraphs, area, fields, low, wrap, false, upright, mirrored);
        for (var step = 0; step < 7; step++)
        {
            var middle = (low + high) / 2;
            var attempt = block.Typeset(paragraphs, area, fields, middle, wrap, false, upright, mirrored);
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

public sealed class TextLineMirror
{
    public IReadOnlyList<TypesetLine> Bands(IReadOnlyList<TypesetLine> lines) =>
        [.. lines.Select(line => line.Segments.Count < 2 ? line : line with
        {
            Segments = [.. line.Segments.Select(segment => segment with { Y = line.Height - segment.Y - segment.Text.Height })],
        })];
}

public sealed class TextColumns
{
    public ComposedTextFrame Distribute(TextFrameArea area, IReadOnlyList<TypesetLine> lines, IReadOnlyList<double> tops, int columns, double pitch)
    {
        var placed = new List<double>(lines.Count);
        var shifts = new List<double>(lines.Count);
        var (column, start) = (0, 0.0);
        for (var index = 0; index < lines.Count; index++)
        {
            var local = tops[index] - area.Inner.Y;
            var bottom = local + lines[index].Height * lines[index].Paragraph.LineSpacing;
            if (column < columns - 1 && index > 0 && bottom - start > area.Inner.Height && local > start)
                (column, start) = (column + 1, local - lines[index].Before);
            placed.Add(area.Inner.Y + local - start);
            shifts.Add(column * pitch);
        }
        return new ComposedTextFrame(area, lines, placed) { Shifts = shifts };
    }
}

public sealed class TextStacks
{
    private const double GapPoints = 4;

    public ComposedTextFrame Arrange(TextFrameArea area, IReadOnlyList<TypesetLine> lines, TextAnchor anchor, bool rightToLeft = false)
    {
        var tallest = lines.Count == 0 ? 0 : lines.Max(line => line.Before + line.Height);
        var free = Math.Max(0, area.Inner.Height - tallest);
        var top = area.Inner.Y + anchor switch
        {
            TextAnchor.Middle or TextAnchor.Justified => free / 2,
            TextAnchor.Bottom => free,
            _ => 0,
        };
        var tops = new List<double>(lines.Count);
        var shifts = new List<double>(lines.Count);
        var shift = 0.0;
        foreach (var line in lines)
        {
            tops.Add(top + line.Before);
            shifts.Add(shift);
            shift += Width(line) + area.Points(GapPoints);
        }
        var offset = Offset(area, lines, shifts, rightToLeft);
        for (var index = 0; index < shifts.Count; index++)
            shifts[index] = rightToLeft
                ? Math.Max(0, area.Inner.Width - shifts[index] - Width(lines[index])) - offset
                : shifts[index] + offset;
        return new ComposedTextFrame(area, lines, tops) { Shifts = shifts, Stacked = true };
    }

    private double Offset(TextFrameArea area, IReadOnlyList<TypesetLine> lines, IReadOnlyList<double> shifts, bool rightToLeft)
    {
        if (lines.Count == 0) return 0;
        var free = Math.Max(0, area.Inner.Width - shifts[^1] - Width(lines[^1]));
        return lines[0].Paragraph.Align switch
        {
            TextAlign.Center => free / 2,
            TextAlign.Right when !rightToLeft => free,
            _ => 0,
        };
    }

    public double Width(TypesetLine line) =>
        line.Segments.Count == 0 ? 0 : line.Segments.Max(segment => segment.X + segment.Text.WidthIncludingTrailingWhitespace);
}

public interface ITextCaretGeometry
{
    Rect Caret(ComposedTextFrame frame, TextPosition position);

    IReadOnlyList<Geometry> Selection(ComposedTextFrame frame, TextPosition from, TextPosition to);

    TextPosition Hit(ComposedTextFrame frame, Point point);

    IReadOnlyList<CaretRow> Rows(ComposedTextFrame frame, Func<TextPosition, int> index);

    Rect Bar(Rect caret);
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
        if (!frame.Placement.IsIdentity && frame.Placement.TryInvert(out var inverse)) point = inverse.Transform(point);

        var column = Column(frame, point.X - frame.Area.Inner.X);
        var (line, lineTop, first) = (-1, double.NegativeInfinity, -1);
        for (var index = 0; index < frame.Lines.Count; index++)
        {
            if (Math.Abs(frame.Shift(index) - column) >= 0.01) continue;
            var top = frame.Tops[index] - frame.Lines[index].Before;
            if (first < 0 || top < frame.Tops[first] - frame.Lines[first].Before) first = index;
            if (point.Y >= top && top > lineTop) (line, lineTop) = (index, top);
        }
        if (line < 0) line = Math.Max(first, 0);

        var typeset = frame.Lines[line];
        var (best, bestRow, bestColumn) = (typeset.Offset, double.MaxValue, double.MaxValue);
        for (var content = typeset.Offset; content <= frame.ContentEnd(line); content++)
        {
            var caret = CaretAt(frame, line, content);
            var row = point.Y < caret.Top ? caret.Top - point.Y : point.Y > caret.Bottom ? point.Y - caret.Bottom : 0;
            var across = Math.Abs(point.X - caret.X);
            if (row < bestRow - 0.01 || (Math.Abs(row - bestRow) <= 0.01 && across < bestColumn))
                (best, bestRow, bestColumn) = (content, row, across);
        }
        return new TextPosition(line, best - typeset.Offset);
    }

    private double Column(ComposedTextFrame frame, double x)
    {
        var shifts = Enumerable.Range(0, frame.Lines.Count).Select(frame.Shift).Distinct().Order().ToList();
        var chosen = shifts[0];
        foreach (var shift in shifts)
            if (x >= shift) chosen = shift;
        return chosen;
    }

    public IReadOnlyList<CaretRow> Rows(ComposedTextFrame frame, Func<TextPosition, int> index)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(index);
        if (frame.Stacked) return StackRows(frame, index);

        var rows = new List<(double Shift, double Top, CaretRow Row)>();
        for (var line = 0; line < frame.Lines.Count; line++)
        {
            var typeset = frame.Lines[line];
            var bands = typeset.Segments
                .GroupBy(segment => Math.Round(segment.Y, 1))
                .OrderBy(band => band.Key)
                .Select(band => (Y: band.Key, Start: band.Min(segment => segment.Start), End: band.Max(segment => segment.End)))
                .ToList();
            if (bands.Count == 0) bands.Add((0, typeset.Offset, typeset.Offset));
            var ordered = bands.OrderBy(band => band.Start).ToList();

            for (var band = 0; band < ordered.Count; band++)
            {
                var start = band == 0 ? typeset.Offset : ordered[band].Start;
                var last = band + 1 < ordered.Count ? Math.Min(ordered[band].End, ordered[band + 1].Start - 1) : frame.ContentEnd(line);
                var stops = new List<CaretStop>();
                for (var content = start; content <= Math.Max(start, last); content++)
                    stops.Add(new CaretStop(index(new TextPosition(line, content - typeset.Offset)), CaretAt(frame, line, content).X));
                rows.Add((frame.Shift(line), frame.Tops[line] + ordered[band].Y, new CaretRow(stops)));
            }
        }
        return [.. rows.OrderBy(row => row.Shift).ThenBy(row => row.Top).Select(row => row.Row)];
    }

    public Rect Bar(Rect caret) =>
        caret.Height <= 0 && caret.Width > 0
            ? new Rect(caret.X, caret.Y - 0.75, caret.Width, 1.5)
            : new Rect(caret.X - 0.75, caret.Y, 1.5, Math.Max(caret.Height, 4));

    private IReadOnlyList<CaretRow> StackRows(ComposedTextFrame frame, Func<TextPosition, int> index)
    {
        var rows = new List<(double Left, CaretRow Row)>();
        for (var line = 0; line < frame.Lines.Count; line++)
        {
            var typeset = frame.Lines[line];
            var stops = new List<CaretStop>();
            for (var content = typeset.Offset; content <= frame.ContentEnd(line); content++)
                stops.Add(new CaretStop(index(new TextPosition(line, content - typeset.Offset)), CaretAt(frame, line, content).Y));
            rows.Add((frame.Left(line), new CaretRow(stops)));
        }
        return [.. rows.OrderBy(row => row.Left).Select(row => row.Row)];
    }

    private Rect CaretAt(ComposedTextFrame frame, int line, int content)
    {
        if (frame.Stacked) return StackCaret(frame, line, content);
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

    private Rect StackCaret(ComposedTextFrame frame, int line, int content)
    {
        var typeset = frame.Lines[line];
        if (typeset.Segments.Count == 0) return new Rect(frame.Left(line), frame.Tops[line], frame.Area.Points(12), 0);

        var segment = SegmentFor(typeset, content);
        var origin = frame.Origin(line, segment);
        var below = content >= segment.End && segment.End > segment.Start;
        return new Rect(origin.X, below ? origin.Y + segment.Text.Height : origin.Y, Math.Max(1, segment.Text.WidthIncludingTrailingWhitespace), 0);
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
