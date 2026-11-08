using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts.Plots;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartLegendPainter(ChartText text)
{
    private const double Swatch = 10;
    private const double Gap = 4;
    private const double RowHeight = 18;
    private const double Spacing = 14;
    private const double SideWidth = 110;
    private const double FontSize = 10;

    public Rect Reserve(LegendPosition position, IReadOnlyList<ChartLegendItem> items, Rect area)
    {
        if (position == LegendPosition.None || items.Count == 0) return area;
        var flow = position is LegendPosition.Top or LegendPosition.Bottom ? Lines(items, area.Width).Count * RowHeight : 0;
        return position switch
        {
            LegendPosition.Left => new Rect(area.X + SideWidth, area.Y, Math.Max(area.Width - SideWidth, 10), area.Height),
            LegendPosition.Top => new Rect(area.X, area.Y + flow, area.Width, Math.Max(area.Height - flow, 10)),
            LegendPosition.Bottom => new Rect(area.X, area.Y, area.Width, Math.Max(area.Height - flow, 10)),
            _ => new Rect(area.X, area.Y, Math.Max(area.Width - SideWidth, 10), area.Height),
        };
    }

    public void Paint(DrawingContext context, LegendPosition position, IReadOnlyList<ChartLegendItem> items, Rect area, Rect plot, Rect bounds)
    {
        if (position == LegendPosition.None || items.Count == 0) return;

        if (position is LegendPosition.Top or LegendPosition.Bottom)
        {
            var lines = Lines(items, area.Width);
            var height = lines.Count * RowHeight;
            PaintFlow(context, lines, area, position == LegendPosition.Top ? plot.Y - height - 4 : bounds.Bottom - height - 2);
            return;
        }

        var x = position == LegendPosition.Left ? area.X : area.Right - SideWidth + 10;
        var y = plot.Y;
        foreach (var item in items)
        {
            if (y > bounds.Bottom - RowHeight) break;
            PaintItem(context, item, new Point(x, y), SideWidth - Swatch - Gap - 14);
            y += RowHeight;
        }
    }

    private void PaintFlow(DrawingContext context, List<List<MeasuredItem>> lines, Rect area, double top)
    {
        var y = top;
        foreach (var line in lines)
        {
            var width = line.Sum(measured => measured.Width) + Spacing * (line.Count - 1);
            var x = area.X + Math.Max(0, (area.Width - width) / 2);
            foreach (var measured in line)
            {
                PaintItem(context, measured.Item, new Point(x, y), area.Width);
                x += measured.Width + Spacing;
            }
            y += RowHeight;
        }
    }

    private void PaintItem(DrawingContext context, ChartLegendItem item, Point origin, double maxWidth)
    {
        context.FillRectangle(new SolidColorBrush(item.Colour), new Rect(origin.X, origin.Y + 3, Swatch, Swatch));
        var label = text.Layout(item.Name, Brushes.Black, FontSize);
        label.MaxTextWidth = Math.Max(maxWidth, 1);
        label.MaxLineCount = 1;
        label.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(label, new Point(origin.X + Swatch + Gap, origin.Y));
    }

    private double Width(ChartLegendItem item) => Swatch + Gap + text.Layout(item.Name, Brushes.Black, FontSize).Width;

    private List<List<MeasuredItem>> Lines(IReadOnlyList<ChartLegendItem> items, double width)
    {
        var lines = new List<List<MeasuredItem>> { new() };
        var used = 0.0;
        foreach (var item in items)
        {
            var size = Width(item);
            if (lines[^1].Count > 0 && used + Spacing + size > width)
            {
                lines.Add([]);
                used = 0;
            }
            used += (lines[^1].Count > 0 ? Spacing : 0) + size;
            lines[^1].Add(new MeasuredItem(item, size));
        }
        return lines;
    }

    private readonly record struct MeasuredItem(ChartLegendItem Item, double Width);
}
