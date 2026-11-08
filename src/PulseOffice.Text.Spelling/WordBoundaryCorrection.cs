namespace PulseOffice.Text.Spelling;

public sealed record BoundaryCorrection(int Start, int Length, string Replacement, string Text, int Caret);

public interface IWordBoundaryCorrection
{
    bool IsBoundary(char character);

    BoundaryCorrection? AfterTyping(string before, string after, int caret, AutoCorrectOptions options);

    BoundaryCorrection? AtEnd(string text, AutoCorrectOptions options);
}

public sealed class WordBoundaryCorrection(IAutoCorrect corrector) : IWordBoundaryCorrection
{
    private const string Punctuation = ".,;:!?)]}\"”»";

    public bool IsBoundary(char character) => char.IsWhiteSpace(character) || Punctuation.Contains(character);

    public BoundaryCorrection? AfterTyping(string before, string after, int caret, AutoCorrectOptions options)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(options);

        var inserted = after.Length - before.Length;
        if (inserted < 1 || caret < inserted || caret > after.Length) return null;
        var start = caret - inserted;
        for (var i = start; i < caret; i++)
            if (!IsBoundary(after[i])) return null;
        if (!after.AsSpan(0, start).SequenceEqual(before.AsSpan(0, start))) return null;
        if (!after.AsSpan(caret).SequenceEqual(before.AsSpan(start))) return null;

        return Correct(after, start + 1, start, caret, options);
    }

    public BoundaryCorrection? AtEnd(string text, AutoCorrectOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        if (text.Length == 0 || IsBoundary(text[^1])) return null;
        return Correct(text, text.Length, text.Length, text.Length, options);
    }

    private BoundaryCorrection? Correct(string text, int end, int wordEnd, int caret, AutoCorrectOptions options)
    {
        var start = wordEnd;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        if (start == end) return null;

        var head = text[..start];
        var whole = corrector.Apply(text[..end], options);
        var correctedHead = corrector.Apply(head, options);
        if (!whole.StartsWith(correctedHead, StringComparison.Ordinal)) return null;

        var replacement = whole[correctedHead.Length..];
        var original = text[start..end];
        if (replacement == original) return null;

        var result = string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(end));
        return new BoundaryCorrection(start, end - start, replacement, result, start + replacement.Length + (caret - end));
    }
}
