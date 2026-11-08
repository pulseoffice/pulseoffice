using Avalonia.Input;

namespace PulseOffice.Presentations.UI;

public interface ISlideKeyNavigation
{
    int? Target(Key key, KeyModifiers modifiers, int current, int count, bool listFocused);
}

public sealed class SlideKeyNavigation : ISlideKeyNavigation
{
    public int? Target(Key key, KeyModifiers modifiers, int current, int count, bool listFocused)
    {
        if (count == 0 || modifiers != KeyModifiers.None) return null;
        int? target = key switch
        {
            Key.PageUp => current - 1,
            Key.PageDown => current + 1,
            Key.Home => 0,
            Key.End => count - 1,
            Key.Up or Key.Left when listFocused => current - 1,
            Key.Down or Key.Right when listFocused => current + 1,
            _ => null,
        };
        return target is { } index ? Math.Clamp(index, 0, count - 1) : null;
    }
}
