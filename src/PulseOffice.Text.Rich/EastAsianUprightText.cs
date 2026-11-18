using System.Buffers;
using System.Globalization;
using System.Text;

namespace PulseOffice.Text.Rich;

public readonly record struct TextCluster(int Start, int End);

public interface IEastAsianUprightText
{
    bool Contains(string? text);

    IReadOnlyList<TextCluster> Clusters(string text);
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
            if (Upright(rune.Value)) return true;
        return false;
    }

    public IReadOnlyList<TextCluster> Clusters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return [];
        var starts = StringInfo.ParseCombiningCharacters(text);
        var clusters = new List<TextCluster>();
        for (var index = 0; index < starts.Length; index++)
        {
            var start = starts[index];
            var end = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            if (Rune.DecodeFromUtf16(text.AsSpan(start, end - start), out var rune, out _) == OperationStatus.Done && Upright(rune.Value))
                clusters.Add(new TextCluster(start, end));
        }
        return clusters;
    }

    private bool Upright(int value)
    {
        foreach (var (first, last) in _ranges)
            if (value >= first && value <= last) return true;
        return false;
    }
}
