namespace PulseOffice.UI.Commands.Shortcuts;

[Flags]
public enum ChordModifiers
{
    None = 0,
    Primary = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Shift = 1 << 3,
    Super = 1 << 4,
}

public sealed record GestureChord(ChordModifiers Modifiers, string Key)
{
    public bool IsFunctionKey =>
        Key.Length > 1 && Key[0] == 'F' && int.TryParse(Key.AsSpan(1), out var number) && number is >= 1 and <= 24;

    public bool HasCommandModifier =>
        (Modifiers & (ChordModifiers.Primary | ChordModifiers.Control | ChordModifiers.Alt | ChordModifiers.Super)) != 0;

    public string Canonical()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ChordModifiers.Primary)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ChordModifiers.Control)) parts.Add("Control");
        if (Modifiers.HasFlag(ChordModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ChordModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ChordModifiers.Super)) parts.Add("Super");

        parts.Add(Key);

        return string.Join('+', parts);
    }

    public override string ToString() => Canonical();
}

public sealed class ChordKeyNames
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["+"] = "Plus",
        ["-"] = "Minus",
        [","] = "Comma",
        ["."] = "Period",
        ["/"] = "Slash",
        [";"] = "OemSemicolon",
        ["'"] = "OemQuotes",
        ["["] = "OemOpenBrackets",
        ["]"] = "OemCloseBrackets",
        ["\\"] = "OemBackslash",
        ["`"] = "OemTilde",
        ["<"] = "Comma",
        [">"] = "Period",
        ["="] = "Plus",
        ["*"] = "Multiply",
        ["OemMinus"] = "Minus",
        ["OemPlus"] = "Plus",
        ["OemComma"] = "Comma",
        ["OemPeriod"] = "Period",
        ["OemQuestion"] = "Slash",
    };

    private static readonly string[] Named =
    [
        "Plus", "Minus", "Comma", "Period", "Slash", "Delete", "Back", "Enter", "Return", "Escape", "Tab", "Space",
        "Home", "End", "PageUp", "PageDown", "Up", "Down", "Left", "Right", "Insert", "Pause", "PrintScreen",
        "Multiply", "OemSemicolon", "OemQuotes", "OemOpenBrackets", "OemCloseBrackets", "OemPipe", "OemTilde",
        "OemBackslash",
    ];

    public bool IsKnown(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.Length == 1 ? char.IsAsciiLetterOrDigit(key[0])
            : (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24)
              || Named.Contains(key, StringComparer.Ordinal);
    }

    private string OemSpelling(string key) =>
        key.StartsWith("oem", StringComparison.OrdinalIgnoreCase) && key.Length > 3
            ? "Oem" + char.ToUpperInvariant(key[3]) + key[4..].ToLowerInvariant()
            : key;

    public string Normalize(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var trimmed = key.Trim();
        if (Symbols.TryGetValue(trimmed, out var symbol) || Symbols.TryGetValue(OemSpelling(trimmed), out symbol)) return symbol;
        if (trimmed.Length == 1) return trimmed.ToUpperInvariant();

        if (trimmed.Length is 2 or 3 && (trimmed[0] is 'f' or 'F') && trimmed[1..].All(char.IsAsciiDigit))
            return "F" + trimmed[1..];

        return Named.FirstOrDefault(name => name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed;
    }
}

public sealed class GestureChordParser
{
    private readonly ChordKeyNames _keys = new();

    public GestureChord? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        var plusKey = trimmed == "+" || trimmed.EndsWith("++", StringComparison.Ordinal);
        var body = plusKey ? trimmed[..^1] : trimmed;
        var parts = body.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (plusKey) parts.Add("+");
        if (parts.Count == 0) return null;

        var modifiers = ChordModifiers.None;

        foreach (var part in parts.Take(parts.Count - 1))
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "cmd" or "meta" or "primary" => ChordModifiers.Primary,
                "control" => ChordModifiers.Control,
                "alt" or "option" => ChordModifiers.Alt,
                "shift" => ChordModifiers.Shift,
                "super" or "win" => ChordModifiers.Super,
                _ => (ChordModifiers?)null,
            };

            if (modifier is null) return null;
            modifiers |= modifier.Value;
        }

        var key = _keys.Normalize(parts[^1]);
        return _keys.IsKnown(key) ? new GestureChord(modifiers, key) : null;
    }
}
