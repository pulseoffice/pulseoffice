using Avalonia;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ShapeTextColumn(Rect Clip, Point Origin);

public readonly record struct ShapeTextLine(double Top, double Bottom);

public interface IShapeTextColumns
{
    double Width(Rect inner, int columns, double spacing);

    IReadOnlyList<ShapeTextColumn> Place(Rect inner, int columns, double spacing, IReadOnlyList<ShapeTextLine> lines);
}

public interface IShapeTextLines
{
    IReadOnlyList<ShapeTextLine> Measure(FormattedText text, string content);
}

public sealed class FormattedShapeTextLines : IShapeTextLines
{
    private const int ProbeEveryCharacterUpTo = 256;

    public IReadOnlyList<ShapeTextLine> Measure(FormattedText text, string content)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(content);
        var lines = new SortedDictionary<double, double>();
        if (content.Length > ProbeEveryCharacterUpTo) ProbeLines(text, content.Length, lines);
        else
            for (var index = 0; index < content.Length; index++)
                if (Probe(text, index) is { } bounds) Add(lines, bounds);

        if (lines.Count == 0) return [new ShapeTextLine(0, Math.Max(1, text.Height))];
        return Merged(lines);
    }

    private void ProbeLines(FormattedText text, int length, SortedDictionary<double, double> lines)
    {
        var index = 0;
        while (index < length)
        {
            if (Probe(text, index) is not { } first)
            {
                index++;
                continue;
            }
            Add(lines, first);
            var top = Math.Round(first.Top, 3);
            var low = index;
            var high = length - 1;
            var step = 1;
            while (low + step <= high)
            {
                if (SameLine(text, low + step, top, lines)) { low += step; step *= 2; }
                else { high = low + step - 1; break; }
            }
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (SameLine(text, middle, top, lines)) low = middle;
                else high = middle - 1;
            }
            index = low + 1;
        }
    }

    private bool SameLine(FormattedText text, int index, double top, SortedDictionary<double, double> lines)
    {
        if (Probe(text, index) is not { } bounds) return true;
        if (Math.Round(bounds.Top, 3) != top) return false;
        Add(lines, bounds);
        return true;
    }

    private Rect? Probe(FormattedText text, int index) =>
        text.BuildHighlightGeometry(default, index, 1) is { Bounds: { Height: > 0 } bounds } ? bounds : null;

    private void Add(SortedDictionary<double, double> lines, Rect bounds)
    {
        var top = Math.Round(bounds.Top, 3);
        lines[top] = Math.Max(lines.GetValueOrDefault(top), bounds.Bottom);
    }

    private IReadOnlyList<ShapeTextLine> Merged(SortedDictionary<double, double> lines)
    {
        var merged = new List<ShapeTextLine>(lines.Count);
        foreach (var (top, bottom) in lines)
        {
            if (merged.Count > 0 && top < merged[^1].Bottom - 0.5)
            {
                merged[^1] = merged[^1] with { Bottom = Math.Max(merged[^1].Bottom, bottom) };
                continue;
            }
            merged.Add(new ShapeTextLine(merged.Count > 0 ? Math.Min(top, merged[^1].Bottom) : Math.Min(0, top), bottom));
        }
        return merged;
    }
}

public sealed class ShapeTextColumns : IShapeTextColumns
{
    public double Width(Rect inner, int columns, double spacing)
    {
        var count = Math.Max(1, columns);
        var gap = Gap(inner, count, spacing);
        return Math.Max(1, (inner.Width - gap * (count - 1)) / count);
    }

    public IReadOnlyList<ShapeTextColumn> Place(Rect inner, int columns, double spacing, IReadOnlyList<ShapeTextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var count = Math.Max(1, columns);
        var width = Width(inner, count, spacing);
        var pitch = width + Gap(inner, count, spacing);
        var room = Math.Max(1, inner.Height);
        var placed = new List<ShapeTextColumn>(count);
        var next = 0;
        while (placed.Count < count && (next < lines.Count || placed.Count == 0))
        {
            var column = placed.Count;
            var top = next < lines.Count ? lines[next].Top : 0;
            var bottom = next < lines.Count ? lines[next].Bottom : room;
            var last = next;
            if (column == count - 1) last = Math.Max(next, lines.Count - 1);
            else
                while (last + 1 < lines.Count && lines[last + 1].Bottom - top <= room + 0.0001) last++;
            if (last < lines.Count) bottom = lines[last].Bottom;
            next = last + 1;

            var left = inner.X + column * pitch;
            var height = column == count - 1 ? Math.Max(room, bottom - top) : Math.Max(bottom - top, 1);
            placed.Add(new ShapeTextColumn(new Rect(left, inner.Y, width, height), new Point(left, inner.Y - top)));
        }
        return placed;
    }

    private double Gap(Rect inner, int count, double spacing) =>
        count > 1 ? Math.Clamp(spacing, 0, Math.Max(0, inner.Width / 2)) : 0;
}
