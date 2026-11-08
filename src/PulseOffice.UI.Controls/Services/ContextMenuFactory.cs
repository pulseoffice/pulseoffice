using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Services;

public interface IContextMenuFactory
{
    ContextMenu? Build(string root);

    bool Open(string root, Control host, Action? closed = null);

    bool OpenAt(string root, Control host, Avalonia.Rect anchor, Action? closed = null);
}

public interface IContextMenuKey
{
    bool Matches(Avalonia.Input.KeyEventArgs e);
}

public sealed class ContextMenuKey : IContextMenuKey
{
    public bool Matches(Avalonia.Input.KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e.Key == Avalonia.Input.Key.Apps && e.KeyModifiers == Avalonia.Input.KeyModifiers.None
               || e.Key == Avalonia.Input.Key.F10 && e.KeyModifiers == Avalonia.Input.KeyModifiers.Shift;
    }
}
