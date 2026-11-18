using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Charts;

public enum ChartRenderMode
{
    Full,
    Thumbnail,
}

public sealed class ChartRenderer
{
    private readonly IReadOnlyList<Plots.IChartPlot> _plots;
    private readonly Plots.IChartPlot _fallback;

    private readonly ITextCatalog? _texts;

    public ChartRenderer(IEnumerable<Plots.IChartPlot> plots, ITextCatalog? texts = null, Plots.IChartPlot? fallback = null)
    {
        _fallback = fallback ?? new Plots.BarPlot();
        _plots = [.. plots];
        _texts = texts;
    }

    public ChartRenderer(ITextCatalog texts) : this(DefaultPlots(), texts) { }

    public ChartRenderer() : this(DefaultPlots()) { }

    private static IEnumerable<Plots.IChartPlot> DefaultPlots() =>
    [
        new Plots.BarPlot(),
        new Plots.LinePlot(),
        new Plots.PiePlot(),
        new Plots.ScatterPlot(),
        new Plots.StatisticalPlot(),
        new Plots.FinancialPlot(),
        new Plots.RadarPlot(),
        new Plots.HierarchyPlot(),
        new Plots.MeterPlot(),
    ];

    private const double Padding = 12;
    private const double ValueLabelWidth = 34;
    private const double AxisLabelHeight = 18;
    private const double AxisTitleSpace = 16;
    private const double ReferenceWidth = 360;
    private const double ReferenceHeight = 220;

    public void Render(DrawingContext context, ChartDefinition definition, ChartData data, Rect bounds) =>
        Render(context, definition, data, bounds, ChartRenderMode.Full);

    public void Render(DrawingContext context, ChartDefinition definition, ChartData data, Rect bounds, ChartRenderMode mode) =>
        Render(context, definition, data, bounds, mode, System.Globalization.CultureInfo.CurrentCulture);

    public void Render(DrawingContext context, ChartDefinition definition, ChartData data, Rect bounds, System.Globalization.CultureInfo culture) =>
        Render(context, definition, data, bounds, ChartRenderMode.Full, culture);

    public void Render(DrawingContext context, ChartDefinition definition, ChartData data, Rect bounds, ChartRenderMode mode, System.Globalization.CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var scene = new ChartScene(new ChartText(culture));
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(definition);
        definition = _inlineErrorValues.Refresh(definition);
        data = definition.Coloured(data);

        var scale = Math.Min(1, Math.Min(bounds.Width / ReferenceWidth, bounds.Height / ReferenceHeight));
        if (scale >= 1 || scale <= 0)
        {
            RenderAtSize(scene, context, Adapt(definition, mode), data, bounds, mode);
            return;
        }

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            RenderAtSize(scene, context, Adapt(definition, mode), data,
                new Rect(bounds.X / scale, bounds.Y / scale, bounds.Width / scale, bounds.Height / scale), mode);
    }

    private ChartDefinition Adapt(ChartDefinition definition, ChartRenderMode mode) =>
        mode == ChartRenderMode.Thumbnail
            ? definition with { ChartTitle = null, CategoryAxisTitle = null, ValueAxisTitle = null, Legend = LegendPosition.None, ShowDataLabels = false }
            : definition;

    private void RenderAtSize(ChartScene scene, DrawingContext context, ChartDefinition definition, ChartData data, Rect bounds, ChartRenderMode mode)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(data);

        if (data.IsEmpty)
        {
            DrawEmpty(scene.Text, context, Brushes.Black, bounds);
            return;
        }

        var painter = _plots.FirstOrDefault(candidate => candidate.Handles(definition.Type)) ?? _fallback;
        if (!painter.KeepsGaps) data = data.WithoutGaps();

        var items = painter.Legend(definition, data);
        var area = ContentArea(definition, bounds);
        var thumbnail = mode == ChartRenderMode.Thumbnail;
        var groups = painter.HasCategoryAxis(definition.Type) ? _categoryGroups.Groups(data) : [];
        var plot = thumbnail ? area : PlotArea(definition, painter, scene.Legend.Reserve(definition.Legend, items, area), groups.Count);

        DrawTitle(scene.Text, context, definition, bounds);
        scene.Legend.Paint(context, definition.Legend, items, area, plot, bounds);

        var frame = new Plots.ChartFrame(context, definition, data, plot, bounds, scene.Text.Culture);
        var horizontal = painter.HorizontalAxis(definition.Type);
        double? categoryLine = null;

