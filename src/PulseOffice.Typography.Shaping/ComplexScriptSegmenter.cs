using System.Globalization;
using System.Text;

namespace PulseOffice.Typography.Shaping;

public interface IComplexScriptSegmenter
{
    IReadOnlyList<ShapingSpan> Spans(string text);
}

public sealed class ComplexScriptSegmenter : IComplexScriptSegmenter
{
    private const int ZeroWidthJoiner = 0x200D;
    private const int ZeroWidthNonJoiner = 0x200C;

    public IReadOnlyList<ShapingSpan> Spans(string text)
    {
        var spans = new List<ShapingSpan>();
        if (string.IsNullOrEmpty(text)) return spans;

        var index = 0;
        while (index < text.Length)
        {
            var (rune, _) = At(text, index);
            if (Joining(rune.Value))
            {
                var end = ScriptEnd(text, index);
                Add(spans, index, end);
                index = end;
                continue;
            }

            var element = StringInfo.GetNextTextElementLength(text, index);
            if (EmojiSequence(text, index, element)) Add(spans, index, index + element);
            index += Math.Max(1, element);
        }

        return spans;
    }

    private int ScriptEnd(string text, int start)
    {
        var end = start;
        var index = start;
        while (index < text.Length)
        {
            var (rune, consumed) = At(text, index);
            var value = rune.Value;
            if (Joining(value) || Mark(rune) || value is ZeroWidthJoiner or ZeroWidthNonJoiner)
            {
                index += consumed;
                end = index;
                continue;
            }

            if (!Neutral(rune)) break;
            index += consumed;
        }

        return end;
    }

    private void Add(List<ShapingSpan> spans, int start, int end)
    {
        if (end <= start) return;
        if (spans.Count > 0 && spans[^1].End == start) spans[^1] = new ShapingSpan(spans[^1].Start, end - spans[^1].Start);
        else spans.Add(new ShapingSpan(start, end - start));
    }

    private (Rune Rune, int Consumed) At(string text, int index)
    {
        Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var consumed);
        return (rune, Math.Max(1, consumed));
    }

    private bool EmojiSequence(string text, int start, int length)
    {
        if (length < 2) return false;
        foreach (var rune in text.AsSpan(start, length).EnumerateRunes())
        {
            if (rune.Value is ZeroWidthJoiner or 0xFE0F or 0x20E3 or (>= 0x1F3FB and <= 0x1F3FF) or (>= 0x1F1E6 and <= 0x1F1FF) or (>= 0xE0020 and <= 0xE007F))
                return true;
        }

        return false;
    }

    private bool Joining(int codePoint) =>
        codePoint is (>= 0x0590 and <= 0x08FF) or (>= 0x0900 and <= 0x0DFF) or (>= 0x0E00 and <= 0x0EFF) or (>= 0x0F00 and <= 0x0FFF)
            or (>= 0x1000 and <= 0x109F) or (>= 0x1780 and <= 0x17FF) or (>= 0x1800 and <= 0x18AF) or (>= 0x1A00 and <= 0x1AAF)
            or (>= 0x1B00 and <= 0x1BFF) or (>= 0xA8E0 and <= 0xA8FF) or (>= 0xA980 and <= 0xA9DF) or (>= 0xFB1D and <= 0xFDFF)
            or (>= 0xFE70 and <= 0xFEFF and not 0xFEFF) or (>= 0x10A00 and <= 0x10A5F) or (>= 0x11000 and <= 0x111FF);

    private bool Mark(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    private bool Neutral(Rune rune) =>
        rune.Value == ' ' || Rune.GetUnicodeCategory(rune) is UnicodeCategory.SpaceSeparator or UnicodeCategory.OtherPunctuation
            or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation;
}
