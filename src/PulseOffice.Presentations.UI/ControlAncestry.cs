using Avalonia.Controls;

namespace PulseOffice.Presentations.UI;

internal sealed class ControlAncestry
{
    public bool IsInside(Control child, Control ancestor)
    {
        for (var current = child.Parent; current is not null; current = current.Parent)
            if (ReferenceEquals(current, ancestor)) return true;

        return false;
    }
}
