using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IChartTableSources
{
    bool IsTable(string? text);

    bool ReadsTables(ChartDefinition chart);

    string? Resolve(string? text, Workbook workbook);

    ChartDefinition Flatten(ChartDefinition chart, Workbook workbook);

    ChartDefinition Rename(ChartDefinition chart, IReadOnlyList<TableRename> renames);

    ChartTableReference? Describe(string? text);

    string Reference(string table, string column);

    IReadOnlyList<string> MissingTables(ChartDefinition chart, Workbook workbook);

    IReadOnlyList<ChartTableColumns> Tables(Workbook workbook);
}

public sealed record ChartTableReference(string Table, string? Column);

public sealed record ChartTableColumns(string Name, IReadOnlyList<string> Columns);

public sealed class ChartTableSources(ITableReferenceResolution resolution, IChartSourceReferences references) : IChartTableSources
{
    private readonly FormulaParser _parser = new();

    public ChartTableSources() : this(new TableReferenceResolution(), new ChartSourceReferences()) { }

    public bool IsTable(string? text) => Table(text) is not null;

    public bool ReadsTables(ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        return chart.SeriesSources is { } sources
            && sources.Any(source => IsTable(source.Name) || IsTable(source.Values) || IsTable(source.Categories));
    }

    public string? Resolve(string? text, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (Table(text) is not { } node) return text;
        return resolution.Resolve(workbook, node, new SheetCell(-1, CellAddress.First)) is { } target
            ? references.Write(workbook.Sheets[target.SheetIndex].Name, target.Range)
            : ChartSourceReferences.Broken;
    }

    public ChartDefinition Flatten(ChartDefinition chart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (!ReadsTables(chart)) return chart;
        return chart with
        {
            SeriesSources = [.. chart.SeriesSources!.Select(source =>
                new ChartSeriesSource(Resolve(source.Name, workbook), Resolve(source.Values, workbook), Resolve(source.Categories, workbook)))],
        };
    }

    public ChartDefinition Rename(ChartDefinition chart, IReadOnlyList<TableRename> renames)
    {
        ArgumentNullException.ThrowIfNull(renames);
        if (!ReadsTables(chart) || renames.Count == 0) return chart;
        var renamed = chart.SeriesSources!.Select(source =>
            new ChartSeriesSource(Renamed(source.Name, renames), Renamed(source.Values, renames), Renamed(source.Categories, renames))).ToList();
        return renamed.SequenceEqual(chart.SeriesSources!) ? chart : chart with { SeriesSources = renamed };
    }

    public ChartTableReference? Describe(string? text) =>
        Table(text) is { } node ? new ChartTableReference(node.Table, node.FirstColumn) : null;

    public string Reference(string table, string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(column);
        var escaped = new System.Text.StringBuilder(column.Length);
        foreach (var character in column)
        {
            if (character is '[' or ']' or '#' or '\'') escaped.Append('\'');
            escaped.Append(character);
        }
        return table + "[" + escaped + "]";
    }

    public IReadOnlyList<string> MissingTables(ChartDefinition chart, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(workbook);
        if (!ReadsTables(chart)) return [];
        return [.. chart.SeriesSources!
            .SelectMany(source => new[] { source.Name, source.Values, source.Categories })
            .Select(Table).OfType<TableReferenceNode>()
            .Select(node => node.Table)
            .Where(name => workbook.DatabaseRanges.Find(name) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<ChartTableColumns> Tables(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return [.. workbook.DatabaseRanges.All
            .Where(table => table.SheetIndex >= 0 && table.SheetIndex < workbook.Sheets.Count)
            .OrderBy(table => table.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(table => new ChartTableColumns(table.Name, resolution.Columns(workbook.Sheets[table.SheetIndex], table)))];
    }

    private string? Renamed(string? text, IReadOnlyList<TableRename> renames)
    {
        if (!IsTable(text)) return text;
        var renamer = new TableReferenceRenamer(renames);
        return renamer.Rewrite("=" + text!.Trim().TrimStart('=')) is { } rewritten && renamer.Changed ? rewritten[1..] : text;
    }

    private TableReferenceNode? Table(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('[', StringComparison.Ordinal)) return null;
        try
        {
            return _parser.Parse("=" + text.Trim().TrimStart('=')) as TableReferenceNode;
        }
        catch (FormulaSyntaxException)
        {
            return null;
        }
    }
}
