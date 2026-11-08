namespace PulseOffice.Presentations;

public interface IAnimationSubtypeTable
{
    string? FromOoxml(AnimationCategory category, int presetId, int subtype);

    int ToOoxml(AnimationCategory category, int presetId, string? subtype);
}

public sealed class AnimationSubtypeTable : IAnimationSubtypeTable
{
    private sealed record Row(int Ooxml, string Odf, int? PresetId = null, AnimationCategory? Category = null);

    private readonly IReadOnlyList<Row> _rows =
    [
        new(1, "to-top", 2, AnimationCategory.Exit),
        new(2, "to-right", 2, AnimationCategory.Exit),
        new(3, "to-top-right", 2, AnimationCategory.Exit),
        new(4, "to-bottom", 2, AnimationCategory.Exit),
        new(6, "to-bottom-right", 2, AnimationCategory.Exit),
        new(8, "to-left", 2, AnimationCategory.Exit),
        new(9, "to-top-left", 2, AnimationCategory.Exit),
        new(12, "to-bottom-left", 2, AnimationCategory.Exit),
        new(1, "to-top", 7, AnimationCategory.Exit),
        new(2, "to-right", 7, AnimationCategory.Exit),
        new(4, "to-bottom", 7, AnimationCategory.Exit),
        new(8, "to-left", 7, AnimationCategory.Exit),
        new(5, "downward", 5),
        new(10, "across", 5),
        new(10, "across", 17),
        new(5, "downward", 17),
        new(3, "right-to-top", 18),
        new(6, "right-to-bottom", 18),
        new(9, "left-to-top", 18),
        new(12, "left-to-bottom", 18),
        new(1, "from-top"),
        new(2, "from-right"),
        new(3, "from-top-right"),
        new(4, "from-bottom"),
        new(5, "horizontal"),
        new(6, "from-bottom-right"),
        new(8, "from-left"),
        new(9, "from-top-left"),
        new(10, "vertical"),
        new(12, "from-bottom-left"),
        new(16, "in"),
        new(21, "vertical-in"),
        new(26, "horizontal-in"),
        new(32, "out"),
        new(36, "out-from-screen-center"),
        new(37, "vertical-out"),
        new(42, "horizontal-out"),
        new(272, "in-slightly"),
        new(288, "out-slightly"),
        new(528, "in-from-screen-center"),
    ];

    public string? FromOoxml(AnimationCategory category, int presetId, int subtype)
    {
        if (subtype == 0) return null;
        if (category is AnimationCategory.Entrance or AnimationCategory.Exit && presetId != 21
            && Applicable(category, presetId).FirstOrDefault(row => row.Ooxml == subtype) is { } row)
            return row.Odf;
        return subtype.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public int ToOoxml(AnimationCategory category, int presetId, string? subtype)
    {
        if (string.IsNullOrEmpty(subtype)) return 0;
        if (int.TryParse(subtype, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)) return number;
        return Applicable(category, presetId).FirstOrDefault(row => row.Odf == subtype)?.Ooxml ?? 0;
    }

    private IEnumerable<Row> Applicable(AnimationCategory category, int presetId) =>
        _rows.Where(row => row.PresetId == presetId && (row.Category is null || row.Category == category))
            .Concat(_rows.Where(row => row.PresetId is null));
}
