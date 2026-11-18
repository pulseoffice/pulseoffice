using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotChartLinks
{
    ChartDefinition Derive(ChartDefinition chart, PivotDefinition pivot, PivotTable table);

    int Follow(Workbook workbook, string linkedName, PivotDefinition pivot, PivotTable table);

    int Detach(Workbook workbook, PivotDefinition pivot);
}

public sealed class PivotChartLinks(IChartSourceReferences references, IChartDataExtractor extractor) : IPivotChartLinks
{
    public PivotChartLinks() : this(new ChartSourceReferences(), new ChartDataExtractor())
    {
    }

    public ChartDefinition Derive(ChartDefinition chart, PivotDefinition pivot, PivotTable table)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(pivot);
        ArgumentNullException.ThrowIfNull(table);

        var origin = pivot.Destination;
        var rowFields = pivot.In(PivotArea.Row).Count();
        var columnFields = pivot.In(PivotArea.Column).Count();
        var headerRows = Math.Max(columnFields, 1);
        var rowHeaderColumns = Math.Max(rowFields, 1);

        var leafRows = Leaves(table.Rows, rowFields);
        var leafColumns = Leaves([.. table.Columns.Select(column => column.Node)], columnFields);
        if (leafRows.Count == 0 || leafColumns.Count == 0) return chart;

        var categoryColumn = pivot.Layout == PivotLayout.Tabular ? origin.Column + rowHeaderColumns - 1 : origin.Column;
        var lastRow = origin.Row + headerRows + leafRows[^1];
        var nameRow = origin.Row + headerRows - 1;
        var sheet = pivot.DestinationSheet;
        var runs = Runs(leafRows);

        var categories = Lane(sheet, runs, origin.Row + headerRows, categoryColumn);
        var sources = leafColumns.Select(index =>
        {
            var column = origin.Column + rowHeaderColumns + index;
            var names = Enumerable.Range(origin.Row, headerRows)
                .Select(row => new ChartSourceReference(sheet, new CellRange(new CellAddress(row, column), new CellAddress(row, column))))
                .ToList();
            return new ChartSeriesSource(
                names.Count == 1 ? references.Write(sheet, names[0].Range) : references.Write(names),
                Lane(sheet, runs, origin.Row + headerRows, column),
                categories);
        }).ToList();

        var span = new CellRange(
            new CellAddress(nameRow, categoryColumn),
            new CellAddress(lastRow, origin.Column + rowHeaderColumns + leafColumns[^1]));

        return chart with
        {
            InlineData = null,
            SourceSheet = sheet,
            SourceRange = span.ToString(),
            SeriesSources = sources,
            SeriesInColumns = true,
            FirstRowIsHeader = true,
            FirstColumnIsCategory = true,
            PivotSource = (chart.PivotSource ?? new ChartPivotSource(pivot.Name)) with { PivotName = pivot.Name },
        };
    }

    public int Follow(Workbook workbook, string linkedName, PivotDefinition pivot, PivotTable table)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return Rewrite(workbook, linkedName, chart => Derive(chart, pivot, table));
    }

    public int Detach(Workbook workbook, PivotDefinition pivot)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return Rewrite(workbook, pivot.Name, chart => chart with
        {
            InlineData = extractor.Extract(workbook, chart),
            SourceSheet = string.Empty,
            SourceRange = string.Empty,
            SeriesSources = null,
            PivotSource = null,
        });
    }

    private static int Rewrite(Workbook workbook, string linkedName, Func<ChartDefinition, ChartDefinition> rewrite)
    {
        var count = 0;
        foreach (var sheet in workbook.Sheets)
        {
            for (var index = 0; index < sheet.Objects.Count; index++)
            {
                if (sheet.Objects[index].Content is not ChartDefinition { PivotSource: { } link } chart
                    || !string.Equals(link.PivotName, linkedName, StringComparison.OrdinalIgnoreCase)) continue;
                sheet.Objects[index] = sheet.Objects[index] with { Content = rewrite(chart) };
                count++;
            }
        }
        return count;
    }

    private string Lane(string sheet, IReadOnlyList<(int First, int Last)> runs, int top, int column)
    {
        var areas = runs
            .Select(run => new ChartSourceReference(sheet, new CellRange(new CellAddress(top + run.First, column), new CellAddress(top + run.Last, column))))
            .ToList();
        return areas.Count == 1 ? references.Write(sheet, areas[0].Range) : references.Write(areas);
    }

    private static List<(int First, int Last)> Runs(IReadOnlyList<int> rows)
    {
        var runs = new List<(int First, int Last)>();
        foreach (var row in rows)
        {
            if (runs.Count > 0 && runs[^1].Last == row - 1) runs[^1] = (runs[^1].First, row);
            else runs.Add((row, row));
        }
        return runs;
    }

    private static List<int> Leaves(IReadOnlyList<PivotNode> nodes, int fieldCount)
    {
        if (fieldCount == 0) return [.. Enumerable.Range(0, nodes.Count)];
        return [.. Enumerable.Range(0, nodes.Count).Where(index => nodes[index].Kind == PivotNodeKind.Item && nodes[index].Path.Count == fieldCount)];
    }
}
