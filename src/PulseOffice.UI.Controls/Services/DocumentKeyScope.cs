using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PulseOffice.UI.Controls.Services;

public interface IDocumentSurface;

public interface IDocumentKeyScope
{
    bool Allows(KeyEventArgs e, TopLevel? top);

    bool FromSurface(object? source, Visual surface);
}

public sealed class DocumentKeyScope : IDocumentKeyScope
{
    private readonly HashSet<Key> _editingKeys =
    [
        Key.Delete, Key.Back, Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.End,
        Key.PageUp, Key.PageDown, Key.Enter, Key.Space, Key.Tab, Key.Escape,
    ];

    public bool Allows(KeyEventArgs e, TopLevel? top)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) != 0) return true;
        if (!_editingKeys.Contains(e.Key)) return true;
        var focused = top?.FocusManager?.GetFocusedElement();
        return focused is null or TopLevel or IDocumentSurface;
    }

    public bool FromSurface(object? source, Visual surface)
    {
        for (var node = source as Visual; node is not null; node = node.GetVisualParent())
        {
            if (ReferenceEquals(node, surface)) return true;
            if (node is InputElement { Focusable: true } && node is not IDocumentSurface) return false;
        }
        return false;
    }
}
