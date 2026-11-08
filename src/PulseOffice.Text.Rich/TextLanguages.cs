namespace PulseOffice.Text.Rich;

public sealed record TextLanguages(string? Western = null, string? Asian = null, string? Complex = null)
{
    public static TextLanguages None { get; } = new();

    public bool IsEmpty => Western is null && Asian is null && Complex is null;
}

public interface ITextScripts
{
    bool HasEastAsian(string text);

    bool IsEastAsianLanguage(string? tag);
}

public sealed class TextScripts : ITextScripts
{
    private readonly HashSet<string> _eastAsianLanguages = new(StringComparer.OrdinalIgnoreCase) { "zh", "ja", "ko", "yue", "lzh", "wuu", "hak", "nan", "cmn" };

    public bool IsEastAsianLanguage(string? tag) =>
        tag is { Length: > 0 } && _eastAsianLanguages.Contains(tag.Replace('_', '-').Split('-')[0]);

    public bool HasEastAsian(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var rune in text.EnumerateRunes())
            if (IsEastAsian(rune.Value)) return true;
        return false;
    }

    private static bool IsEastAsian(int code) =>
        code is >= 0x1100 and <= 0x11FF
            or >= 0x2E80 and <= 0x2FDF
            or >= 0x3000 and <= 0x30FF
            or >= 0x3100 and <= 0x318F
            or >= 0x31A0 and <= 0x31FF
            or >= 0x3200 and <= 0x9FFF
            or >= 0xA960 and <= 0xA97F
            or >= 0xAC00 and <= 0xD7FF
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFFEF
            or >= 0x20000 and <= 0x3FFFF;
}

public interface ISymbolFonts
{
    bool IsSymbolic(string? font);
}

public sealed class SymbolFonts : ISymbolFonts
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase)
    {
        "Wingdings", "Wingdings 2", "Wingdings 3", "Webdings", "Symbol", "Marlett", "MT Extra", "MS Reference Specialty",
    };

    public bool IsSymbolic(string? font) => font is { Length: > 0 } && _names.Contains(font.Trim());
}

public sealed record ScriptFonts(string? Asian = null, string? Complex = null)
{
    public static ScriptFonts None { get; } = new();

    public bool IsEmpty => Asian is null && Complex is null;
}
