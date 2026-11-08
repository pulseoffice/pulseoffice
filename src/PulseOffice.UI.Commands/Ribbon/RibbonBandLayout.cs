namespace PulseOffice.UI.Commands;

public sealed record RibbonBandMetrics(double TileWidth, double TileHeight, int Columns, int VisibleRows, int TotalRows, double Gap)
{
    public double Width => Columns * TileWidth + Math.Max(0, Columns - 1) * Gap;

    public int LastFirstRow => Math.Max(0, TotalRows - VisibleRows);
}

public interface IRibbonBandLayout
{
    RibbonBandMetrics Measure(double height, RibbonBandSpec band, int count, double gap);

    int Clamp(int firstRow, RibbonBandMetrics metrics);

    int Reveal(int index, int firstRow, RibbonBandMetrics metrics);

    bool Shows(int index, int firstRow, RibbonBandMetrics metrics);
}

public sealed class RibbonBandLayout : IRibbonBandLayout
{
    public RibbonBandMetrics Measure(double height, RibbonBandSpec band, int count, double gap)
    {
        ArgumentNullException.ThrowIfNull(band);

        var rows = Math.Max(1, band.Rows);
        var columns = Math.Max(1, band.Columns);
        var spacing = Math.Max(0, gap);
        var usable = double.IsFinite(height) ? Math.Max(0, height - spacing * (rows - 1)) : 0;
        var tileHeight = usable / rows;
        var tileWidth = tileHeight * (band.CurrentAspect > 0 ? band.CurrentAspect : 1);
        var total = Math.Max(1, (int)Math.Ceiling(Math.Max(0, count) / (double)columns));

        return new RibbonBandMetrics(tileWidth, tileHeight, columns, Math.Min(rows, total), total, spacing);
    }

    public int Clamp(int firstRow, RibbonBandMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return Math.Clamp(firstRow, 0, metrics.LastFirstRow);
    }

    public int Reveal(int index, int firstRow, RibbonBandMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (index < 0) return Clamp(firstRow, metrics);

        var row = index / metrics.Columns;
        if (row < firstRow) return Clamp(row, metrics);
        if (row >= firstRow + metrics.VisibleRows) return Clamp(row - metrics.VisibleRows + 1, metrics);
        return Clamp(firstRow, metrics);
    }

    public bool Shows(int index, int firstRow, RibbonBandMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var row = index / metrics.Columns;
        return index >= 0 && row >= firstRow && row < firstRow + metrics.VisibleRows;
    }
}
