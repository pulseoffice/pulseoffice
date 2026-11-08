using System.Text;

namespace PulseOffice.Text.Rich;

public readonly record struct ScriptSpan(int Start, int Length, bool Asian);

public interface IAsianScriptRanges
{
    IReadOnlyList<ScriptSpan> Split(string text, int start, int length);
}

public sealed class AsianScriptRanges : IAsianScriptRanges
{
    public IReadOnlyList<ScriptSpan> Split(string text, int start, int length)
    {
        var spans = new List<ScriptSpan>();
        var end = Math.Min(text.Length, start + length);
        var index = Math.Max(0, start);
        var spanStart = index;
        bool? current = null;
        while (index < end)
        {
            var width = char.IsHighSurrogate(text[index]) && index + 1 < end && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            var asian = Rune.TryGetRuneAt(text, index, out var rune) ? IsAsian(rune.Value) ?? current ?? false : current ?? false;
            if (current is { } previous && previous != asian)
            {
                spans.Add(new ScriptSpan(spanStart, index - spanStart, previous));
                spanStart = index;
            }
            current = asian;
            index += width;
        }
        if (current is { } last && index > spanStart) spans.Add(new ScriptSpan(spanStart, index - spanStart, last));
        return spans;
    }

    private bool? IsAsian(int code) => code switch
    {
        >= 0x1100 and <= 0x11FF => true,
        >= 0x2E80 and <= 0x2FDF => true,
        >= 0x3000 and <= 0x303F => true,
        >= 0x3040 and <= 0x30FF => true,
        >= 0x3100 and <= 0x31FF => true,
        >= 0x3200 and <= 0x33FF => true,
        >= 0x3400 and <= 0x4DBF => true,
        >= 0x4E00 and <= 0x9FFF => true,
        >= 0xA960 and <= 0xA97F => true,
        >= 0xAC00 and <= 0xD7FF => true,
        >= 0xF900 and <= 0xFAFF => true,
        >= 0xFE30 and <= 0xFE4F => true,
        >= 0xFF00 and <= 0xFFEF => true,
        >= 0x20000 and <= 0x3FFFF => true,
        ' ' or '\t' => null,
        _ => false,
    };
}
