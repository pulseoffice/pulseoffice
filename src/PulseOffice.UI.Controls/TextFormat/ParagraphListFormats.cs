using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IParagraphListFormats
{
    IReadOnlyList<ListNumbering> Numberings { get; }

    ListLevelFormat List(TextParagraph paragraph);

    TextParagraph Apply(TextParagraph paragraph, ListLevelFormat level, int bulletLevel);

    ListFormatRequest Request(TextParagraph first);
}

public sealed class ParagraphListFormats(IListNumberFormats numbers) : IParagraphListFormats
{
    public const int LevelCount = 9;

    private readonly IReadOnlyList<(ListNumbering Numbering, ListNumberStyle Style)> _styles =
    [
        (ListNumbering.Arabic, ListNumberStyle.Arabic),
        (ListNumbering.LowerLetter, ListNumberStyle.LowerLetter),
        (ListNumbering.UpperLetter, ListNumberStyle.UpperLetter),
        (ListNumbering.LowerRoman, ListNumberStyle.LowerRoman),
        (ListNumbering.UpperRoman, ListNumberStyle.UpperRoman),
    ];

    public ParagraphListFormats() : this(new ListNumberFormats())
    {
    }

    public IReadOnlyList<ListNumbering> Numberings { get; } =
        [ListNumbering.None, ListNumbering.Bullet, ListNumbering.Arabic, ListNumbering.LowerLetter, ListNumbering.UpperLetter, ListNumbering.LowerRoman, ListNumbering.UpperRoman];

    public ListLevelFormat List(TextParagraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        return paragraph.Numbered
            ? Numbered(paragraph.NumberFormat ?? new ListNumberFormat())
            : paragraph.Bullet
                ? new ListLevelFormat(ListNumbering.Bullet) { Bullet = paragraph.BulletSymbol?.Character ?? "•", Suffix = string.Empty, Picture = paragraph.BulletSymbol?.Picture }
                : new ListLevelFormat(ListNumbering.None) { Suffix = string.Empty };
    }

    public TextParagraph Apply(TextParagraph paragraph, ListLevelFormat level, int bulletLevel)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(level);
        var numbered = _styles.Any(pair => pair.Numbering == level.Numbering);
        var changed = paragraph with
        {
            Bullet = level.Numbering == ListNumbering.Bullet,
            Numbered = numbered,
            NumberFormat = numbered ? Format(paragraph.Numbered ? paragraph.NumberFormat : null, level) : null,
            BulletLevel = Math.Clamp(bulletLevel, 0, LevelCount - 1),
        };
        if (level.Numbering != ListNumbering.Bullet) return changed;
        var symbol = paragraph.BulletSymbol;
        if (Equals(symbol?.Picture, level.Picture) && (symbol?.Character == level.Bullet || (symbol is null && level.Bullet == "•"))) return changed;
        return changed with { BulletSymbol = (symbol ?? new BulletSymbol(level.Bullet)) with { Character = level.Bullet, Picture = level.Picture } };
    }

    public ListFormatRequest Request(TextParagraph first)
    {
        ArgumentNullException.ThrowIfNull(first);
        return new ListFormatRequest(
            [.. Enumerable.Repeat(List(first), LevelCount)],
            Math.Clamp(first.BulletLevel, 0, LevelCount - 1),
            Numberings,
            ListFeatures.Symbol | ListFeatures.Picture | ListFeatures.Affixes | ListFeatures.Start);
    }

    private ListLevelFormat Numbered(ListNumberFormat format) =>
        new(_styles.FirstOrDefault(pair => pair.Style == format.Style, _styles[0]).Numbering)
        {
            Prefix = format.Prefix,
            Suffix = format.Suffix,
            Start = format.Start,
        };

    private ListNumberFormat? Format(ListNumberFormat? before, ListLevelFormat level)
    {
        var chosen = new ListNumberFormat
        {
            Style = _styles.First(pair => pair.Numbering == level.Numbering).Style,
            Prefix = level.Prefix,
            Suffix = level.Suffix,
            Start = level.Start,
        };
        var kept = before is not null && before.Style == chosen.Style && before.Prefix == chosen.Prefix && before.Suffix == chosen.Suffix
            ? chosen with { DrawingScheme = before.DrawingScheme, OdfFormat = before.OdfFormat }
            : chosen;
        return numbers.Normalized(kept);
    }
}
