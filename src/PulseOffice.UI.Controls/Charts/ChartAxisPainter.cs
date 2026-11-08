using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartAxisPainter(ChartText text)
{
    private const double LabelSize = 9;
    private readonly IBrush _labels = Brushes.Gray;

    public void Values(DrawingContext context, PlotScale scale, Rect plot, bool horizontal, bool grid)
    {
        var pen = new Pen(new SolidColorBrush(Colors.Gray, 0.3));

        foreach (var value in scale.Divisions())
        {
            var label = text.Layout(text.Number(value), _labels, LabelSize);
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
        var axis = new Pen(Brushes.Gray, 1);
        if (horizontal) context.DrawLine(axis, new Point(zero, plot.Y), new Point(zero, plot.Bottom));
        else context.DrawLine(axis, new Point(plot.X, zero), new Point(plot.Right, zero));
    }

    public void Categories(DrawingContext context, IReadOnlyList<string> names, CategoryScale scale, Rect plot, bool horizontal)
    {
        if (names.Count == 0) return;

        if (horizontal)
        {
            var skip = Math.Max(1, (int)Math.Ceiling(14 / Math.Max(Math.Abs(scale.Width), 1)));
            for (var index = 0; index < names.Count && index < scale.Count; index += skip)
            {
                var label = text.Layout(names[index], _labels, LabelSize);
                context.DrawText(label, new Point(plot.X - label.Width - 4, scale.Centre(index) - label.Height / 2));
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
            context.DrawText(label, new Point(scale.Centre(index) - label.Width / 2, plot.Bottom + 4));
        }
    }

    public void Titles(DrawingContext context, string? category, string? value, Rect plot, bool horizontal)
    {
        var below = horizontal ? value : category;
        var beside = horizontal ? category : value;

        if (below is { Length: > 0 })
            text.Centred(context, text.Layout(below, Brushes.Black, 10, bold: true), new Point(plot.Center.X, plot.Bottom + 28));

        if (beside is not { Length: > 0 }) return;
        var label = text.Layout(beside, Brushes.Black, 10, bold: true);
        var centre = new Point(plot.X - 44, plot.Center.Y);
        using (context.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(centre.X, centre.Y)))
            text.Centred(context, label, centre);
    }
}
