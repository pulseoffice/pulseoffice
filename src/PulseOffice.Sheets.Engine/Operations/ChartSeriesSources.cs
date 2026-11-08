using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record ChartSourceReference(string Sheet, CellRange Range);

public interface IChartSourceReferences
{
    ChartSourceReference? Read(string? text);

    string Write(string sheet, CellRange range);

    bool Same(string? first, string? second);
}

public sealed class ChartSourceReferences : IChartSourceReferences
{
    public const string Broken = "#REF!";

    private readonly SheetReferenceText _text = new();

    public ChartSourceReference? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var reference = _text.Split(text);
        return reference.Sheet is { } sheet && (CellRange.TryParseA1(reference.Local, out var range) || Whole(reference.Local, out range))
            ? new ChartSourceReference(sheet, range)
            : null;
    }

    private bool Whole(string local, out CellRange range)
    {
        range = default;
        var parts = local.Split(':');
        if (parts.Length != 2) return false;
        if (CellAddress.TryParseA1(parts[0] + "1", out var left) && CellAddress.TryParseA1(parts[1] + "1", out var right) && parts.All(part => part.All(char.IsAsciiLetter)))
        {
            range = new CellRange(new CellAddress(0, left.Column), new CellAddress(CellAddress.MaxRows - 1, right.Column));
            return true;
        }
        if (parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit)) && CellAddress.TryParseA1("A" + parts[0], out var top) && CellAddress.TryParseA1("A" + parts[1], out var bottom))
        {
            range = new CellRange(new CellAddress(top.Row, 0), new CellAddress(bottom.Row, CellAddress.MaxColumns - 1));
            return true;
        }
        return false;
    }

    public string Write(string sheet, CellRange range) => _text.Join(sheet, range.ToString());

    public bool Same(string? first, string? second)
    {
        var left = Read(first);
        var right = Read(second);
        if (left is null || right is null) return left is null && right is null && string.IsNullOrWhiteSpace(first) == string.IsNullOrWhiteSpace(second);
        return left.Range == right.Range && string.Equals(left.Sheet, right.Sheet, StringComparison.CurrentCultureIgnoreCase);
    }
}

public interface IChartSourceLayout
{
    IReadOnlyList<ChartSeriesSource> Expand(ChartDefinition definition);

    ChartDefinition Settle(ChartDefinition compact, IReadOnlyList<ChartSeriesSource> read);
}

public sealed class ChartSourceLayout(IChartSourceReferences references) : IChartSourceLayout
{
    public ChartSourceLayout() : this(new ChartSourceReferences())
    {
    }

    public IReadOnlyList<ChartSeriesSource> Expand(ChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.HasSeriesSources) return definition.SeriesSources!;
        if (string.IsNullOrEmpty(definition.SourceSheet) || !CellRange.TryParseA1(definition.SourceRange, out var range)) return [];

        var fitted = Fit(definition, range);
        var dataTop = range.Start.Row + (fitted.FirstRowIsHeader ? 1 : 0);
        var dataLeft = range.Start.Column + (fitted.FirstColumnIsCategory ? 1 : 0);
        var sheet = definition.SourceSheet;
        var sources = new List<ChartSeriesSource>();

        if (fitted.SeriesInColumns)
        {
            var categories = fitted.FirstColumnIsCategory && dataTop <= range.End.Row
                ? Write(sheet, dataTop, range.End.Row, range.Start.Column, range.Start.Column)
                : null;
            for (var lane = dataLeft; lane <= range.End.Column; lane++)
                sources.Add(new ChartSeriesSource(
                    fitted.FirstRowIsHeader ? Write(sheet, range.Start.Row, range.Start.Row, lane, lane) : null,
                    dataTop <= range.End.Row ? Write(sheet, dataTop, range.End.Row, lane, lane) : null,
                    categories));
        }
        else
        {
            var categories = fitted.FirstRowIsHeader && dataLeft <= range.End.Column
                ? Write(sheet, range.Start.Row, range.Start.Row, dataLeft, range.End.Column)
                : null;
            for (var lane = dataTop; lane <= range.End.Row; lane++)
                sources.Add(new ChartSeriesSource(
                    fitted.FirstColumnIsCategory ? Write(sheet, lane, lane, range.Start.Column, range.Start.Column) : null,
                    dataLeft <= range.End.Column ? Write(sheet, lane, lane, dataLeft, range.End.Column) : null,
                    categories));
        }

