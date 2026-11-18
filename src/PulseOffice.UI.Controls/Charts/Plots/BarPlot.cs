using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class BarPlot : ChartPlotBase
{
    public override bool Handles(ChartType type) => type is
        ChartType.Column or ChartType.StackedColumn or ChartType.PercentStackedColumn or ChartType.ColumnLine
        or ChartType.Bar or ChartType.StackedBar or ChartType.PercentStackedBar
        or ChartType.PopulationPyramid;

    public override bool HorizontalAxis(ChartType type) => type is
        ChartType.Bar or ChartType.StackedBar or ChartType.PercentStackedBar
        or ChartType.PopulationPyramid;

    public override PlotScale? ValueAxis(ChartFrame frame) => Primary(frame, Prepared(frame), HorizontalAxis(frame.Definition.Type));

    public override PlotScale? SecondaryValueAxis(ChartFrame frame) => Secondary(frame, Prepared(frame), HorizontalAxis(frame.Definition.Type));

    public override PlotScale? SeriesScale(ChartFrame frame, int series) =>
        ScaleOf(frame, series, Primary(frame, Prepared(frame), HorizontalAxis(frame.Definition.Type)), SecondaryValueAxis(frame));

    public override bool DrawsReversedCategories => true;

    private readonly ChartSeriesStroke _strokes = new();

    private readonly ChartAxisMarks _marks = new();

    private bool Splits(ChartFrame frame) =>
        frame.Definition.HasSecondaryAxis && frame.Definition.Type != ChartType.PopulationPyramid;

    private PlotScale Primary(ChartFrame frame, ChartData data, bool horizontal) =>
        Stacked(frame.Definition.Type)
            ? Scaled(StackScale(Side(frame, data, false, Splits(frame)), frame.Plot, horizontal, true), frame.Definition.ValueAxis)
            : PrimaryScale(frame, data, Splits(frame), horizontal);

    private PlotScale? Secondary(ChartFrame frame, ChartData data, bool horizontal) =>
        !Splits(frame) ? null
        : Stacked(frame.Definition.Type)
            ? Scaled(StackScale(Side(frame, data, true, true), frame.Plot, horizontal, true), frame.Definition.SecondaryValueAxis)
            : SecondaryScale(frame, data, true, horizontal);

    private double Baseline(ChartFrame frame, int series, PlotScale scale, PlotScale? secondary) =>
        scale.Position(_marks.ValueCrossing(scale,
            secondary is not null && frame.Definition.SeriesFormat(series).SecondaryAxis ? frame.Definition.SecondaryValueAxis : frame.Definition.ValueAxis));

    public override void Draw(ChartFrame frame)
    {
        var horizontal = HorizontalAxis(frame.Definition.Type);
        var data = Prepared(frame);

        if (Stacked(frame.Definition.Type)) DrawStacked(frame, data, horizontal);
        else if (frame.Definition.Type == ChartType.ColumnLine) DrawCombination(frame, data);
        else DrawGrouped(frame, data, horizontal);
    }

    private void DrawCombination(ChartFrame frame, ChartData data)
    {
        var scale = Primary(frame, data, false);
        var secondary = Secondary(frame, data, false);
        var categories = Categories(frame, data);
        var columns = Enumerable.Range(0, data.Series.Count).Where(series => !frame.Definition.IsLineSeries(series, data.Series.Count)).ToList();
        var lines = Enumerable.Range(0, data.Series.Count).Where(series => frame.Definition.IsLineSeries(series, data.Series.Count));

        for (var index = 0; index < categories.Count; index++)
            for (var slot = 0; slot < columns.Count; slot++)
            {
                var series = columns[slot];
                if (!IsPlotted(data.Series[series], index) || Value(data.Series[series], index) == 0) continue;
                var (start, width) = categories.Slot(index, slot, columns.Count);
                var own = ScaleOf(frame, series, scale, secondary);
                frame.Context.FillRectangle(Brush(data, series), Rectangle(false, start, width, Baseline(frame, series, own, secondary), own.Position(Value(data.Series[series], index))));
            }

        foreach (var series in lines)
        {
            var points = Enumerable.Range(0, categories.Count)
                .Where(index => IsPlotted(data.Series[series], index))
                .Select(index => new Point(categories.Centre(index), ScaleOf(frame, series, scale, secondary).Position(Value(data.Series[series], index))))
                .ToList();
            if (points.Count == 0) continue;

            var geometry = new StreamGeometry();
            using (var sink = geometry.Open())
            {
                sink.BeginFigure(points[0], false);
                foreach (var point in points.Skip(1)) sink.LineTo(point);
                sink.EndFigure(false);
            }

            var format = frame.Definition.SeriesFormat(series);
            frame.Context.DrawGeometry(null, _strokes.Pen(Brush(data, series), format, 2.5), geometry);
            foreach (var point in points) _strokes.Marker(frame.Context, Brush(data, series), format, point, 3);
        }
    }

    private bool Stacked(ChartType type) => type is
        ChartType.StackedColumn or ChartType.StackedBar
        or ChartType.PercentStackedColumn or ChartType.PercentStackedBar;

    private ChartData Prepared(ChartFrame frame) =>
        frame.Definition.Type is ChartType.PercentStackedColumn or ChartType.PercentStackedBar
            ? Normalised(frame.Data)
            : frame.Data;

    private void DrawGrouped(ChartFrame frame, ChartData data, bool horizontal)
    {
        var scale = Primary(frame, data, horizontal);
        var secondary = Secondary(frame, data, horizontal);
        var categories = Categories(frame, data, horizontal);

        for (var index = 0; index < categories.Count; index++)
            for (var series = 0; series < data.Series.Count; series++)
            {
                if (!IsPlotted(data.Series[series], index) || Value(data.Series[series], index) == 0) continue;
                var (start, width) = categories.Slot(index, series, data.Series.Count);
                var own = ScaleOf(frame, series, scale, secondary);
                var position = own.Position(Value(data.Series[series], index));

                frame.Context.FillRectangle(
                    Brush(data, series),
                    Rectangle(horizontal, start, width, Baseline(frame, series, own, secondary), position));
            }
    }

    private void DrawStacked(ChartFrame frame, ChartData data, bool horizontal)
    {
        foreach (var segment in StackedSegments(frame, data, horizontal))
        {
            if (segment.From == segment.To) continue;
            frame.Context.FillRectangle(
                Brush(data, segment.Series),
                Rectangle(horizontal, segment.Start, segment.Width, segment.Scale.Position(segment.From), segment.Scale.Position(segment.To)));
        }
    }

    private sealed record StackedSegment(int Series, int Index, double Start, double Width, double From, double To, PlotScale Scale);

    private IEnumerable<StackedSegment> StackedSegments(ChartFrame frame, ChartData data, bool horizontal)
    {
        var scale = Primary(frame, data, horizontal);
        var secondary = Secondary(frame, data, horizontal);
        var categories = Categories(frame, data, horizontal);
        var groups = secondary is null ? 1 : 2;

        for (var index = 0; index < categories.Count; index++)
        {
            for (var group = 0; group < groups; group++)
            {
                var positive = 0.0;
                var negative = 0.0;
                var width = categories.Width * 0.7 / groups;
                var start = categories.Start(index) + categories.Width * 0.15 + width * group;

                for (var series = 0; series < data.Series.Count; series++)
                {
                    var onSecondary = secondary is not null && frame.Definition.SeriesFormat(series).SecondaryAxis;
                    if (onSecondary != (group == 1) || !IsPlotted(frame.Data.Series[series], index)) continue;
                    var value = Value(data.Series[series], index);
                    var from = value >= 0 ? positive : negative;
                    var to = from + value;
                    if (value >= 0) positive = to; else negative = to;
                    yield return new StackedSegment(series, index, start, width, from, to, onSecondary ? secondary! : scale);
                }
            }
        }
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        var horizontal = HorizontalAxis(frame.Definition.Type);
        var data = Prepared(frame);

        if (Stacked(frame.Definition.Type))
        {
            foreach (var segment in StackedSegments(frame, data, horizontal))
            {
                var along = segment.Scale.Position(segment.To);
                var across = segment.Start + segment.Width / 2;
                yield return new ChartDataLabel(
                    horizontal ? new Point(along, across) : new Point(across, along),
                    Value(frame.Data.Series[segment.Series], segment.Index))
                { Series = segment.Series, Index = segment.Index, Across = segment.Index + 1 };
            }
            yield break;
        }

        var scale = Primary(frame, data, horizontal);
        var secondary = Secondary(frame, data, horizontal);
        var categories = Categories(frame, data, horizontal);

        for (var index = 0; index < categories.Count; index++)
        {
            for (var series = 0; series < data.Series.Count; series++)
            {
                if (!IsPlotted(frame.Data.Series[series], index)) continue;
                var (start, width) = categories.Slot(index, series, data.Series.Count);
                var along = ScaleOf(frame, series, scale, secondary).Position(Value(data.Series[series], index));
                yield return new ChartDataLabel(
                    horizontal ? new Point(along, start + width / 2) : new Point(start + width / 2, along),
                    Value(frame.Data.Series[series], index))
                { Series = series, Index = index, Across = index + 1 };
            }
        }
    }

    private PlotScale StackScale(ChartData data, Rect plot, bool horizontal, bool stacked)
    {
        if (!stacked) return ValueScale(data, plot, horizontal);

        var count = data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count);

        var totals = Enumerable.Range(0, count)
            .Select(index => data.Series.Sum(series => Math.Max(0, Value(series, index))))
            .DefaultIfEmpty(0)
            .ToList();

        var lows = Enumerable.Range(0, count)
            .Select(index => data.Series.Sum(series => Math.Min(0, Value(series, index))))
            .DefaultIfEmpty(0)
            .ToList();

        return horizontal
            ? PlotScale.Nice(lows.Min(), Math.Max(totals.Max(), lows.Min() < 0 ? 0 : 1), plot.X, plot.Right)
            : PlotScale.Nice(lows.Min(), Math.Max(totals.Max(), lows.Min() < 0 ? 0 : 1), plot.Bottom, plot.Y);
    }

    private Rect Rectangle(bool horizontal, double start, double width, double from, double to)
    {
        var low = Math.Min(from, to);
        var length = Math.Abs(to - from);

        return horizontal
            ? new Rect(low, start, length, width)
            : new Rect(start, low, width, length);
    }

    private ChartData Normalised(ChartData data)
    {
        var count = data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count);

        var totals = Enumerable.Range(0, count)
            .Select(index => data.Series.Sum(series => Math.Abs(Value(series, index))))
            .ToList();

        return new ChartData(data.Categories,
        [
            .. data.Series.Select(series => series with
            {
                Values =
                [
                    .. Enumerable.Range(0, count).Select(index =>
                        totals[index] <= 0 ? 0 : Value(series, index) / totals[index] * 100),
                ],
            }),
        ]);
    }
}
