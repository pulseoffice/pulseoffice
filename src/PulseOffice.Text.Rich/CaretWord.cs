namespace PulseOffice.Text.Rich;

public interface ICaretWord
{
    (int Start, int End) Around(string text, int caret);

    (int Start, int End) Enclosing(string text, int index);
}

public sealed class CaretWord : ICaretWord
{
    public (int Start, int End) Around(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        caret = Math.Clamp(caret, 0, text.Length);
        if (caret == 0 || caret == text.Length || !Part(text, caret - 1) || !Part(text, caret)) return (caret, caret);

        var start = caret;
        while (start > 0 && Part(text, start - 1)) start--;

        var end = caret;
        while (end < text.Length && Part(text, end)) end++;

        return (start, end);
    }

    public (int Start, int End) Enclosing(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(index, 0, text.Length);
        var end = start;
        while (start > 0 && Part(text, start - 1)) start--;
        while (end < text.Length && Part(text, end)) end++;
        return (start, end);
    }

    private bool Part(string text, int index) =>
        Letter(text, index)
        || (text[index] is '\'' or '’' && index > 0 && index < text.Length - 1
            && Letter(text, index - 1) && Letter(text, index + 1));

    private bool Letter(string text, int index)
    {
        var c = text[index];
        if (char.IsLowSurrogate(c) && index > 0 && char.IsHighSurrogate(text[index - 1])) index--;
        if (!System.Text.Rune.TryGetRuneAt(text, index, out var rune)) return false;
        return System.Text.Rune.GetUnicodeCategory(rune) switch
        {
            System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark
                or System.Globalization.UnicodeCategory.EnclosingMark => true,
            _ => System.Text.Rune.IsLetterOrDigit(rune),
        };
    }
}
