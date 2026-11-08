using System.Text;

namespace PulseOffice.Text.Rich;

public readonly record struct LineSplit(int End, int Next);

public interface IFirstLineBreaker
{
    LineSplit Break(string content, double available, Func<int, double> prefixWidth);

    LineSplit Break(string content, int start, double available, Func<int, double> prefixWidth);
}

public sealed class FirstLineBreaker : IFirstLineBreaker
{
    private const string HardBreaks = "\n\r\v\u2028";

    public LineSplit Break(string content, double available, Func<int, double> prefixWidth) => Break(content, 0, available, prefixWidth);

    public LineSplit Break(string content, int start, double available, Func<int, double> prefixWidth)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(prefixWidth);
        start = Math.Clamp(start, 0, content.Length);

        var hard = content.AsSpan(start).IndexOfAny(HardBreaks);
        var limit = hard < 0 ? content.Length - start : hard;
        var whole = hard < 0 ? new LineSplit(limit, limit) : new LineSplit(hard, Skip(content, start, hard));

        var candidates = new BreakCandidates(this, content, start, limit);
        if (!candidates.Has(0)) return whole;

        bool Fits(int index) => prefixWidth(Trimmed(content, start, candidates[index])) <= available;

        var lastFit = -1;
        var firstMiss = -1;
        for (var probe = 0; candidates.Has(probe); probe = 2 * probe + 1)
        {
            if (!Fits(probe))
            {
                firstMiss = probe;
                break;
            }
            lastFit = probe;
        }
        if (firstMiss < 0)
        {
            if (prefixWidth(limit) <= available) return whole;
            firstMiss = candidates.Count();
        }

        var (low, high) = (lastFit + 1, firstMiss - 1);
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (Fits(middle))
            {
                lastFit = middle;
                low = middle + 1;
            }
            else high = middle - 1;
        }

        var next = candidates[Math.Max(lastFit, 0)];
        return new LineSplit(Trimmed(content, start, next), next);
    }

    private const string NoLineStart = "、。，．,.!?！？：；:;)]}）］｝〕〉》」』】〙〗〟’”»ー々〻ゝゞヽヾぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ・…‥";

    private const string NoLineEnd = "([{（［｛〔〈《「『【〘〖〝‘“«";

    private bool IsCandidate(string content, int start, int index)
    {
        var at = start + index;
        return char.IsWhiteSpace(content[at - 1]) && !char.IsWhiteSpace(content[at]) || Ideographic(content, start, at);
    }

    private bool Ideographic(string content, int start, int index)
    {
        if (char.IsLowSurrogate(content[index]) || char.IsWhiteSpace(content[index]) || char.IsWhiteSpace(content[index - 1])) return false;
        if (NoLineStart.Contains(content[index]) || NoLineEnd.Contains(content[index - 1])) return false;
        if (char.GetUnicodeCategory(content, index) is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark
            or System.Globalization.UnicodeCategory.Format) return false;

        var before = char.IsLowSurrogate(content[index - 1]) && index - 2 >= start ? index - 2 : index - 1;
        return Asian(content, before) || Asian(content, index);
    }

    private bool Asian(string content, int index) =>
        Rune.TryGetRuneAt(content, index, out var rune) && rune.Value is >= 0x2E80 and <= 0x2FDF or >= 0x3000 and <= 0x30FF
            or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0xFF00 and <= 0xFFEF
            or >= 0x20000 and <= 0x3FFFF;

    private int Trimmed(string content, int start, int end)
    {
        while (end > 0 && char.IsWhiteSpace(content[start + end - 1])) end--;
        return end;
    }

    private int Skip(string content, int start, int hard) =>
        content[start + hard] == '\r' && start + hard + 1 < content.Length && content[start + hard + 1] == '\n' ? hard + 2 : hard + 1;

    private sealed class BreakCandidates(FirstLineBreaker owner, string content, int start, int limit)
    {
        private readonly List<int> _found = [];
        private int _scanned = 1;

        public int this[int index] => _found[index];

        public bool Has(int index)
        {
            while (_found.Count <= index && _scanned < limit)
            {
                if (owner.IsCandidate(content, start, _scanned)) _found.Add(_scanned);
                _scanned++;
            }
            return index < _found.Count;
        }

        public int Count()
        {
            Has(int.MaxValue - 1);
            return _found.Count;
        }
    }
}
