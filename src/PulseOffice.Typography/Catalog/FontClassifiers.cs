using PulseOffice.Typography.OpenType;

namespace PulseOffice.Typography.Catalog;

public interface IFontCategoryClassifier
{
    FontCategory Classify(OpenTypeFace face);

    FontCategory FromName(string category);
}

public sealed class FontCategoryClassifier : IFontCategoryClassifier
{
    private static readonly (string Marker, FontCategory Category)[] NameMarkers =
    [
        ("mono", FontCategory.Monospace), ("code", FontCategory.Monospace), ("courier", FontCategory.Monospace),
        ("script", FontCategory.Handwriting), ("hand", FontCategory.Handwriting), ("brush", FontCategory.Handwriting),
        ("symbol", FontCategory.Symbol), ("dingbat", FontCategory.Symbol), ("emoji", FontCategory.Symbol),
        ("sans", FontCategory.SansSerif), ("grotesk", FontCategory.SansSerif), ("gothic", FontCategory.SansSerif),
        ("serif", FontCategory.Serif), ("times", FontCategory.Serif), ("roman", FontCategory.Serif),
    ];

    public FontCategory Classify(OpenTypeFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (face.IsFixedPitch) return FontCategory.Monospace;

        var panose = face.Panose;
        if (panose.Length >= 4)
        {
            switch (panose[0])
            {
                case 2 when panose[3] == 9:
                    return FontCategory.Monospace;
                case 2 when panose[1] is 11 or 12 or 13:
                    return FontCategory.SansSerif;
                case 2 when panose[1] is >= 2 and <= 10 or 14 or 15:
                    return FontCategory.Serif;
                case 3:
                    return FontCategory.Handwriting;
                case 4:
                    return FontCategory.Display;
                case 5:
                    return FontCategory.Symbol;
            }
        }

        var familyClass = (face.FamilyClass >> 8) & 0xFF;
        var byClass = familyClass switch
        {
            >= 1 and <= 5 or 7 => FontCategory.Serif,
            8 => FontCategory.SansSerif,
            9 => FontCategory.Display,
            10 => FontCategory.Handwriting,
            12 => FontCategory.Symbol,
            _ => FontCategory.Unknown,
        };

        return byClass != FontCategory.Unknown ? byClass : ByName(face.Family);
    }

    public FontCategory FromName(string category) => category.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ') switch
    {
        "serif" => FontCategory.Serif,
        "sans serif" => FontCategory.SansSerif,
        "monospace" => FontCategory.Monospace,
        "display" => FontCategory.Display,
        "handwriting" => FontCategory.Handwriting,
        "icons" or "symbols" => FontCategory.Symbol,
        _ => FontCategory.Unknown,
    };

    private FontCategory ByName(string family)
    {
        var lower = family.ToLowerInvariant();
        foreach (var (marker, category) in NameMarkers)
            if (lower.Contains(marker, StringComparison.Ordinal)) return category;
        return FontCategory.Unknown;
    }
}

public interface IFontScriptCoverage
{
    IReadOnlyList<FontScript> FromUnicodeRanges(IReadOnlyList<uint> ranges);

    IReadOnlyList<FontScript> FromSubsets(IEnumerable<string> subsets);
}

public sealed class FontScriptCoverage : IFontScriptCoverage
{
    private static readonly (int Bit, FontScript Script)[] Bits =
    [
        (0, FontScript.Latin), (1, FontScript.Latin), (2, FontScript.LatinExtended), (3, FontScript.LatinExtended),
        (7, FontScript.Greek), (9, FontScript.Cyrillic), (10, FontScript.Armenian), (11, FontScript.Hebrew),
        (13, FontScript.Arabic), (15, FontScript.Devanagari), (16, FontScript.Bengali), (20, FontScript.Tamil),
        (24, FontScript.Thai), (26, FontScript.Georgian), (28, FontScript.Korean), (56, FontScript.Korean),
        (49, FontScript.Japanese), (50, FontScript.Japanese), (59, FontScript.Chinese), (38, FontScript.Math),
        (75, FontScript.Ethiopic), (46, FontScript.Symbols), (47, FontScript.Symbols),
    ];

    private static readonly Dictionary<string, FontScript> Subsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["latin"] = FontScript.Latin,
        ["latin-ext"] = FontScript.LatinExtended,
        ["greek"] = FontScript.Greek,
        ["greek-ext"] = FontScript.Greek,
        ["cyrillic"] = FontScript.Cyrillic,
        ["cyrillic-ext"] = FontScript.Cyrillic,
        ["vietnamese"] = FontScript.Vietnamese,
        ["armenian"] = FontScript.Armenian,
        ["hebrew"] = FontScript.Hebrew,
        ["arabic"] = FontScript.Arabic,
        ["devanagari"] = FontScript.Devanagari,
        ["bengali"] = FontScript.Bengali,
        ["tamil"] = FontScript.Tamil,
        ["thai"] = FontScript.Thai,
        ["georgian"] = FontScript.Georgian,
        ["ethiopic"] = FontScript.Ethiopic,
        ["korean"] = FontScript.Korean,
        ["japanese"] = FontScript.Japanese,
        ["chinese-simplified"] = FontScript.Chinese,
        ["chinese-traditional"] = FontScript.Chinese,
        ["chinese-hongkong"] = FontScript.Chinese,
        ["math"] = FontScript.Math,
        ["symbols"] = FontScript.Symbols,
    };

    public IReadOnlyList<FontScript> FromUnicodeRanges(IReadOnlyList<uint> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var found = new SortedSet<FontScript>();
        foreach (var (bit, script) in Bits)
        {
            var word = bit / 32;
            if (word < ranges.Count && (ranges[word] & (1u << (bit % 32))) != 0) found.Add(script);
        }
        return [.. found];
    }

    public IReadOnlyList<FontScript> FromSubsets(IEnumerable<string> subsets)
    {
        ArgumentNullException.ThrowIfNull(subsets);
        return [.. new SortedSet<FontScript>(subsets.Where(Subsets.ContainsKey).Select(subset => Subsets[subset]))];
    }
}
