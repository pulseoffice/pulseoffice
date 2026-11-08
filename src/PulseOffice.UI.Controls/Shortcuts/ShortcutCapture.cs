using Avalonia.Input;
using PulseOffice.UI.Commands.Shortcuts;

namespace PulseOffice.UI.Controls.Shortcuts;

public interface IShortcutCapture
{
    GestureChord? Chord(Key key, KeyModifiers modifiers);

    GestureChord? Chord(ShortcutGesture gesture);
}

public sealed class ShortcutCapture(bool macOS) : IShortcutCapture
{
    private readonly ChordKeyNames _names = new();
    private readonly IReadOnlyDictionary<Key, string> _aliases = new Dictionary<Key, string>
    {
        [Key.PageUp] = "PageUp",
        [Key.PageDown] = "PageDown",
        [Key.PrintScreen] = "PrintScreen",
        [Key.Return] = "Enter",
        [Key.Back] = "Back",
        [Key.Escape] = "Escape",
        [Key.OemSemicolon] = "OemSemicolon",
        [Key.OemQuotes] = "OemQuotes",
        [Key.OemOpenBrackets] = "OemOpenBrackets",
        [Key.OemCloseBrackets] = "OemCloseBrackets",
        [Key.OemPipe] = "OemPipe",
        [Key.OemTilde] = "OemTilde",
        [Key.OemBackslash] = "OemBackslash",
        [Key.Multiply] = "Multiply",
    };

    public ShortcutCapture()
        : this(new PulseOffice.Platform.RuntimeHostSystem().Kind == PulseOffice.Platform.HostSystemKind.MacOS)
    {
    }

    public GestureChord? Chord(ShortcutGesture gesture) => Chord(gesture.Key, gesture.Modifiers);

    public GestureChord? Chord(Key key, KeyModifiers modifiers) =>
        NameOf(key) is { } name && _names.Normalize(name) is var normalized && _names.IsKnown(normalized)
            ? new GestureChord(ModifiersOf(modifiers), normalized)
            : null;

    private ChordModifiers ModifiersOf(KeyModifiers modifiers)
    {
        var chord = ChordModifiers.None;

        if (modifiers.HasFlag(KeyModifiers.Shift)) chord |= ChordModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Alt)) chord |= ChordModifiers.Alt;

        if (macOS)
        {
            if (modifiers.HasFlag(KeyModifiers.Meta)) chord |= ChordModifiers.Primary;
            if (modifiers.HasFlag(KeyModifiers.Control)) chord |= ChordModifiers.Control;
        }
        else
        {
            if (modifiers.HasFlag(KeyModifiers.Control)) chord |= ChordModifiers.Primary;
            if (modifiers.HasFlag(KeyModifiers.Meta)) chord |= ChordModifiers.Super;
        }

        return chord;
    }

    private string? NameOf(Key key) => key switch
    {
        Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.CapsLock or Key.NumLock or Key.Scroll or Key.FnLeftArrow => null,
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        Key.OemPlus or Key.Add => "Plus",
        Key.OemMinus or Key.Subtract => "Minus",
        Key.OemComma => "Comma",
        Key.OemPeriod or Key.Decimal => "Period",
        Key.OemQuestion or Key.Divide => "Slash",
        _ when _aliases.TryGetValue(key, out var alias) => alias,
        _ => key.ToString(),
    };
}
