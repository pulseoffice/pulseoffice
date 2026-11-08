using Avalonia.Input;
using Avalonia.Interactivity;

namespace PulseOffice.UI.Controls.Shortcuts;

public sealed class TypingChordGuard
{
    private bool _chord;

    public void Attach(InputElement surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        surface.AddHandler(InputElement.KeyDownEvent, (_, e) => _chord = e.Handled || IsChord(e.KeyModifiers), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public bool Rejects(TextInputEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return _chord || string.IsNullOrEmpty(e.Text) || e.Text.All(char.IsControl);
    }

    private static bool IsChord(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Meta) || modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Alt);
}
