using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record ChartSelectionLayout(CellRange Range, bool FirstRowIsHeader, bool FirstColumnIsCategory, bool SeriesInColumns)
{
    public int SeriesCount => SeriesInColumns
        ? Range.ColumnCount - (FirstColumnIsCategory ? 1 : 0)
        : Range.RowCount - (FirstRowIsHeader ? 1 : 0);

    public ChartSelectionLayout For(ChartType type) =>
        type is ChartType.Scatter or ChartType.ScatterWithLines or ChartType.Bubble && SeriesInColumns && Range.ColumnCount > 1
            ? this with { FirstColumnIsCategory = true }
            : this;

    public LegendPosition Legend(ChartType type) =>
        type is ChartType.Pie or ChartType.Doughnut || SeriesCount > 1 ? LegendPosition.Right : LegendPosition.None;
}

public interface IChartSelectionShape
{
    ChartSelectionLayout Detect(Worksheet sheet, CellRange selection);
}

public sealed class ChartSelectionShape(ISheetRegions regions) : IChartSelectionShape
{
    public ChartSelectionShape() : this(new SheetRegions())
    {
    }

    public ChartSelectionLayout Detect(Worksheet sheet, CellRange selection)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var range = selection.CellCount == 1 ? regions.CurrentRegion(sheet, selection.Start) : selection;
        var corner = sheet[range.Start].Value.IsEmpty && range.RowCount > 1 && range.ColumnCount > 1;
        var header = range.RowCount > 1 && (corner || Labels(sheet, range.Start.Row, range.Start.Row, range.Start.Column + (range.ColumnCount > 1 ? 1 : 0), range.End.Column));
        var categories = range.ColumnCount > 1 && (corner || Labels(sheet, range.Start.Row + (header ? 1 : 0), range.End.Row, range.Start.Column, range.Start.Column));
        var dataRows = range.RowCount - (header ? 1 : 0);
        var dataColumns = range.ColumnCount - (categories ? 1 : 0);
        return new ChartSelectionLayout(range, header, categories, dataRows >= dataColumns);
    }

    private static bool Labels(Worksheet sheet, int top, int bottom, int left, int right)
    {
        var values = new List<CellValue>();
        for (var row = top; row <= bottom; row++)
        {
            for (var column = left; column <= right; column++) values.Add(sheet[new CellAddress(row, column)].Value);
        }
        return values.All(value => value.IsEmpty || value.Kind == ValueKind.Text) && values.Any(value => value.Kind == ValueKind.Text);
    }
}
