using Avalonia;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ShapeTextColumn(Rect Clip, Point Origin);

public interface IShapeTextColumns
{
    double Width(Rect inner, int columns, double spacing);

    IReadOnlyList<ShapeTextColumn> Place(Rect inner, int columns, double spacing, double lineHeight, double textHeight);
}

public sealed class ShapeTextColumns : IShapeTextColumns
{
    public double Width(Rect inner, int columns, double spacing)
    {
        var count = Math.Max(1, columns);
        var gap = Gap(inner, count, spacing);
        return Math.Max(1, (inner.Width - gap * (count - 1)) / count);
    }

    public IReadOnlyList<ShapeTextColumn> Place(Rect inner, int columns, double spacing, double lineHeight, double textHeight)
    {
        var count = Math.Max(1, columns);
        var width = Width(inner, count, spacing);
        var pitch = width + Gap(inner, count, spacing);
        var line = Math.Max(1, lineHeight);
        var height = Math.Max(line, Math.Floor(Math.Max(0, inner.Height) / line) * line);
        var used = Math.Clamp((int)Math.Ceiling(Math.Max(0, textHeight) / height - 0.0001), 1, count);
        var placed = new List<ShapeTextColumn>(used);
        for (var column = 0; column < used; column++)
        {
            var left = inner.X + column * pitch;
            var last = column == used - 1 && column == count - 1;
            var clip = new Rect(left, inner.Y, width, last ? Math.Max(height, textHeight - column * height) : height);
            placed.Add(new ShapeTextColumn(clip, new Point(left, inner.Y - column * height)));
        }
        return placed;
    }

    private double Gap(Rect inner, int count, double spacing) =>
        count > 1 ? Math.Clamp(spacing, 0, Math.Max(0, inner.Width / 2)) : 0;
}
