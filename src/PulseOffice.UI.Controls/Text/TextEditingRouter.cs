using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Shortcuts;

namespace PulseOffice.UI.Controls.Text;

public enum TextEditingAction { SelectAll, Copy, Cut, Paste, Undo, Redo }

public interface ITextEditingTarget
{
    void Perform(TextEditingAction action);
}

public interface IKeyCaptureTarget
{
    void Capture(ShortcutGesture gesture);
}

public interface ITextEditingRouter
{
    bool IsEditingText(TopLevel? top);

    bool TryRoute(string? gesture);
}

public sealed class TextEditingRouter : ITextEditingRouter
{
    private readonly IReadOnlyList<(ShortcutGesture Gesture, TextEditingAction Action)> _gestures =
    [
        .. new (string Gesture, TextEditingAction Action)[]
        {
            ("Ctrl+A", TextEditingAction.SelectAll),
            ("Ctrl+C", TextEditingAction.Copy),
            ("Ctrl+X", TextEditingAction.Cut),
            ("Ctrl+V", TextEditingAction.Paste),
            ("Ctrl+Z", TextEditingAction.Undo),
            ("Ctrl+Shift+Z", TextEditingAction.Redo),
            ("Ctrl+Y", TextEditingAction.Redo),
        }
        .Select(entry => (ShortcutGesture.Parse(entry.Gesture)!.Value, entry.Action)),
    ];

    public bool IsEditingText(TopLevel? top) => Target(top) is not null;

    public bool TryRoute(string? gesture)
    {
        if (ShortcutGesture.Parse(gesture) is not { } parsed) return false;

        var top = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (CaptureTarget(top) is { } capture)
        {
            capture.Capture(parsed);
            return true;
        }

        var match = _gestures.Where(entry => entry.Gesture.Key == parsed.Key && entry.Gesture.Modifiers == parsed.Modifiers)
            .Select(entry => (TextEditingAction?)entry.Action)
            .FirstOrDefault();
        if (match is not { } action) return false;

        switch (Target(top))
        {
            case TextBox box:
                Perform(box, action);
                return true;
            case ITextEditingTarget target:
                target.Perform(action);
                return true;
            default:
                return false;
        }
    }

    private IKeyCaptureTarget? CaptureTarget(TopLevel? top) =>
        top?.FocusManager?.GetFocusedElement() is Visual focused
            ? focused.GetSelfAndVisualAncestors().OfType<IKeyCaptureTarget>().FirstOrDefault()
            : null;

    private object? Target(TopLevel? top)
    {
        if (top?.FocusManager?.GetFocusedElement() is not Visual focused) return null;
        if (focused is TextBox box and not Editing.TextInputSink) return box;
        return focused.GetSelfAndVisualAncestors().OfType<ITextEditingTarget>().FirstOrDefault();
    }

    private readonly IReadOnlyDictionary<TextEditingAction, Action<TextBox>> _boxActions = new Dictionary<TextEditingAction, Action<TextBox>>
    {
        [TextEditingAction.SelectAll] = box => box.SelectAll(),
        [TextEditingAction.Copy] = box => box.Copy(),
        [TextEditingAction.Cut] = box => box.Cut(),
        [TextEditingAction.Paste] = box => box.Paste(),
        [TextEditingAction.Undo] = box => box.Undo(),
        [TextEditingAction.Redo] = box => box.Redo(),
    };

    private void Perform(TextBox box, TextEditingAction action) => _boxActions[action](box);
}
