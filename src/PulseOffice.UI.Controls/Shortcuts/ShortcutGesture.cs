using Avalonia.Input;
using PulseOffice.Platform;
using PulseOffice.UI.Commands.Shortcuts;

namespace PulseOffice.UI.Controls.Shortcuts;

public readonly record struct ShortcutGesture(Key Key, KeyModifiers Modifiers)
{
    private static readonly IHostSystem Host = new RuntimeHostSystem();

    public static KeyModifiers Primary => Host.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control;

    private static readonly GestureChordParser Parser = new();

    public static ShortcutGesture? Parse(string? text)
    {
        if (Parser.Parse(text) is not { } chord || KeyOf(chord.Key) is not { } key) return null;

        var modifiers = KeyModifiers.None;
        if (chord.Modifiers.HasFlag(ChordModifiers.Primary)) modifiers |= Primary;
        if (chord.Modifiers.HasFlag(ChordModifiers.Control)) modifiers |= KeyModifiers.Control;
        if (chord.Modifiers.HasFlag(ChordModifiers.Alt)) modifiers |= KeyModifiers.Alt;
        if (chord.Modifiers.HasFlag(ChordModifiers.Shift)) modifiers |= KeyModifiers.Shift;
        if (chord.Modifiers.HasFlag(ChordModifiers.Super)) modifiers |= KeyModifiers.Meta;

        return new ShortcutGesture(key, modifiers);
    }

    private static Key? KeyOf(string name)
    {
        var normalized = name switch
        {
            "Plus" => "OemPlus",
            "Minus" => "OemMinus",
            "Comma" => "OemComma",
            "Period" => "OemPeriod",
            "Slash" => "OemQuestion",
            "Back" => "Back",
            _ => name,
        };

        if (normalized.Length == 1 && char.IsAsciiDigit(normalized[0]))
            normalized = "D" + normalized;

        return Enum.TryParse<Key>(normalized, ignoreCase: true, out var key) && Enum.IsDefined(key) && key != Key.None
            ? key
            : null;
    }

    public bool Matches(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.KeyModifiers != Modifiers) return false;
        if (e.Key == Key) return true;

        return (Key, e.Key) switch
        {
            (Key.OemPlus, Key.Add) => true,
            (Key.OemMinus, Key.Subtract) => true,
            _ => false,
        };
    }

    private static readonly IGestureNotation Notation = GestureNotation.ForSystem(Host.IsMacOS);

    public string Display() => Notation.Display(ToChord());

    public GestureChord ToChord() => new ShortcutCapture(Host.IsMacOS).Chord(Key, Modifiers)
        ?? new GestureChord(ChordModifiers.None, Key.ToString());
}
