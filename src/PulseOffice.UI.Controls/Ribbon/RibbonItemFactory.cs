using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class RibbonItemFactory(RibbonBuildContext context)
{
    private const int LargeItemThreshold = 2;
    private const double PopoverStripWidth = 360;
    private const double WideStripWidth = 340;
    private const int PopoverTileColumns = 5;
    private const double TileWidth = 76;

    private enum Lane { Large, Column, Strip, Tiles }

    public RibbonBuildContext Context => context;

    public double ItemHeight => context.ItemHeight;

    public double GroupLabelHeight => context.GroupLabelHeight;

    public Control Build(IReadOnlyList<RibbonItem> items, RibbonForm form, int rows, ICollection<RibbonSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(slots);

        var segments = Segments(items, form, rows);

        if (form == RibbonForm.Popover)
        {
            var stack = new StackPanel { Spacing = context.Metrics.SpacingSmall };

            foreach (var (lane, entries) in segments)
                stack.Children.Add(BuildSegment(lane, entries, form, rows, slots));

            return stack;
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = context.Metrics.SpacingSmall,
            Height = rows * ItemHeight,
        };

        foreach (var (lane, entries) in segments)
        {
            var segment = BuildSegment(lane, entries, form, rows, slots);
            segment.VerticalAlignment = lane == Lane.Large ? VerticalAlignment.Stretch : VerticalAlignment.Center;
            row.Children.Add(segment);
        }

        return row;
    }

    public IconView? GroupIcon(RibbonGroup group, double size)
    {
        ArgumentNullException.ThrowIfNull(group);

        foreach (var item in group.Items)
            if (context.Icon(item.Command.Id.Value, size) is { } icon)
                return icon;

        return null;
    }

    private List<(Lane Lane, List<(RibbonItem Item, RibbonShape Shape)> Entries)> Segments(
        IReadOnlyList<RibbonItem> items, RibbonForm form, int rows)
    {
        var segments = new List<(Lane, List<(RibbonItem, RibbonShape)>)>();
        var single = items.Count <= LargeItemThreshold;

        foreach (var item in items)
        {
            var shape = ShapeOf(item, form, single);
            var lane = LaneOf(shape);

            if (lane != Lane.Large && segments.Count > 0 && segments[^1].Item1 == lane)
                segments[^1].Item2.Add((item, shape));
            else
                segments.Add((lane, [(item, shape)]));
        }

        return segments;
    }

    private RibbonShape ShapeOf(RibbonItem item, RibbonForm form, bool single)
    {
        if (item.Control is { FillsHeight: true } && form != RibbonForm.Compact) return RibbonShape.Large;

        if (item.Control is { FollowsDisplay: false }) return RibbonShape.Wide;

        return (item.Display, form) switch
        {
            (RibbonDisplay.Icon, _) => RibbonShape.Icon,
            (_, RibbonForm.Compact) => RibbonShape.Icon,
            (RibbonDisplay.Tile, RibbonForm.Medium) => RibbonShape.Tile,
            (RibbonDisplay.Tile, RibbonForm.Full) => RibbonShape.Tile,
            (RibbonDisplay.Tile, RibbonForm.Popover) => RibbonShape.Tile,
            (RibbonDisplay.Large, RibbonForm.Full) => RibbonShape.Large,
            (RibbonDisplay.Auto, RibbonForm.Full) when single => RibbonShape.Large,
            _ => RibbonShape.Small,
        };
    }

    private Lane LaneOf(RibbonShape shape) => shape switch
    {
        RibbonShape.Large => Lane.Large,
        RibbonShape.Tile => Lane.Tiles,
        RibbonShape.Icon or RibbonShape.Wide => Lane.Strip,
        _ => Lane.Column,
    };

    private Control BuildSegment(
        Lane lane, List<(RibbonItem Item, RibbonShape Shape)> entries, RibbonForm form, int rows, ICollection<RibbonSlot> slots)
    {
        var built = entries.Select(entry => Slot(entry.Item, entry.Shape, form, slots)).ToList();

        return lane switch
        {
            Lane.Large => built[0],
            Lane.Tiles => Tiles(built, form),
            Lane.Strip => Strip(entries, built, form, rows),
            _ => Columns(built, form, rows),
        };
    }

    private RibbonSlot Slot(RibbonItem item, RibbonShape shape, RibbonForm form, ICollection<RibbonSlot> slots)
    {
        var slot = context.Slots.Create(item, shape, form, context);
        slots.Add(slot);
        return slot;
    }

    private Control Tiles(List<RibbonSlot> built, RibbonForm form)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };

        if (form == RibbonForm.Popover) panel.MaxWidth = PopoverTileColumns * TileWidth;

        foreach (var slot in built) panel.Children.Add(slot);

        return panel;
    }

    private Control Columns(List<RibbonSlot> built, RibbonForm form, int rows)
    {
        var perColumn = form == RibbonForm.Popover
            ? Math.Max(rows, (int)Math.Ceiling(built.Count / 3.0))
            : rows;

        var grid = new Grid();

        for (var row = 0; row < Math.Min(perColumn, built.Count); row++)
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(ItemHeight)));

        var columns = (int)Math.Ceiling(built.Count / (double)perColumn);

        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        for (var index = 0; index < built.Count; index++)
        {
            Grid.SetColumn(built[index], index / perColumn);
            Grid.SetRow(built[index], index % perColumn);
            built[index].HorizontalAlignment = HorizontalAlignment.Stretch;
            grid.Children.Add(built[index]);
        }

        return grid;
    }

    private Control Strip(
        List<(RibbonItem Item, RibbonShape Shape)> entries, List<RibbonSlot> built, RibbonForm form, int rows)
    {
        var widths = entries.Select(entry => WidthOf(entry.Item, entry.Shape, form)).ToList();
        var total = widths.Sum();

        var maximum = form == RibbonForm.Popover
            ? (int)Math.Ceiling(total / PopoverStripWidth)
            : rows < 3 ? rows : total > WideStripWidth * 2 ? 3 : 2;

        var counts = context.Strips.Lines(widths, Math.Max(1, maximum));

        var lines = new StackPanel
        {
            Spacing = form == RibbonForm.Popover ? context.Metrics.SpacingSmall : 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var index = 0;

        foreach (var count in counts)
        {
            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Height = ItemHeight,
            };

            foreach (var slot in built.Skip(index).Take(count))
            {
                slot.VerticalAlignment = VerticalAlignment.Center;
                line.Children.Add(slot);
            }

            index += count;
            lines.Children.Add(line);
        }

        lines.Margin = new Thickness(0, 0, 0, 0);
        return lines;
    }

    private double WidthOf(RibbonItem item, RibbonShape shape, RibbonForm form) =>
        shape == RibbonShape.Wide && item.Control is { } control
            ? form == RibbonForm.Compact && control is FontFamilySpec ? 112 : control.InlineWidth
            : ItemHeight + 4;
}
