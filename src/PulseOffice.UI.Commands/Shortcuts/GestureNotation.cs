namespace PulseOffice.UI.Commands.Shortcuts;

public interface IGestureNotation
{
    string Display(GestureChord chord);

    string Display(string? gesture);

    IReadOnlyList<string> Keys(string? gesture);
}

public abstract class GestureNotation : IGestureNotation
{
    private readonly GestureChordParser _parser = new();

    public static IGestureNotation ForSystem(bool macOS) => macOS ? new MacGestureNotation() : new PcGestureNotation();

    public string Display(string? gesture) =>
        _parser.Parse(gesture) is { } chord ? Display(chord) : gesture ?? string.Empty;

    public IReadOnlyList<string> Keys(string? gesture) =>
        _parser.Parse(gesture) is { } chord
            ? [.. ModifierLabels(chord.Modifiers), KeyLabel(chord.Key)]
            : string.IsNullOrWhiteSpace(gesture) ? [] : [gesture];

    public string Display(GestureChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return Compose(ModifierLabels(chord.Modifiers).ToList(), KeyLabel(chord.Key));
    }

    protected abstract IEnumerable<string> ModifierLabels(ChordModifiers modifiers);

    protected abstract string Compose(IReadOnlyList<string> modifiers, string key);

    protected virtual string KeyLabel(string key) => key switch
    {
        "Plus" => "+",
        "Minus" => "−",
        "Comma" => ",",
        "Period" => ".",
        "Slash" => "/",
        "OemSemicolon" => ";",
        "OemQuotes" => "'",
        "OemOpenBrackets" => "[",
        "OemCloseBrackets" => "]",
        "OemBackslash" or "OemPipe" => "\\",
        "OemTilde" => "`",
        "Multiply" => "*",
        _ => key,
    };
}

public sealed class MacGestureNotation : GestureNotation
{
    protected override IEnumerable<string> ModifierLabels(ChordModifiers modifiers)
    {
        if (modifiers.HasFlag(ChordModifiers.Control)) yield return "⌃";
        if (modifiers.HasFlag(ChordModifiers.Alt)) yield return "⌥";
        if (modifiers.HasFlag(ChordModifiers.Shift)) yield return "⇧";
        if (modifiers.HasFlag(ChordModifiers.Primary) || modifiers.HasFlag(ChordModifiers.Super)) yield return "⌘";
    }

    protected override string Compose(IReadOnlyList<string> modifiers, string key) => string.Concat(modifiers) + key;

    protected override string KeyLabel(string key) => key switch
    {
        "Delete" => "⌦",
        "Back" => "⌫",
        "Enter" or "Return" => "↩",
        "Escape" => "⎋",
        "Tab" => "⇥",
        "Up" => "↑",
        "Down" => "↓",
        "Left" => "←",
        "Right" => "→",
        "PageUp" => "⇞",
        "PageDown" => "⇟",
        "Home" => "↖",
        "End" => "↘",
        _ => base.KeyLabel(key),
    };
}

public sealed class PcGestureNotation : GestureNotation
{
    protected override IEnumerable<string> ModifierLabels(ChordModifiers modifiers)
    {
        if (modifiers.HasFlag(ChordModifiers.Primary) || modifiers.HasFlag(ChordModifiers.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(ChordModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(ChordModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(ChordModifiers.Super)) yield return "Win";
    }

    protected override string Compose(IReadOnlyList<string> modifiers, string key) =>
        string.Join('+', [.. modifiers, key]);

    protected override string KeyLabel(string key) => key switch
    {
        "Delete" => "Del",
        "Back" => "Backspace",
        "Escape" => "Esc",
        "Up" => "↑",
        "Down" => "↓",
        "Left" => "←",
        "Right" => "→",
        _ => base.KeyLabel(key),
    };
}
