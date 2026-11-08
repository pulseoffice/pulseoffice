using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace PulseOffice.UI.Controls.Text;

public interface ITextEditingCompanion
{
}

public interface ITextEditingFocus
{
    bool KeepsEditing(Visual owner);
}

public sealed class TextEditingFocus : ITextEditingFocus
{
    public bool KeepsEditing(Visual owner)
    {
        if (TopLevel.GetTopLevel(owner)?.FocusManager?.GetFocusedElement() is not Visual focused) return true;
        if (TopLevel.GetTopLevel(focused) is PopupRoot) return true;
        return focused is ITextEditingCompanion || focused.GetVisualAncestors().Any(ancestor => ancestor is ITextEditingCompanion);
    }
}
