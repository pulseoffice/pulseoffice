namespace PulseOffice.Text.Rich;

public interface IEastAsianUprightText
{
    bool Contains(string? text);
}

public sealed class EastAsianUprightText : IEastAsianUprightText
{
    private readonly (int First, int Last)[] _ranges =
    [
        (0x1100, 0x11FF),
        (0x2E80, 0x2FDF),
        (0x3000, 0x303F),
        (0x3040, 0x30FF),
        (0x3100, 0x31FF),
        (0x3200, 0x4DBF),
        (0x4E00, 0x9FFF),
        (0xA960, 0xA97F),
        (0xAC00, 0xD7FF),
        (0xF900, 0xFAFF),
        (0xFE30, 0xFE4F),
        (0xFF00, 0xFFEF),
        (0x20000, 0x3FFFF),
    ];

    public bool Contains(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var rune in text.EnumerateRunes())
            foreach (var (first, last) in _ranges)
                if (rune.Value >= first && rune.Value <= last) return true;
        return false;
    }
}
