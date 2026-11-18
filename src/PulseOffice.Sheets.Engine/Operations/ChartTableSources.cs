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
}

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
