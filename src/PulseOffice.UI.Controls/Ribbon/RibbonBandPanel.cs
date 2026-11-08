using Avalonia;
using Avalonia.Controls;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class RibbonBandPanel(IRibbonBandLayout layout, RibbonBandSpec band, double gap) : Panel
{
    private int _firstRow;

    public int FirstRow
    {
        get => _firstRow;
        set
        {
            if (_firstRow == value) return;
            _firstRow = value;
            InvalidateArrange();
        }
    }

    public RibbonBandMetrics Metrics(double height) => layout.Measure(height, band, Children.Count, gap);

    protected override Size MeasureOverride(Size availableSize)
    {
        var metrics = Metrics(availableSize.Height);
        var tile = new Size(metrics.TileWidth, metrics.TileHeight);

        foreach (var child in Children) child.Measure(tile);

        return new Size(metrics.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var metrics = Metrics(finalSize.Height);
        var first = layout.Clamp(_firstRow, metrics);

        for (var index = 0; index < Children.Count; index++)
        {
            var shown = layout.Shows(index, first, metrics);
            Children[index].IsVisible = shown;
            if (!shown) continue;

            var column = index % metrics.Columns;
            var row = index / metrics.Columns - first;

            Children[index].Arrange(new Rect(
                column * (metrics.TileWidth + metrics.Gap),
                row * (metrics.TileHeight + metrics.Gap),
                metrics.TileWidth,
                metrics.TileHeight));
        }

        return finalSize;
    }
}
