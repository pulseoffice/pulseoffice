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
    private const int ProbeEveryCharacterUpTo = 4000;

    public IReadOnlyList<ShapeTextLine> Measure(FormattedText text, string content)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(content);
        var length = content.Length;
        var lines = new SortedDictionary<double, double>();
        for (var index = 0; index < length; index++)
        {
            if (length > ProbeEveryCharacterUpTo && index > 0 && !char.IsWhiteSpace(content[index - 1])) continue;
            if (text.BuildHighlightGeometry(default, index, 1) is not { } geometry) continue;
            var bounds = geometry.Bounds;
            if (bounds.Height <= 0) continue;
            var top = Math.Round(bounds.Top, 3);
            lines[top] = Math.Max(lines.GetValueOrDefault(top), bounds.Bottom);
        }

        if (lines.Count == 0) return [new ShapeTextLine(0, Math.Max(1, text.Height))];
        return Merged(lines);
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
