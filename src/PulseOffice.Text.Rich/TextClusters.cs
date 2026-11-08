using System.Globalization;
using System.Text;

namespace PulseOffice.Text.Rich;

public interface ITextClusters
{
    int Previous(string text, int offset);

    int Next(string text, int offset);
}

public sealed class TextClusters : ITextClusters
{
    public int Previous(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = Math.Clamp(offset, 0, text.Length);
        if (offset == 0) return 0;
        var start = Math.Max(0, offset - 64);
        while (start > 0 && char.IsLowSurrogate(text[start])) start--;
        var previous = start;
        var position = start;
        while (position < offset)
        {
            previous = position;
            position += Math.Max(1, StringInfo.GetNextTextElementLength(text, position));
        }
        return previous;
    }

    public int Next(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = Math.Clamp(offset, 0, text.Length);
        if (offset >= text.Length) return text.Length;
        return Math.Min(text.Length, offset + Math.Max(1, StringInfo.GetNextTextElementLength(text, offset)));
    }
}

public interface IWordMotion
{
    int Previous(string text, int offset);

    int Next(string text, int offset);
}

public sealed class WordMotion(ITextClusters clusters) : IWordMotion
{
    private enum Kind
    {
        Space,
        Punctuation,
        Letter,
        Han,
        Hiragana,
        Katakana,
        Hangul,
    }

    public int Next(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset >= text.Length) return text.Length;
        var kind = KindAt(text, offset);
        if (kind is not (Kind.Space or Kind.Punctuation))
            while (offset < text.Length && KindAt(text, offset) == kind) offset = clusters.Next(text, offset);
        while (offset < text.Length && KindAt(text, offset) is Kind.Space or Kind.Punctuation) offset = clusters.Next(text, offset);
        return offset;
    }

    public int Previous(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset <= 0) return 0;
        while (offset > 0 && KindAt(text, clusters.Previous(text, offset)) is Kind.Space or Kind.Punctuation) offset = clusters.Previous(text, offset);
        if (offset == 0) return 0;
        var kind = KindAt(text, clusters.Previous(text, offset));
        while (offset > 0 && KindAt(text, clusters.Previous(text, offset)) == kind) offset = clusters.Previous(text, offset);
        return offset;
    }

    private Kind KindAt(string text, int index)
    {
        if (!Rune.TryGetRuneAt(text, index, out var rune)) return Kind.Punctuation;
        if (Rune.IsWhiteSpace(rune)) return Kind.Space;
        var value = rune.Value;
        if (value is >= 0x3040 and <= 0x309F) return Kind.Hiragana;
        if (value is >= 0x30A0 and <= 0x30FF or >= 0x31F0 and <= 0x31FF or >= 0xFF66 and <= 0xFF9F) return Kind.Katakana;
        if (value is >= 0xAC00 and <= 0xD7AF or >= 0x1100 and <= 0x11FF or >= 0x3130 and <= 0x318F) return Kind.Hangul;
        if (value is >= 0x4E00 and <= 0x9FFF or >= 0x3400 and <= 0x4DBF or >= 0x20000 and <= 0x2FA1F or >= 0xF900 and <= 0xFAFF) return Kind.Han;
        if (Rune.IsLetterOrDigit(rune)) return Kind.Letter;
        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.ConnectorPunctuation
            ? Kind.Letter
            : text[index] is '\'' or '’' && index > 0 && index + 1 < text.Length && char.IsLetter(text[index - 1]) && char.IsLetter(text[index + 1]) ? Kind.Letter : Kind.Punctuation;
    }
}
