using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Outline;

public sealed class ListThreadCounters(StyleSheet sheet)
{
    private const string ThreadPrefix = "\u0001";

    private readonly Dictionary<string, int?[]> _counts = new(StringComparer.Ordinal);

    public string Thread(TextParagraph paragraph) => paragraph.ListId is { } thread ? ThreadPrefix + thread : paragraph.ListStyleName ?? string.Empty;

    public int Next(TextParagraph paragraph, int level)
    {
        var counts = Counts(paragraph);
        var clamped = Math.Clamp(level, 0, ListStyle.LevelCount - 1);
        if (paragraph.ListHeader) return counts[clamped] ?? Start(paragraph, clamped);
        counts[clamped] = paragraph.ListRestart is { } restart ? restart : counts[clamped] is { } previous ? previous + 1 : Start(paragraph, clamped);
        for (var deeper = clamped + 1; deeper < counts.Length; deeper++) counts[deeper] = null;
        return counts[clamped]!.Value;
    }

    public int Upcoming(TextParagraph paragraph, int level)
    {
        var clamped = Math.Clamp(level, 0, ListStyle.LevelCount - 1);
        return Counts(paragraph)[clamped] is { } current ? current + 1 : Start(paragraph, clamped);
    }

    private int?[] Counts(TextParagraph paragraph)
    {
        var key = Thread(paragraph);
        if (!_counts.TryGetValue(key, out var counts))
        {
            counts = new int?[ListStyle.LevelCount];
            _counts[key] = counts;
        }
        return counts;
    }

    private int Start(TextParagraph paragraph, int level) => sheet.List.Find(paragraph.ListStyleName)?.Level(level).StartAt ?? 1;
}
