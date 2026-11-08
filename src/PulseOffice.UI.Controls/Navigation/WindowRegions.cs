using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PulseOffice.UI.Controls.Navigation;

public interface IWindowRegion
{
    bool IsAvailable { get; }

    bool ContainsFocus { get; }

    bool Focus();
}

public sealed class DelegateWindowRegion(Func<bool> isAvailable, Func<bool> containsFocus, Func<bool> focus) : IWindowRegion
{
    public bool IsAvailable => isAvailable();

    public bool ContainsFocus => containsFocus();

    public bool Focus() => focus();
}

public sealed class ControlWindowRegion(Control root) : IWindowRegion
{
    public bool IsAvailable => root.IsEffectivelyVisible && FirstFocusable() is not null;

    public bool ContainsFocus => root.IsKeyboardFocusWithin;

    public bool Focus() => FirstFocusable() is { } target && target.Focus(NavigationMethod.Tab);

    private InputElement? FirstFocusable() =>
        root.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled && element.IsTabStop);
}

public interface IWindowRegionCycle
{
    bool Move(IReadOnlyList<IWindowRegion> regions, bool backwards);
}

public sealed class WindowRegionCycle : IWindowRegionCycle
{
    public bool Move(IReadOnlyList<IWindowRegion> regions, bool backwards)
    {
        if (regions.Count == 0) return false;

        var current = -1;
        for (var index = 0; index < regions.Count; index++)
            if (regions[index].ContainsFocus) { current = index; break; }

        var step = backwards ? -1 : 1;
        var start = current < 0 ? (backwards ? 0 : regions.Count - 1) : current;

        for (var offset = 1; offset <= regions.Count; offset++)
        {
            var candidate = regions[((start + step * offset) % regions.Count + regions.Count) % regions.Count];
            if (candidate.IsAvailable && !candidate.ContainsFocus && candidate.Focus()) return true;
        }
        return false;
    }
}

public interface IDocumentRegions
{
    IReadOnlyList<IWindowRegion> Regions { get; }
}

public sealed class KeyboardFocusFrame
{
    private readonly Border _frame;

    public KeyboardFocusFrame(Border frame, InputElement target)
    {
        _frame = frame;
        _frame.BorderThickness = new Avalonia.Thickness(2);
        _frame.BorderBrush = Avalonia.Media.Brushes.Transparent;
        target.GotFocus += (_, e) => Show(e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
        target.LostFocus += (_, _) => Show(false);
    }

    private void Show(bool visible) =>
        _frame.BorderBrush = visible && _frame.FindResource(PulseOffice.UI.Theming.AccentService.AccentKey) is Avalonia.Media.IBrush accent
            ? accent
            : Avalonia.Media.Brushes.Transparent;
}
