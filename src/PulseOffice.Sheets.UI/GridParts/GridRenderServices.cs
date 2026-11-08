using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Charts;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridRenderServices
{
    public IChartDataExtractor ChartData { get; set; } = new ChartDataExtractor();

    public ChartRenderer ChartRenderer { get; set; } = new();

    public ICellOverflow Overflow { get; set; } = new CellOverflow();

    public ICellTextPlacement TextPlacement { get; set; } = new CellTextPlacement();

    public ISparklinePainter SparklinePainter { get; set; } = new SparklinePainter();

    public ICellTextComposer TextComposer { get; set; } = new CellTextComposer();

    public Painting.IGridPaints Paints { get; set; } = new Painting.GridPaints();

    public ICellNavigator Navigator { get; set; } = new CellNavigator();
}
