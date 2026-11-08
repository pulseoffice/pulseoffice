using Avalonia;
using Avalonia.Controls;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class RibbonGroupsPanel(IRibbonReductionPlanner planner) : Panel
{
    public IEnumerable<RibbonGroupView> Groups => Children.OfType<RibbonGroupView>();

    protected override Size MeasureOverride(Size availableSize)
    {
        var groups = Groups.ToList();

        foreach (var group in groups) group.Reset();

        var width = MeasureChildren();

        while (width > availableSize.Width)
        {
            if (planner.Next([.. groups.Select(group => group.State)]) is not { } step) break;

            groups[step.Index].Apply(step.State);
            width = MeasureChildren();
        }

        return new Size(Math.Min(width, availableSize.Width), DesiredHeight());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;

        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }

    private double MeasureChildren()
    {
        var total = 0.0;

        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            total += child.DesiredSize.Width;
        }

        return total;
    }

    private double DesiredHeight()
    {
        var height = 0.0;

        foreach (var child in Children)
            height = Math.Max(height, child.DesiredSize.Height);

        return height;
    }
}