        return sources;
    }

    public ChartDefinition Settle(ChartDefinition compact, IReadOnlyList<ChartSeriesSource> read)
    {
        ArgumentNullException.ThrowIfNull(compact);
        ArgumentNullException.ThrowIfNull(read);
        if (read.Count == 0) return compact with { SeriesSources = null };

        var expanded = Expand(compact with { SeriesSources = null });
        var matches = expanded.Count == read.Count && read.Select((source, index) => Matches(source, expanded[index])).All(same => same);
        return matches ? compact with { SeriesSources = null } : compact with { SeriesSources = read };
    }

    private bool Matches(ChartSeriesSource read, ChartSeriesSource expanded) =>
        references.Same(read.Values, expanded.Values)
        && (read.Categories is null || references.Same(read.Categories, expanded.Categories))
        && (read.Name is null || references.Same(read.Name, expanded.Name));

    private ChartDefinition Fit(ChartDefinition definition, CellRange range) => definition with
    {
        FirstColumnIsCategory = definition.FirstColumnIsCategory && (!definition.SeriesInColumns || range.ColumnCount > 1),
        FirstRowIsHeader = definition.FirstRowIsHeader && (definition.SeriesInColumns || range.RowCount > 1),
    };

    private string Write(string sheet, int top, int bottom, int left, int right) =>
        references.Write(sheet, new CellRange(new CellAddress(top, left), new CellAddress(bottom, right)));
}

internal sealed class SeriesSourceReader(IChartSourceReferences references, IChartSeriesNames names, IChartCellText cells, ISelectionWorkArea workArea)
{
    private const int MaximumPoints = 100_000;

    public ChartData Read(Workbook workbook, ChartDefinition definition, bool visibleOnly)
    {
        var sources = definition.SeriesSources!;
        var categorySource = sources.Select(source => source.Categories).FirstOrDefault(text => references.Read(text) is not null);
        var categories = categorySource is null ? null : Cells(workbook, categorySource, visibleOnly).Select(cell => cell.Text).ToList();

        var series = new List<ChartSeries>();
        foreach (var source in sources)
        {
            var values = source.Values is null ? [] : Cells(workbook, source.Values, visibleOnly).Select(cell => cell.Number).ToList();
            var name = definition.ExplicitSeriesName(series.Count) ?? Name(workbook, source.Name);
            series.Add(new ChartSeries(string.IsNullOrWhiteSpace(name) ? names.Fallback(series.Count + 1) : name, values)
            {
                ColorHex = definition.SeriesColor(series.Count),
            });
        }

        var length = series.Select(item => item.Values.Count).DefaultIfEmpty(0).Max();
        var labels = categories ?? [.. Enumerable.Range(1, length).Select(index => index.ToString(System.Globalization.CultureInfo.CurrentCulture))];
        return new ChartData(labels, series) { OrdinalCategories = categories is null };
    }

    private string? Name(Workbook workbook, string? text)
    {
        if (references.Read(text) is not { } reference || workbook.FindSheet(reference.Sheet) is not { } sheet) return null;
        return cells.Text(workbook, sheet[reference.Range.Start]);
    }

    private IEnumerable<(string Text, double Number)> Cells(Workbook workbook, string text, bool visibleOnly)
    {
        if (references.Read(text) is not { } reference || workbook.FindSheet(reference.Sheet) is not { } sheet) yield break;
        if ((workArea.Bound(reference.Range, null) ?? workArea.Bound(reference.Range, sheet.UsedRange())) is not { } range) yield break;

        var count = 0;
        foreach (var address in range.Cells())
        {
            if (visibleOnly && (sheet.Layout.IsRowHidden(address.Row) || sheet.Layout.IsColumnHidden(address.Column))) continue;
            if (++count > MaximumPoints) yield break;
            var cell = sheet[address];
            yield return (cells.Text(workbook, cell), cell.Value.Kind == Values.ValueKind.Number ? cell.Value.AsNumber() : double.NaN);
        }
    }
}
