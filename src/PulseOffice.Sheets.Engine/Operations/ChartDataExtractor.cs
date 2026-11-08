using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IChartDataExtractor
{
    ChartData Extract(Workbook workbook, ChartDefinition definition, bool visibleOnly = false);
}

public interface IChartSeriesNames
{
    string Fallback(int number);
}

public sealed class CanonicalSeriesNames : IChartSeriesNames
{
    public string Fallback(int number) =>
        "Series " + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public interface IChartCellText
{
    string Text(Workbook workbook, Cell cell);
}

public sealed class RawChartCellText : IChartCellText
{
    public string Text(Workbook workbook, Cell cell) => cell.Value.ToString();
}

public sealed class PresentedChartCellText(PulseOffice.Sheets.Formatting.ICellPresenter presenter) : IChartCellText
{
    private const double UnboundedWidth = 1000;

    public string Text(Workbook workbook, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Value.Kind == ValueKind.Text ? cell.Value.ToString() : presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnboundedWidth).Text;
    }
}

public sealed class ChartDataExtractor(IChartSeriesNames names, IChartCellText cells, ISelectionWorkArea workArea) : IChartDataExtractor
{
    public ChartDataExtractor() : this(new CanonicalSeriesNames())
    {
    }

    public ChartDataExtractor(IChartSeriesNames names) : this(names, new RawChartCellText())
    {
    }

    public ChartDataExtractor(IChartSeriesNames names, IChartCellText cells) : this(names, cells, new SelectionWorkArea())
    {
    }

    private readonly SeriesLayout _columns = new ColumnSeriesLayout();
    private readonly SeriesLayout _rows = new RowSeriesLayout();
    private readonly SeriesSourceReader _sources = new(new ChartSourceReferences(), names, cells, workArea);

    public ChartData Extract(Workbook workbook, ChartDefinition definition, bool visibleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.HasSeriesSources) return _sources.Read(workbook, definition, visibleOnly);
        if (workbook.FindSheet(definition.SourceSheet) is not { } sheet) return ChartData.Empty;
        if (!CellRange.TryParseA1(definition.SourceRange, out var whole)) return ChartData.Empty;
        if ((workArea.Bound(whole, null) ?? workArea.Bound(whole, sheet.UsedRange())) is not { } range) return ChartData.Empty;

        var layout = definition.SeriesInColumns ? _columns : _rows;
        return layout.Read(new SheetGrid(workbook, sheet, visibleOnly, cells), names, range, layout.Fit(definition, range));
    }
}

internal sealed class SheetGrid(Workbook workbook, Worksheet sheet, bool visibleOnly, IChartCellText cells)
{
    public bool RowHidden(int row) => visibleOnly && sheet.Layout.IsRowHidden(row);

    public bool ColumnHidden(int column) => visibleOnly && sheet.Layout.IsColumnHidden(column);

    public string Text(int row, int column) => cells.Text(workbook, sheet[new CellAddress(row, column)]);

    public double Number(int row, int column)
    {
        var value = sheet[new CellAddress(row, column)].Value;
        return value.Kind == ValueKind.Number ? value.AsNumber() : double.NaN;
    }
}

internal abstract class SeriesLayout
{
    public ChartData Read(SheetGrid grid, IChartSeriesNames names, CellRange range, ChartDefinition definition)
    {
        var firstValue = FirstValue(range, definition);
        var indices = Enumerable.Range(firstValue, Math.Max(0, LastValue(range) - firstValue + 1))
            .Where(index => !IndexHidden(grid, index)).ToList();
        var categories = indices.Select(index => Category(grid, range, definition, index, firstValue)).ToList();

        var series = new List<ChartSeries>();
        for (var lane = FirstSeries(range, definition); lane <= LastSeries(range); lane++)
        {
            if (LaneHidden(grid, lane)) continue;
            var values = indices.Select(index => Value(grid, lane, index)).ToList();
            var name = definition.ExplicitSeriesName(series.Count) ?? SeriesName(grid, range, definition, lane);

            series.Add(new ChartSeries(string.IsNullOrWhiteSpace(name) ? names.Fallback(series.Count + 1) : name, values)
            {
                ColorHex = definition.SeriesColor(series.Count),
            });
        }

        return new ChartData(categories, series) { OrdinalCategories = !CategoriesFromCells(definition) };
    }

