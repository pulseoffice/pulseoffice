using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class ChartSeriesShift(StructureChange change)
{
    private readonly ChartStructureOffsets _offsets = new(change);

    public ChartDefinition Apply(ChartDefinition before, ChartDefinition after)
    {
        if (before.HasSeriesSources || before.IsPivotChart) return after;
        if (before.SeriesNames is null && before.SeriesColors is null && before.SeriesFormats is null && before.LineSeriesIndices is null) return after;
        if (!CellRange.TryParseA1(before.SourceRange, out var compact) || !_offsets.Affects(before.SourceSheet, compact)) return after;
        if ((_offsets.Axis == StructureAxis.Columns) != before.SeriesInColumns) return after;
        var skip = before.SeriesInColumns ? before.FirstColumnIsCategory : before.FirstRowIsHeader;
        var origin = _offsets.Position(compact.Start);
        var offset = skip ? 1 : 0;
        int? Map(int series) => _offsets.Moved(origin, origin + offset + series) is { } position && position - offset >= 0 ? position - offset : null;

        return after with
        {
            SeriesNames = Shifted(before.SeriesNames, Map, string.Empty),
            SeriesColors = Shifted(before.SeriesColors, Map, null),
            SeriesFormats = Shifted(before.SeriesFormats, Map, null),
            LineSeriesIndices = before.LineSeriesIndices is { } lines
                ? [.. lines.Select(Map).OfType<int>().Order()]
                : null,
        };
    }

    private IReadOnlyList<T>? Shifted<T>(IReadOnlyList<T>? items, Func<int, int?> map, T empty)
    {
        if (items is null) return null;
        var moved = new SortedDictionary<int, T>();
        for (var index = 0; index < items.Count; index++)
            if (map(index) is { } target) moved[target] = items[index];
        if (moved.Count == 0) return null;
        var result = Enumerable.Repeat(empty, moved.Keys.Max() + 1).ToList();
        foreach (var (target, item) in moved) result[target] = item;
        return result;
    }
}