        if (painter.DrawsAxes && !thumbnail)
        {
            var values = painter.ValueAxis(frame) ?? _axisScaling.Apply(DefaultScale(data, plot, horizontal), definition.ValueAxis);
            categoryLine = _marks.CategoryAxisLine(values, definition.ValueAxis, horizontal ? plot.X : plot.Bottom);
            scene.Axes.Values(context, values, plot, horizontal, definition.ShowGridLines, definition.ValueAxis.NumberFormat);
            scene.Axes.MinorTicks(context, _marks.MinorTicks(values, definition.ValueAxis), values, plot, horizontal, opposite: false);
            var across = painter.CategoryValueAxis(frame);
            if (across is not null)
            {
                scene.Axes.Values(context, across, plot, !horizontal, definition.ShowGridLines, definition.CategoryAxis.NumberFormat);
                scene.Axes.MinorTicks(context, _marks.MinorTicks(across, definition.CategoryAxis), across, plot, !horizontal, opposite: false);
            }
            if (painter.SecondaryValueAxis(frame) is { } secondary)
            {
                scene.Axes.Secondary(context, secondary, plot, definition.SecondaryValueAxis.NumberFormat, horizontal);
                scene.Axes.MinorTicks(context, _marks.MinorTicks(secondary, definition.SecondaryValueAxis), secondary, plot, horizontal, opposite: true);
            }
            if (across is null && painter.HasCategoryAxis(definition.Type))
                scene.Axes.Crossings(context, plot, horizontal,
                    definition.ValueAxis.Crossing == AxisCrossing.Automatic ? null : values.Position(_marks.ValueCrossing(values, definition.ValueAxis)),
                    CategoryCrossing(definition, data, plot, horizontal, painter.DrawsReversedCategories && definition.CategoryAxis.Reversed));
        }

        painter.Draw(frame);

        if (!thumbnail && painter.DrawsAxes && HasAnalysis(definition, data))
            _analysis.Paint(context, painter, frame, [.. painter.DataLabels(frame)], horizontal);

        if (painter.DrawsAxes && !thumbnail)
        {
            if (painter.HasCategoryAxis(definition.Type))
            {
                var scale = Categories(data, plot, horizontal, painter.DrawsReversedCategories && definition.CategoryAxis.Reversed);
                scene.Axes.Categories(context, data.Categories, scale, plot, horizontal, categoryLine);
                if (groups.Count > 0) scene.Axes.Groups(context, groups, scale, plot, horizontal, categoryLine);
            }
            scene.Axes.Titles(context, definition.CategoryAxisTitle, definition.ValueAxisTitle, plot, horizontal, groups.Count);
        }

