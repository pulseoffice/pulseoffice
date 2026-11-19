using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed record SlideElementPlacement(double Left, double Top, double Width, double Height, double PointsPerTwip, double RotationDegrees);

public sealed class SlideTableCellLocator(
    DataTableObject table, SlideElementPlacement placement, IReadOnlyDictionary<(int Row, int Column), PrintTag> cells, TableGeometry geometry) : IPrintSegmentLocator
{
    private readonly TableCellIndex _index = new(table);

    public PrintTag? At(double xPoints, double yPoints)
    {
        if (!(placement.Width > 0) || !(placement.PointsPerTwip > 0)) return null;
        var (x, y) = Unrotated(xPoints, yPoints);
        if (geometry.CellAt(table, (x - placement.Left) / placement.Width, (y - placement.Top) / (placement.PointsPerTwip * Twip.PerPoint)) is not { } cell)
            return null;
        var (row, column) = _index.Cover(cell.Row, cell.Column) is { } merge ? (merge.Row, merge.Column) : (cell.Row, cell.Column);
        return cells.GetValueOrDefault((row, column));
    }

    private (double X, double Y) Unrotated(double x, double y)
    {
        if (placement.RotationDegrees == 0 || !double.IsFinite(placement.RotationDegrees)) return (x, y);
        var radians = placement.RotationDegrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var centreX = placement.Left + placement.Width / 2;
        var centreY = placement.Top + placement.Height / 2;
        var (dx, dy) = (x - centreX, y - centreY);
        return (centreX + dx * cos + dy * sin, centreY - dx * sin + dy * cos);
    }
}
