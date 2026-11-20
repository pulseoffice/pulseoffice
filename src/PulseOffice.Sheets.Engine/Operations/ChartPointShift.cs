using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class ChartPointShift(StructureChange change, IChartSourceReferences references)
{
    private readonly ChartStructureOffsets _offsets = new(change);

    public ChartDefinition Apply(ChartDefinition before, ChartDefinition after)
    {
        if (before.SeriesFormats is not { } formats || formats.All(format => format is null || format.Points.IsEmpty)) return after;
        var shifted = new List<ChartSeriesFormat?>(formats);
        var changed = false;
        for (var series = 0; series < formats.Count; series++)
        {
            if (formats[series] is not { Points.IsEmpty: false } format) continue;
            if (PointStart(before, series) is not { } start) continue;
            var (origin, offset) = start;
            var points = new List<ChartPointFormat>();
            foreach (var point in format.Points.Items)
                if (_offsets.Moved(origin, origin + offset + point.Index) is { } position && position - offset >= 0) points.Add(point with { Index = position - offset });
            var moved = points.Count == 0 ? ChartPointFormats.None : new ChartPointFormats([.. points.OrderBy(point => point.Index)]);
            if (moved.Equals(format.Points)) continue;
            shifted[series] = format with { Points = moved };
            if (shifted[series]!.IsPlain) shifted[series] = null;
            changed = true;
        }
        if (!changed) return after;
        return after with { SeriesFormats = shifted.All(format => format is null) ? null : shifted };
    }

    private (int Origin, int Offset)? PointStart(ChartDefinition chart, int series)
    {
        if (chart.HasSeriesSources)
        {
            if (series >= chart.SeriesSources!.Count) return null;
            var areas = references.Areas(chart.SeriesSources[series].Values);
            if (areas.Count != 1 || !_offsets.Affects(areas[0].Sheet, areas[0].Range)) return null;
            var range = areas[0].Range;
            var down = range.RowCount > 1 || range.ColumnCount == 1;
            return (_offsets.Axis == StructureAxis.Rows) == down ? (_offsets.Position(range.Start), 0) : null;
        }

        if (!CellRange.TryParseA1(chart.SourceRange, out var compact) || !_offsets.Affects(chart.SourceSheet, compact)) return null;
        var alongRows = chart.SeriesInColumns;
        if ((_offsets.Axis == StructureAxis.Rows) != alongRows) return null;
        var skip = alongRows ? chart.FirstRowIsHeader : chart.FirstColumnIsCategory;
        return (_offsets.Position(compact.Start), skip ? 1 : 0);
    }
}