        if (definition.ShowDataLabels) DrawDataLabels(scene.Text, context, definition, data, painter.DataLabels(frame));
    }

    private readonly ChartAxisScaling _axisScaling = new();

    private readonly ChartCategoryGroups _categoryGroups = new();

    private readonly ChartInlineErrorValues _inlineErrorValues = new();

    private readonly ChartAxisMarks _marks = new();

    private readonly ChartAnalysisPainter _analysis = new();

    private bool HasAnalysis(ChartDefinition definition, ChartData data) =>
        Enumerable.Range(0, data.Series.Count).Any(series => definition.SeriesFormat(series) is { Trendline: not null } or { ErrorBars: not null } or { XErrorBars: not null });

    private string LabelText(ChartText text, ChartDefinition definition, ChartData data, Plots.ChartDataLabel label)
    {
        var options = definition.DataLabels;
        if (options.IsValuesOnly) return text.Number(label.Value);
        var parts = new List<string>();
        if (options.ShowSeriesName && label.Series >= 0 && label.Series < data.Series.Count) parts.Add(data.Series[label.Series].Name);
        if (options.ShowCategory && label.Index >= 0 && label.Index < data.Categories.Count) parts.Add(data.Categories[label.Index]);
        if (options.ShowValue) parts.Add(text.Formatted(label.Value, options.NumberFormat));
        if (options.ShowPercent && label.Series >= 0 && label.Series < data.Series.Count
            && data.Series[label.Series].Values.Where(double.IsFinite).Sum(value => Math.Abs(value)) is > 0 and var total)
            parts.Add(text.Number(label.Value / total, "0%"));
        return string.Join("; ", parts);
    }

    private void DrawDataLabels(ChartText text, DrawingContext context, ChartDefinition definition, ChartData data, IEnumerable<Plots.ChartDataLabel> labels)
    {
        foreach (var label in labels)
        {
            var caption = LabelText(text, definition, data, label);
            if (caption.Length == 0) continue;
            var layout = text.Layout(caption, Brushes.Black, 9);
            var origin = new Point(label.Anchor.X - layout.Width / 2, label.Anchor.Y - layout.Height - 2);
            context.FillRectangle(new SolidColorBrush(Colors.White, 0.7), new Rect(origin, new Size(layout.Width, layout.Height)));
            context.DrawText(layout, origin);
        }
    }

    private Rect ContentArea(ChartDefinition definition, Rect bounds)
    {
        var top = bounds.Y + Padding + (definition.ChartTitle is { Length: > 0 } ? 22 : 0);
        return new Rect(bounds.X + Padding, top, Math.Max(bounds.Width - Padding * 2, 10), Math.Max(bounds.Bottom - Padding - top, 10));
    }

    private Rect PlotArea(ChartDefinition definition, Plots.IChartPlot painter, Rect area, int groupLevels)
    {
        if (!painter.DrawsAxes) return area;

        var horizontal = painter.HorizontalAxis(definition.Type);
        var left = area.X + ValueLabelWidth + (Has(definition.ValueAxisTitle) || Has(definition.CategoryAxisTitle) ? AxisTitleSpace : 0)
            + (horizontal ? groupLevels * ChartAxisPainter.GroupColumnWidth : 0);
        var bottom = area.Bottom - AxisLabelHeight - (Has(definition.CategoryAxisTitle) || Has(definition.ValueAxisTitle) ? AxisTitleSpace : 0)
            - (horizontal ? 0 : groupLevels * ChartAxisPainter.GroupRowHeight);
        var right = area.Right - (definition.HasSecondaryAxis && !horizontal ? ValueLabelWidth : 0);
        var top = area.Y + (definition.HasSecondaryAxis && horizontal ? AxisLabelHeight : 0);
        return new Rect(left, top, Math.Max(right - left, 10), Math.Max(bottom - top, 10));
    }

    private double? CategoryCrossing(ChartDefinition definition, ChartData data, Rect plot, bool horizontal, bool reversed)
    {
        if (definition.CategoryAxis.Crossing == AxisCrossing.Automatic) return null;
        var categories = Categories(data, plot, horizontal, reversed);
        return categories.Boundary(_marks.CategoryCrossing(categories.Count, definition.CategoryAxis));
    }

    private bool Has(string? text) => text is { Length: > 0 };

    private CategoryScale Categories(ChartData data, Rect plot, bool horizontal, bool reversed)
    {
        var count = Math.Max(1, Math.Max(data.Categories.Count, data.Series.Count == 0 ? 0 : data.Series.Max(series => series.Values.Count)));
        return horizontal ? new CategoryScale(count, plot.Y, plot.Bottom, Reversed: !reversed) : new CategoryScale(count, plot.X, plot.Right, Reversed: reversed);
    }

    private PlotScale DefaultScale(ChartData data, Rect plot, bool horizontal)
    {
        var minimum = Math.Min(data.Minimum, 0);
        var maximum = Math.Max(data.Maximum, 0);
        return horizontal ? PlotScale.Nice(minimum, maximum, plot.X, plot.Right) : PlotScale.Nice(minimum, maximum, plot.Bottom, plot.Y);
    }

    private void DrawEmpty(ChartText text, DrawingContext context, IBrush foreground, Rect bounds)
    {
        if (_texts is not null)
        {
            text.Centred(context, text.Layout(_texts["chart.empty"], foreground, 12), bounds.Center);
            return;
        }

        var frame = bounds.Deflate(Math.Min(bounds.Width, bounds.Height) * 0.25);
        if (frame.Width <= 0 || frame.Height <= 0) return;

        context.DrawRectangle(null, new Pen(Brushes.Gray, 1) { DashStyle = new DashStyle([4, 3], 0) }, frame);
    }

    private void DrawTitle(ChartText chartText, DrawingContext context, ChartDefinition definition, Rect bounds)
    {
        if (definition.ChartTitle is not { Length: > 0 } title) return;

        var text = chartText.Layout(title, Brushes.Black, 14, bold: true);
        text.MaxTextWidth = Math.Max(bounds.Width - Padding * 2, 1);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(text, new Point(bounds.X + Math.Max(Padding, (bounds.Width - text.Width) / 2), bounds.Y + 6));
    }
}

internal sealed class ChartScene(ChartText text)
{
    public ChartText Text => text;

    public ChartLegendPainter Legend { get; } = new(text);

    public ChartAxisPainter Axes { get; } = new(text);
}
