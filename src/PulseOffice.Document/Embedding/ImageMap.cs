namespace PulseOffice.Document.Embedding;

public enum ImageMapShape { Rectangle, Circle, Polygon }

public sealed record ImageMapArea
{
    public required string Id { get; init; }

    public ImageMapShape Shape { get; init; } = ImageMapShape.Rectangle;

    public IReadOnlyList<(double X, double Y)> Points { get; init; } = [];

    public string Target { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Contains(double x, double y)
    {
        if (Points.Count == 0) return false;

        return Shape switch
        {
            ImageMapShape.Rectangle => InRectangle(x, y),
            ImageMapShape.Circle => InCircle(x, y),
            _ => InPolygon(x, y),
        };
    }

    private bool InRectangle(double x, double y)
    {
        if (Points.Count < 2) return false;

        var left = Math.Min(Points[0].X, Points[1].X);
        var right = Math.Max(Points[0].X, Points[1].X);
        var top = Math.Min(Points[0].Y, Points[1].Y);
        var bottom = Math.Max(Points[0].Y, Points[1].Y);

        return x >= left && x <= right && y >= top && y <= bottom;
    }

    private bool InCircle(double x, double y)
    {
        if (Points.Count < 2) return false;

        var radius = Math.Sqrt(
            Math.Pow(Points[1].X - Points[0].X, 2) + Math.Pow(Points[1].Y - Points[0].Y, 2));

        var distance = Math.Sqrt(
            Math.Pow(x - Points[0].X, 2) + Math.Pow(y - Points[0].Y, 2));

        return distance <= radius;
    }

    private bool InPolygon(double x, double y)
    {
        var inside = false;

        for (int current = 0, previous = Points.Count - 1; current < Points.Count; previous = current++)
        {
            var (currentX, currentY) = Points[current];
            var (previousX, previousY) = Points[previous];

            if (currentY > y == previousY > y) continue;

            var crossing = ((previousX - currentX) * (y - currentY) / (previousY - currentY)) + currentX;

            if (x < crossing) inside = !inside;
        }

        return inside;
    }
}

public sealed class ImageMap
{
    private readonly List<ImageMapArea> _areas = [];

    public IReadOnlyList<ImageMapArea> Areas => _areas;

    public bool IsEmpty => _areas.Count == 0;

    public void Add(ImageMapArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        _areas.Add(area);
    }

    public bool Remove(string id) => _areas.RemoveAll(area => area.Id == id) > 0;

    public void Replace(IEnumerable<ImageMapArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        _areas.Clear();
        _areas.AddRange(areas);
    }

    public ImageMapArea? At(double x, double y) =>
        _areas.LastOrDefault(area => area.Contains(x, y));
}
