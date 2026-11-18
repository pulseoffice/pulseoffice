using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public enum ListNumbering
{
    None,
    Bullet,
    Arabic,
    LowerLetter,
    UpperLetter,
    LowerRoman,
    UpperRoman,
    DecimalZero,
    Ordinal,
    OrdinalText,
    CardinalText,
    Hebrew,
    ArabicAbjad,
    ArabicAlpha,
    Hiragana,
    Katakana,
    KatakanaIroha,
    IdeographDigital,
    ChineseCounting,
    ThaiLetters,
    RussianLower,
    RussianUpper,
}

[Flags]
public enum ListFeatures
{
    None = 0,
    Affixes = 1,
    Start = 1 << 1,
    DisplayLevels = 1 << 2,
    Indents = 1 << 3,
    Symbol = 1 << 4,
    Picture = 1 << 5,
    Separator = 1 << 6,
    All = (1 << 7) - 1,
}

public enum ListSampleMode
{
    Items,
    Headings,
}

public sealed record ListLevelFormat(ListNumbering Numbering)
{
    public string Bullet { get; init; } = "•";

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = ".";

    public int Start { get; init; } = 1;

    public int DisplayLevels { get; init; } = 1;

    public string Separator { get; init; } = ".";

    public double Indent { get; init; }

    public double Hanging { get; init; } = 18;

    public ListBulletPicture? Picture { get; init; }
}

public sealed record ListPreset(string Key, ListLevelFormat Level, string Caption);

public sealed record ListFormatRequest(
    IReadOnlyList<ListLevelFormat> Levels,
    int Level,
    IReadOnlyList<ListNumbering> Numberings,
    ListFeatures Features,
    ListSampleMode Mode = ListSampleMode.Items)
{
    public string? Language { get; init; }

    public bool Offers(ListFeatures feature) => (Features & feature) == feature;
}

public sealed record ListLevelsAnswer(IReadOnlyList<ListLevelFormat> Levels, int Level);

public interface IListLabeler
{
    string Number(ListNumbering numbering, int value, string? language = null);

    string Label(IReadOnlyList<ListLevelFormat> levels, int level, IReadOnlyList<int> counters, string? language = null);
}

public sealed class ListLabeler(IListNumberFormats numbers, INumberSystems systems) : IListLabeler
{
    public ListLabeler() : this(new ListNumberFormats(), new NumberSystems())
    {
    }

    public string Number(ListNumbering numbering, int value, string? language = null) => numbering switch
    {
        ListNumbering.Arabic => numbers.Number(ListNumberStyle.Arabic, value),
        ListNumbering.LowerLetter => numbers.Number(ListNumberStyle.LowerLetter, value),
        ListNumbering.UpperLetter => numbers.Number(ListNumberStyle.UpperLetter, value),
        ListNumbering.LowerRoman => numbers.Number(ListNumberStyle.LowerRoman, value),
        ListNumbering.UpperRoman => numbers.Number(ListNumberStyle.UpperRoman, value),
        ListNumbering.None or ListNumbering.Bullet => string.Empty,
        _ => systems.Render(Enum.Parse<NumberSystem>(numbering.ToString()), value, language),
    };

    public string Label(IReadOnlyList<ListLevelFormat> levels, int level, IReadOnlyList<int> counters, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(counters);
        var format = levels[level];
        if (format.Numbering == ListNumbering.None) return string.Empty;
        if (format.Numbering == ListNumbering.Bullet) return format.Bullet;
        var first = Math.Max(0, level - Math.Max(1, format.DisplayLevels) + 1);
        var parts = Enumerable.Range(first, level - first + 1)
            .Where(index => levels[index].Numbering is not (ListNumbering.None or ListNumbering.Bullet))
            .Select(index => Number(levels[index].Numbering, counters[index], language));
        return format.Prefix + string.Join(format.Separator, parts) + format.Suffix;
    }
}
