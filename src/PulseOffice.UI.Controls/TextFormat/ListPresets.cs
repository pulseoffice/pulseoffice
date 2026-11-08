using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IListPresetCatalog
{
    IReadOnlyList<ListPreset> Presets(ListFormatRequest request);

    IReadOnlyList<ListLevelFormat> Apply(IReadOnlyList<ListLevelFormat> levels, int level, ListPreset preset);

    ListPreset? Matching(IReadOnlyList<ListPreset> presets, ListLevelFormat level, int index);
}

public sealed class ListPresetCatalog(ITextCatalog texts) : IListPresetCatalog
{
    private const string FullOutline = "outline.full";

    private readonly string[] _bullets = ["•", "◦", "▪", "➢", "✓", "–", "★", "→"];

    public IReadOnlyList<ListPreset> Presets(ListFormatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var presets = new List<ListPreset> { new("none", new ListLevelFormat(ListNumbering.None) { Suffix = string.Empty }, texts["textFormat.list.preset.none"]) };
        if (request.Mode == ListSampleMode.Items && request.Numberings.Contains(ListNumbering.Bullet))
            presets.AddRange(_bullets.Select(bullet => new ListPreset($"bullet.{bullet}", new ListLevelFormat(ListNumbering.Bullet) { Bullet = bullet, Suffix = string.Empty }, bullet)));
        var numbers = request.Mode == ListSampleMode.Headings
            ? new List<ListPreset>
            {
                new("outline.plain", new ListLevelFormat(ListNumbering.Arabic) { Suffix = string.Empty }, "1"),
                new("outline.dot", new ListLevelFormat(ListNumbering.Arabic) { Suffix = "." }, "1."),
                new(FullOutline, new ListLevelFormat(ListNumbering.Arabic) { Suffix = string.Empty }, "1.1"),
                new("outline.roman", new ListLevelFormat(ListNumbering.UpperRoman) { Suffix = "." }, "I."),
                new("outline.letter", new ListLevelFormat(ListNumbering.UpperLetter) { Suffix = "." }, "A."),
                new("outline.lower", new ListLevelFormat(ListNumbering.LowerLetter) { Suffix = ")" }, "a)"),
            }
            :
            [
                new("arabic.dot", new ListLevelFormat(ListNumbering.Arabic) { Suffix = "." }, "1."),
                new("arabic.paren", new ListLevelFormat(ListNumbering.Arabic) { Suffix = ")" }, "1)"),
                new("arabic.round", new ListLevelFormat(ListNumbering.Arabic) { Prefix = "(", Suffix = ")" }, "(1)"),
                new("upperRoman", new ListLevelFormat(ListNumbering.UpperRoman) { Suffix = "." }, "I."),
                new("upperLetter", new ListLevelFormat(ListNumbering.UpperLetter) { Suffix = "." }, "A."),
                new("lowerLetter", new ListLevelFormat(ListNumbering.LowerLetter) { Suffix = ")" }, "a)"),
                new("lowerRoman", new ListLevelFormat(ListNumbering.LowerRoman) { Suffix = "." }, "i."),
            ];
        presets.AddRange(numbers.Where(preset => request.Numberings.Contains(preset.Level.Numbering) && (request.Offers(ListFeatures.Affixes) || Plain(preset.Level))));
        return presets;
    }

    public IReadOnlyList<ListLevelFormat> Apply(IReadOnlyList<ListLevelFormat> levels, int level, ListPreset preset)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(preset);
        return [.. levels.Select((existing, index) => index != level ? existing : existing with
        {
            Numbering = preset.Level.Numbering,
            Bullet = preset.Level.Numbering == ListNumbering.Bullet ? preset.Level.Bullet : existing.Bullet,
            Prefix = preset.Level.Prefix,
            Suffix = preset.Level.Suffix,
            DisplayLevels = preset.Key == FullOutline ? level + 1 : 1,
            Picture = null,
        })];
    }

    public ListPreset? Matching(IReadOnlyList<ListPreset> presets, ListLevelFormat level, int index)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(level);
        if (level.Picture is not null) return null;
        return presets.FirstOrDefault(preset =>
            preset.Level.Numbering == level.Numbering
            && (level.Numbering != ListNumbering.Bullet || preset.Level.Bullet == level.Bullet)
            && (level.Numbering is ListNumbering.None or ListNumbering.Bullet
                || (preset.Level.Prefix == level.Prefix && preset.Level.Suffix == level.Suffix
                    && (preset.Key == FullOutline) == (index > 0 && level.DisplayLevels > 1))));
    }

    private bool Plain(ListLevelFormat level) => level.Prefix.Length == 0 && level.Suffix == ".";
}
