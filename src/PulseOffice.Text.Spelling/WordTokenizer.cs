namespace PulseOffice.Text.Spelling;

public readonly record struct WordSpan(int Start, int Length, string Text)
{
    public int End => Start + Length;
}

public interface IWordTokenizer
{
    IReadOnlyList<WordSpan> Words(string text);
}

public sealed class WordTokenizer : IWordTokenizer
{
    private readonly LinkAreas _links = new();

    public IReadOnlyList<WordSpan> Words(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var words = new List<WordSpan>();
        var skipped = _links.Of(text);
        var index = 0;

        while (index < text.Length)
        {
            if (!IsLetter(text, index))
            {
                index += Width(text, index);
                continue;
            }

            var start = index;
            var end = index;

            while (index < text.Length && IsWordPart(text, index))
            {
                var width = Width(text, index);
                if (IsCore(text, index)) end = index + width;
                index += width;
            }

            index = end;

            var word = text[start..end];

            if (Spellable(word) && !skipped.Any(area => start < area.End && area.Start < end))
                words.Add(new WordSpan(start, word.Length, word));

            if (index == start) index += Width(text, start);
        }

        return words;
    }

    private int Width(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;

    private System.Globalization.UnicodeCategory Category(string text, int index) =>
        Width(text, index) == 2
            ? System.Text.Rune.GetUnicodeCategory(new System.Text.Rune(text[index], text[index + 1]))
            : char.GetUnicodeCategory(text[index]);

    private bool IsLetter(string text, int index) =>
        Category(text, index) is System.Globalization.UnicodeCategory.UppercaseLetter
            or System.Globalization.UnicodeCategory.LowercaseLetter
            or System.Globalization.UnicodeCategory.TitlecaseLetter
            or System.Globalization.UnicodeCategory.ModifierLetter
            or System.Globalization.UnicodeCategory.OtherLetter;

    private bool IsCore(string text, int index) =>
        IsLetter(text, index)
        || Category(text, index) is System.Globalization.UnicodeCategory.DecimalDigitNumber
            or System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark;

    private bool IsWordPart(string text, int index)
    {
        if (IsCore(text, index)) return true;

        if (text[index] is not ('\'' or '’' or '-')) return false;

        return index + 1 < text.Length && IsLetter(text, index + 1);
    }

    private bool Spellable(string word)
    {
        if (word.Length < 2) return false;
        if (!word.Any(char.IsLetter) && !word.Any(char.IsSurrogate)) return false;
        if (word.Any(char.IsDigit)) return false;

        return true;
    }
}
