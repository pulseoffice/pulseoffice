using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartAxisPainter(ChartText text)
{
    private const double LabelSize = 9;
    private const double MinorTickLength = 3;
    private readonly IBrush _labels = text.Ink.Muted;

    public void Values(DrawingContext context, PlotScale scale, Rect plot, bool horizontal, bool grid) =>
        Values(context, scale, plot, horizontal, grid, null);

    public void Secondary(DrawingContext context, PlotScale scale, Rect plot, string? format) =>
        Secondary(context, scale, plot, format, false);

    public void Secondary(DrawingContext context, PlotScale scale, Rect plot, string? format, bool horizontal)
    {
        foreach (var value in scale.Divisions())
        {
            var label = text.Layout(text.Formatted(value, format), _labels, LabelSize);
            var position = scale.Position(value);
            context.DrawText(label, horizontal
                ? new Point(position - label.Width / 2, plot.Y - label.Height - 4)
                : new Point(plot.Right + 4, position - label.Height / 2));
        }
    }

    public void MinorTicks(DrawingContext context, IReadOnlyList<double> ticks, PlotScale scale, Rect plot, bool horizontal, bool opposite)
    {
        if (ticks.Count == 0) return;
        var pen = new Pen(text.Ink.Stroke, 1);
        foreach (var value in ticks)
        {
            var position = scale.Position(value);
            if (horizontal)
            {
                var edge = opposite ? plot.Y : plot.Bottom;
                context.DrawLine(pen, new Point(position, edge), new Point(position, edge + (opposite ? MinorTickLength : -MinorTickLength)));
            }
            else
            {
                var edge = opposite ? plot.Right : plot.X;
                context.DrawLine(pen, new Point(edge, position), new Point(edge + (opposite ? -MinorTickLength : MinorTickLength), position));
            }
        }
    }

    public void Crossings(DrawingContext context, Rect plot, bool horizontal, double? valueCrossing, double? categoryCrossing)
    {
        var axis = new Pen(text.Ink.Stroke, 1);
        if (valueCrossing is { } along)
        {
            if (horizontal) context.DrawLine(axis, new Point(along, plot.Y), new Point(along, plot.Bottom));
            else context.DrawLine(axis, new Point(plot.X, along), new Point(plot.Right, along));
        }

        if (categoryCrossing is { } across)
        {
            if (horizontal) context.DrawLine(axis, new Point(plot.X, across), new Point(plot.Right, across));
            else context.DrawLine(axis, new Point(across, plot.Y), new Point(across, plot.Bottom));
        }
    }

    public void Values(DrawingContext context, PlotScale scale, Rect plot, bool horizontal, bool grid, string? format)
    {
        var pen = new Pen(text.Ink.Grid);

        foreach (var value in scale.Divisions())
        {
            var label = text.Layout(text.Formatted(value, format), _labels, LabelSize);
            var position = scale.Position(value);
            if (horizontal)
            {
                if (grid) context.DrawLine(pen, new Point(position, plot.Y), new Point(position, plot.Bottom));
                context.DrawText(label, new Point(position - label.Width / 2, plot.Bottom + 4));
            }
            else
            {
                if (grid) context.DrawLine(pen, new Point(plot.X, position), new Point(plot.Right, position));
                context.DrawText(label, new Point(plot.X - label.Width - 4, position - label.Height / 2));
            }
        }

        if (!(scale.Minimum < 0 && scale.Maximum > 0)) return;

        var zero = scale.Position(0);
        var axis = new Pen(text.Ink.Stroke, 1);
        if (horizontal) context.DrawLine(axis, new Point(zero, plot.Y), new Point(zero, plot.Bottom));
        else context.DrawLine(axis, new Point(plot.X, zero), new Point(plot.Right, zero));
    }

    public void Categories(DrawingContext context, IReadOnlyList<string> names, CategoryScale scale, Rect plot, bool horizontal) =>
        Categories(context, names, scale, plot, horizontal, null);

    public void Categories(DrawingContext context, IReadOnlyList<string> names, CategoryScale scale, Rect plot, bool horizontal, double? line)
    {
        if (names.Count == 0) return;

        if (horizontal)
        {
            var skip = Math.Max(1, (int)Math.Ceiling(14 / Math.Max(Math.Abs(scale.Width), 1)));
            for (var index = 0; index < names.Count && index < scale.Count; index += skip)
            {
                var label = text.Layout(names[index], _labels, LabelSize);
                context.DrawText(label, new Point((line ?? plot.X) - label.Width - 4, scale.Centre(index) - label.Height / 2));
            }
            return;
        }

        var every = Math.Max(1, (int)Math.Ceiling(Math.Min(names.Count, scale.Count) * 40 / Math.Max(plot.Width, 1)));
        for (var index = 0; index < names.Count && index < scale.Count; index += every)
        {
            var label = text.Layout(names[index], _labels, LabelSize);
            label.MaxTextWidth = Math.Max(scale.Width * every, 1);
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(label, new Point(scale.Centre(index) - label.Width / 2, (line ?? plot.Bottom) + 4));
        }
    }

    public const double GroupRowHeight = 16;

    public const double GroupColumnWidth = 48;

    private const double LeafColumnWidth = 34;

    public void Groups(DrawingContext context, IReadOnlyList<IReadOnlyList<ChartCategoryGroup>> levels, CategoryScale scale, Rect plot, bool horizontal, double? line)
    {
        var pen = new Pen(text.Ink.Stroke, 1);
        for (var level = 0; level < levels.Count; level++)
        {
            var ring = levels.Count - level;
            foreach (var group in levels[level])
            {
                if (group.First >= scale.Count) continue;
                var last = Math.Min(group.Last, scale.Count - 1);
                var from = Math.Min(scale.Start(group.First), scale.Start(last));
                var to = Math.Max(scale.Start(group.First), scale.Start(last)) + Math.Abs(scale.Width);
                var label = text.Layout(group.Label, _labels, LabelSize);
                label.MaxLineCount = 1;
                label.Trimming = TextTrimming.CharacterEllipsis;
                if (horizontal)
                {
                    var right = (line ?? plot.X) - LeafColumnWidth - (ring - 1) * GroupColumnWidth;
                    label.MaxTextWidth = GroupColumnWidth - 4;
                    context.DrawLine(pen, new Point(right - GroupColumnWidth, from), new Point(right, from));
                    context.DrawLine(pen, new Point(right - GroupColumnWidth, to), new Point(right, to));
                    context.DrawText(label, new Point(right - label.Width - 4, (from + to - label.Height) / 2));
                }
                else
                {
                    var top = (line ?? plot.Bottom) + ring * GroupRowHeight;
                    label.MaxTextWidth = Math.Max(to - from, 1);
                    context.DrawLine(pen, new Point(from, top - GroupRowHeight), new Point(from, top + GroupRowHeight));
                    context.DrawLine(pen, new Point(to, top - GroupRowHeight), new Point(to, top + GroupRowHeight));
                    context.DrawText(label, new Point((from + to - label.Width) / 2, top + 2));
                }
            }
        }
    }

    public void Titles(DrawingContext context, string? category, string? value, Rect plot, bool horizontal) =>
        Titles(context, category, value, plot, horizontal, 0);

    public void Titles(DrawingContext context, string? category, string? value, Rect plot, bool horizontal, int groupLevels)
    {
        var below = horizontal ? value : category;
        var beside = horizontal ? category : value;

        if (below is { Length: > 0 })
            text.Centred(context, text.Layout(below, text.Ink.Text, 10, bold: true), new Point(plot.Center.X, plot.Bottom + 28 + (horizontal ? 0 : groupLevels * GroupRowHeight)));

        if (beside is not { Length: > 0 }) return;
        var label = text.Layout(beside, text.Ink.Text, 10, bold: true);
        var centre = new Point(plot.X - 44 - (horizontal ? groupLevels * GroupColumnWidth : 0), plot.Center.Y);
        using (context.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(centre.X, centre.Y)))
            text.Centred(context, label, centre);
    }
}
