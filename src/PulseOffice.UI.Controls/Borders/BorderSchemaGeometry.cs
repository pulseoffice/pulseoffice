using Avalonia;
using PulseOffice.Document.Borders;

namespace PulseOffice.UI.Controls.Borders;

public sealed record BorderSegment(BorderPosition Position, Point Start, Point End);

public interface IBorderSchemaGeometry
{
    IReadOnlyList<BorderSegment> Segments(Rect area, BorderFrameScope scope);

    BorderPosition? HitTest(Rect area, BorderFrameScope scope, Point point);
}

public sealed class BorderSchemaGeometry : IBorderSchemaGeometry
{
    private const double Tolerance = 9;

    public IReadOnlyList<BorderSegment> Segments(Rect area, BorderFrameScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var middleX = area.X + area.Width / 2;
        var middleY = area.Y + area.Height / 2;
        var segments = new List<BorderSegment>
        {
            new(BorderPosition.Top, area.TopLeft, area.TopRight),
            new(BorderPosition.Bottom, area.BottomLeft, area.BottomRight),
            new(BorderPosition.Left, area.TopLeft, area.BottomLeft),
            new(BorderPosition.Right, area.TopRight, area.BottomRight),
        };
        if (scope.InsideHorizontal) segments.Add(new(BorderPosition.InsideHorizontal, new Point(area.X, middleY), new Point(area.Right, middleY)));
        if (scope.InsideVertical) segments.Add(new(BorderPosition.InsideVertical, new Point(middleX, area.Y), new Point(middleX, area.Bottom)));
        if (scope.Diagonals)
        {
            segments.Add(new(BorderPosition.DiagonalDown, area.TopLeft, area.BottomRight));
            segments.Add(new(BorderPosition.DiagonalUp, area.BottomLeft, area.TopRight));
        }
        return segments;
    }

    public BorderPosition? HitTest(Rect area, BorderFrameScope scope, Point point)
    {
        var inflated = area.Inflate(Tolerance);
        if (!inflated.Contains(point)) return null;
        var best = Segments(area, scope)
            .Select(segment => (segment.Position, Distance: Distance(point, segment.Start, segment.End)))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Position)
            .First();
        return best.Distance <= Tolerance ? best.Position : null;
    }

    private double Distance(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = dx * dx + dy * dy;
        var t = length <= 0 ? 0 : Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / length, 0, 1);
        var x = start.X + t * dx - point.X;
        var y = start.Y + t * dy - point.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