    public abstract ChartDefinition Fit(ChartDefinition definition, CellRange range);

    protected abstract bool CategoriesFromCells(ChartDefinition definition);

    protected string Ordinal(int index, int first) =>
        (index - first + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);

    protected abstract int FirstValue(CellRange range, ChartDefinition definition);
    protected abstract int LastValue(CellRange range);
    protected abstract int FirstSeries(CellRange range, ChartDefinition definition);
    protected abstract int LastSeries(CellRange range);
    protected abstract bool IndexHidden(SheetGrid grid, int index);
    protected abstract bool LaneHidden(SheetGrid grid, int lane);
    protected abstract double Value(SheetGrid grid, int lane, int index);
    protected abstract string Category(SheetGrid grid, CellRange range, ChartDefinition definition, int index, int first);
    protected abstract string SeriesName(SheetGrid grid, CellRange range, ChartDefinition definition, int lane);
}

internal sealed class ColumnSeriesLayout : SeriesLayout
{
    public override ChartDefinition Fit(ChartDefinition definition, CellRange range) =>
        definition.FirstColumnIsCategory && range.ColumnCount < 2 ? definition with { FirstColumnIsCategory = false } : definition;

    protected override int FirstValue(CellRange range, ChartDefinition definition) => range.Start.Row + (definition.FirstRowIsHeader ? 1 : 0);
    protected override int LastValue(CellRange range) => range.End.Row;
    protected override int FirstSeries(CellRange range, ChartDefinition definition) => range.Start.Column + (definition.FirstColumnIsCategory ? 1 : 0);
    protected override int LastSeries(CellRange range) => range.End.Column;
    protected override bool IndexHidden(SheetGrid grid, int index) => grid.RowHidden(index);
    protected override bool LaneHidden(SheetGrid grid, int lane) => grid.ColumnHidden(lane);
    protected override double Value(SheetGrid grid, int lane, int index) => grid.Number(index, lane);

    protected override bool CategoriesFromCells(ChartDefinition definition) => definition.FirstColumnIsCategory;

    protected override string Category(SheetGrid grid, CellRange range, ChartDefinition definition, int index, int first) =>
        definition.FirstColumnIsCategory ? grid.Text(index, range.Start.Column) : Ordinal(index, first);

    protected override string SeriesName(SheetGrid grid, CellRange range, ChartDefinition definition, int lane) =>
        definition.FirstRowIsHeader ? grid.Text(range.Start.Row, lane) : CellAddress.ColumnName(lane);
}

internal sealed class RowSeriesLayout : SeriesLayout
{
    public override ChartDefinition Fit(ChartDefinition definition, CellRange range) =>
        definition.FirstRowIsHeader && range.RowCount < 2 ? definition with { FirstRowIsHeader = false } : definition;

    protected override int FirstValue(CellRange range, ChartDefinition definition) => range.Start.Column + (definition.FirstColumnIsCategory ? 1 : 0);
    protected override int LastValue(CellRange range) => range.End.Column;
    protected override int FirstSeries(CellRange range, ChartDefinition definition) => range.Start.Row + (definition.FirstRowIsHeader ? 1 : 0);
    protected override int LastSeries(CellRange range) => range.End.Row;
    protected override bool IndexHidden(SheetGrid grid, int index) => grid.ColumnHidden(index);
    protected override bool LaneHidden(SheetGrid grid, int lane) => grid.RowHidden(lane);
    protected override double Value(SheetGrid grid, int lane, int index) => grid.Number(lane, index);

    protected override bool CategoriesFromCells(ChartDefinition definition) => definition.FirstRowIsHeader;

    protected override string Category(SheetGrid grid, CellRange range, ChartDefinition definition, int index, int first) =>
        definition.FirstRowIsHeader ? grid.Text(range.Start.Row, index) : Ordinal(index, first);

    protected override string SeriesName(SheetGrid grid, CellRange range, ChartDefinition definition, int lane) =>
        definition.FirstColumnIsCategory
            ? grid.Text(lane, range.Start.Column)
            : (lane + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
}
