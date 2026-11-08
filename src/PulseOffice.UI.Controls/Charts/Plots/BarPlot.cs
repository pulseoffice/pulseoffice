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

    public override PlotScale? ValueAxis(ChartFrame frame) =>
        StackScale(Prepared(frame), frame.Plot, HorizontalAxis(frame.Definition.Type), Stacked(frame.Definition.Type));

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
        var scale = StackScale(data, frame.Plot, horizontal: false, stacked: false);
        var categories = Categories(data, frame.Plot);
        var zero = scale.Position(Math.Max(0, scale.Minimum));
        var columns = Enumerable.Range(0, data.Series.Count).Where(series => !frame.Definition.IsLineSeries(series, data.Series.Count)).ToList();
        var lines = Enumerable.Range(0, data.Series.Count).Where(series => frame.Definition.IsLineSeries(series, data.Series.Count));

        for (var index = 0; index < categories.Count; index++)
            for (var slot = 0; slot < columns.Count; slot++)
            {
                var series = columns[slot];
                if (!IsPlotted(data.Series[series], index) || Value(data.Series[series], index) == 0) continue;
                var (start, width) = categories.Slot(index, slot, columns.Count);
                frame.Context.FillRectangle(Brush(data, series), Rectangle(false, start, width, zero, scale.Position(Value(data.Series[series], index))));
            }

        foreach (var series in lines)
        {
            var points = Enumerable.Range(0, categories.Count)
                .Where(index => IsPlotted(data.Series[series], index))
                .Select(index => new Point(categories.Centre(index), scale.Position(Value(data.Series[series], index))))
                .ToList();
            if (points.Count == 0) continue;

            var geometry = new StreamGeometry();
            using (var sink = geometry.Open())
            {
                sink.BeginFigure(points[0], false);
                foreach (var point in points.Skip(1)) sink.LineTo(point);
                sink.EndFigure(false);
            }

            var pen = new Pen(Brush(data, series), 2.5) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
            frame.Context.DrawGeometry(null, pen, geometry);
            foreach (var point in points) frame.Context.DrawEllipse(Brush(data, series), null, point, 3, 3);
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
        var scale = StackScale(data, frame.Plot, horizontal, stacked: false);
        var categories = Categories(data, frame.Plot, horizontal);
        var zero = scale.Position(Math.Max(0, scale.Minimum));

        for (var index = 0; index < categories.Count; index++)
            for (var series = 0; series < data.Series.Count; series++)
            {
                if (!IsPlotted(data.Series[series], index) || Value(data.Series[series], index) == 0) continue;
                var (start, width) = categories.Slot(index, series, data.Series.Count);
                var position = scale.Position(Value(data.Series[series], index));

                frame.Context.FillRectangle(
                    Brush(data, series),
                    Rectangle(horizontal, start, width, zero, position));
            }
    }

    private void DrawStacked(ChartFrame frame, ChartData data, bool horizontal)
    {
        var scale = StackScale(data, frame.Plot, horizontal, stacked: true);
        var categories = Categories(data, frame.Plot, horizontal);

        for (var index = 0; index < categories.Count; index++)
        {
            var positive = 0.0;
            var negative = 0.0;

            for (var series = 0; series < data.Series.Count; series++)
            {
                var value = Value(data.Series[series], index);
                if (value == 0) continue;
                var from = value >= 0 ? positive : negative;
                var to = from + value;

                if (value >= 0) positive = to; else negative = to;

                var start = categories.Start(index) + categories.Width * 0.15;
                var width = categories.Width * 0.7;

                frame.Context.FillRectangle(
                    Brush(data, series),
                    Rectangle(horizontal, start, width, scale.Position(from), scale.Position(to)));
            }
        }
    }

    public override IEnumerable<ChartDataLabel> DataLabels(ChartFrame frame)
    {
        var horizontal = HorizontalAxis(frame.Definition.Type);
        var data = Prepared(frame);
        var stacked = Stacked(frame.Definition.Type);
        var scale = StackScale(data, frame.Plot, horizontal, stacked);
        var categories = Categories(data, frame.Plot, horizontal);

        for (var index = 0; index < categories.Count; index++)
        {
            var positive = 0.0;
            var negative = 0.0;
            for (var series = 0; series < data.Series.Count; series++)
            {
                if (!IsPlotted(frame.Data.Series[series], index)) continue;
                var value = Value(data.Series[series], index);
                var end = value;
                if (stacked)
                {
                    end = (value >= 0 ? positive : negative) + value;
                    if (value >= 0) positive = end; else negative = end;
                }
                var (start, width) = stacked
                    ? (categories.Start(index) + categories.Width * 0.15, categories.Width * 0.7)
                    : categories.Slot(index, series, data.Series.Count);
                var along = scale.Position(end);
                yield return new ChartDataLabel(
                    horizontal ? new Point(along, start + width / 2) : new Point(start + width / 2, along),
                    Value(frame.Data.Series[series], index));
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
