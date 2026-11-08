using System.Globalization;
using System.Text;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record ListPlacement(string Label, double ExtraMargin, double LabelPosition)
{
    public bool FollowedByTab { get; init; } = true;

    public ListBulletPicture? Picture { get; init; }

    public string Key => Picture is null ? Label : Label + "\uFFFC" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Picture).ToString(CultureInfo.InvariantCulture);
}

public interface INumberFormatter
{
    NumberFormat Format { get; }

    string Render(int value);
}

public sealed class ArabicNumbers : INumberFormatter
{
    public NumberFormat Format => NumberFormat.Arabic;

    public string Render(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed class LetterNumbers(bool upper) : INumberFormatter
{
    private const int LongestRepetition = 30;

    public NumberFormat Format => upper ? NumberFormat.UpperLetter : NumberFormat.LowerLetter;

    public string Render(int value)
    {
        if (value <= 0) return string.Empty;
        if ((value - 1) / 26 >= LongestRepetition) return value.ToString(CultureInfo.InvariantCulture);
        var letter = (char)((upper ? 'A' : 'a') + (value - 1) % 26);
        return new string(letter, (value - 1) / 26 + 1);
    }
}

public sealed class RomanNumbers(bool upper) : INumberFormatter
{
    private readonly (int Value, string Symbol)[] _symbols =
    [
        (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"),
        (50, "l"), (40, "xl"), (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i"),
    ];

    public NumberFormat Format => upper ? NumberFormat.UpperRoman : NumberFormat.LowerRoman;

    public string Render(int value)
    {
        var builder = new StringBuilder();
        var rest = Math.Clamp(value, 0, 3999);
        foreach (var (amount, symbol) in _symbols)
        {
            while (rest >= amount)
            {
                builder.Append(symbol);
                rest -= amount;
            }
        }
        return upper ? builder.ToString().ToUpperInvariant() : builder.ToString();
    }
}

public interface IListLabeling
{
    IReadOnlyList<ListPlacement?> Place(StyleSheet sheet, IReadOnlyList<TextParagraph> paragraphs);
}

public sealed class ListLabeling(IListLabels flagLabels, IEnumerable<INumberFormatter> formatters, LayoutOptions options) : IListLabeling
{
    private const string ThreadPrefix = "\u0001";

    private readonly IReadOnlyDictionary<NumberFormat, INumberFormatter> _formatters = formatters.ToDictionary(formatter => formatter.Format);

    public IReadOnlyList<ListPlacement?> Place(StyleSheet sheet, IReadOnlyList<TextParagraph> paragraphs)
    {
        var flags = flagLabels.For(paragraphs);
        var counters = new Dictionary<string, int?[]>(StringComparer.Ordinal);
        var result = new ListPlacement?[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var style = sheet.List.Find(paragraph.ListStyleName);
            if (style is not null)
            {
                result[i] = paragraph.ListHeader ? Header(style, paragraph) : Styled(style, paragraph, counters);
                continue;
            }
            if (!paragraph.IsListItem && paragraph.OutlineLevel is { } outline && outline is > 0 and <= ListStyle.LevelCount
                && sheet.Outline.Level(outline - 1).Format != NumberFormat.None)
            {
                result[i] = Styled(sheet.Outline, paragraph with { BulletLevel = outline - 1 }, counters);
                continue;
            }
            if (!paragraph.IsListItem) continue;
            var indent = options.ListIndentPerLevel;
            result[i] = new ListPlacement(flags[i], indent * (Math.Clamp(paragraph.BulletLevel, 0, ListStyle.LevelCount - 1) + 1), -indent);
        }
        return result;
    }

    private ListPlacement Styled(ListStyle style, TextParagraph paragraph, Dictionary<string, int?[]> counters)
    {
        var level = Math.Clamp(paragraph.BulletLevel, 0, Math.Max(0, Math.Min(style.Levels.Count, ListStyle.LevelCount) - 1));
        var definition = style.Level(level);
        var key = paragraph.ListId is { } thread ? ThreadPrefix + thread : style.Name;
        if (!counters.TryGetValue(key, out var counts))
        {
            counts = new int?[ListStyle.LevelCount];
            counters[key] = counts;
        }
        var start = definition.StartAt;
        counts[level] = paragraph.ListRestart is { } restart ? restart : counts[level] is { } previous ? previous + 1 : start;
        for (var deeper = level + 1; deeper < counts.Length; deeper++) counts[deeper] = null;
        var label = definition.Format switch
        {
            NumberFormat.None => string.Empty,
            NumberFormat.Bullet => definition.Bullet,
            _ => definition.Prefix + Numbers(style, counts, level, definition.DisplayLevels) + definition.Suffix,
        };
        return new ListPlacement(label, definition.Indent + definition.Hanging, -definition.Hanging)
        {
            Picture = definition.Format == NumberFormat.Bullet && definition.Picture is { Data.Length: > 0 } picture ? picture : null,
        };
    }

    private ListPlacement Header(ListStyle style, TextParagraph paragraph)
    {
        var definition = style.Level(Math.Clamp(paragraph.BulletLevel, 0, Math.Max(0, Math.Min(style.Levels.Count, ListStyle.LevelCount) - 1)));
        return new ListPlacement(string.Empty, definition.Indent + definition.Hanging, 0);
    }

    private string Numbers(ListStyle style, int?[] counts, int level, int display)
    {
        var first = Math.Max(0, level - Math.Max(1, display) + 1);
        var parts = new List<string>();
        var legal = style.Level(level).Legal;
        for (var l = first; l <= level; l++)
        {
            var format = legal ? NumberFormat.Arabic : style.Level(l).Format;
            var value = counts[l] ?? style.Level(l).StartAt;
            parts.Add(_formatters.TryGetValue(format, out var formatter) ? formatter.Render(value) : value.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join('.', parts);
    }
}
