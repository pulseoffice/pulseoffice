using System.Text;

namespace PulseOffice.Documents.Layout.Lines;

public interface ILineBreakOpportunities
{
    bool BreaksBefore(string text, int index);

    bool Ideographic(string text, int index);
}

public sealed class IdeographicBreaks : ILineBreakOpportunities
{
    private const string CannotStart = "、。，．・：；？！ー）」』】〕〉》〙〗｝］゛゜ヽヾゝゞ々〻ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ‐–〜～％‰℃”’）],.!?:;})";

    private const string CannotEnd = "（「『【〔〈《〘〖｛［“‘([{";

    public bool BreaksBefore(string text, int index)
    {
        if (index <= 0 || index >= text.Length || char.IsLowSurrogate(text[index])) return false;
        var previous = char.IsLowSurrogate(text[index - 1]) && index >= 2 ? index - 2 : index - 1;
        if (text[index] is ' ' or '\t' || text[previous] is ' ' or '\t') return false;
        if (!Ideographic(text, index) && !Ideographic(text, previous)) return false;
        return !CannotStart.Contains(text[index]) && !CannotEnd.Contains(text[previous]);
    }

    public bool Ideographic(string text, int index)
    {
        if (index < 0 || index >= text.Length || !Rune.TryGetRuneAt(text, index, out var rune)) return false;
        return rune.Value switch
        {
            >= 0x2E80 and <= 0x2FDF => true,
            >= 0x3000 and <= 0x303F => true,
            >= 0x3040 and <= 0x30FF => true,
            >= 0x3100 and <= 0x31FF => true,
            >= 0x3200 and <= 0x33FF => true,
            >= 0x3400 and <= 0x4DBF => true,
            >= 0x4E00 and <= 0x9FFF => true,
            >= 0xF900 and <= 0xFAFF => true,
            >= 0xFE30 and <= 0xFE4F => true,
            >= 0xFF01 and <= 0xFF60 => true,
            >= 0x20000 and <= 0x3FFFF => true,
            _ => false,
        };
    }
}
