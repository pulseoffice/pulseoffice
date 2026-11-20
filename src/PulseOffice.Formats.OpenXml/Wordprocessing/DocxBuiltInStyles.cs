namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxBuiltInStyle(int UiPriority, bool PrimaryFormat);

public sealed class DocxBuiltInStyles
{
    private const int Unprioritised = 99;

    private readonly Dictionary<string, DocxBuiltInStyle> _styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = new(0, true),
        ["Title"] = new(10, true),
        ["Subtitle"] = new(11, true),
        ["No Spacing"] = new(1, true),
        ["Quote"] = new(29, true),
        ["Intense Quote"] = new(30, true),
        ["List Paragraph"] = new(34, true),
        ["caption"] = new(35, true),
        ["TOC Heading"] = new(39, true),
        ["Bibliography"] = new(37, false),
        ["Default Paragraph Font"] = new(1, false),
        ["Strong"] = new(22, true),
        ["Emphasis"] = new(20, true),
        ["Subtle Emphasis"] = new(19, true),
        ["Intense Emphasis"] = new(21, true),
        ["Subtle Reference"] = new(31, true),
        ["Intense Reference"] = new(32, true),
        ["Book Title"] = new(33, true),
        ["Table Grid"] = new(39, false),
        ["Table Normal"] = new(Unprioritised, false),
        ["header"] = new(Unprioritised, false),
        ["footer"] = new(Unprioritised, false),
        ["Hyperlink"] = new(Unprioritised, false),
        ["FollowedHyperlink"] = new(Unprioritised, false),
        ["footnote text"] = new(Unprioritised, false),
        ["footnote reference"] = new(Unprioritised, false),
        ["endnote text"] = new(Unprioritised, false),
        ["endnote reference"] = new(Unprioritised, false),
        ["annotation text"] = new(Unprioritised, false),
        ["annotation reference"] = new(Unprioritised, false),
        ["Balloon Text"] = new(Unprioritised, false),
        ["Placeholder Text"] = new(Unprioritised, false),
        ["List Bullet"] = new(Unprioritised, false),
        ["List Number"] = new(Unprioritised, false),
        ["Body Text"] = new(Unprioritised, false),
        ["Plain Text"] = new(Unprioritised, false),
        ["table of figures"] = new(Unprioritised, false),
    };

    public DocxBuiltInStyle? Find(string wordName)
    {
        if (_styles.TryGetValue(wordName, out var style)) return style;
        if (Level(wordName, "heading ") is >= 1 and <= 9) return new DocxBuiltInStyle(9, true);
        if (Level(wordName, "toc ") is >= 1 and <= 9) return new DocxBuiltInStyle(39, false);
        return null;
    }

    private int? Level(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length == prefix.Length + 1 && char.IsAsciiDigit(name[^1])
            ? name[^1] - '0'
            : null;
}
