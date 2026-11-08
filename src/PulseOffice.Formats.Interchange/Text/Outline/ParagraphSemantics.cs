using System.Globalization;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Outline;

public enum ListKind
{
    None,
    Bullet,
    Numbered,
}

public sealed record ListPlacement(ListKind Kind, int Level, NumberFormat Format, string? StyleName)
{
    public static readonly ListPlacement None = new(ListKind.None, 0, NumberFormat.None, null);
}

public interface IParagraphSemantics
{
    int HeadingLevel(StyleSheet sheet, TextParagraph paragraph);

    ListPlacement List(StyleSheet sheet, TextParagraph paragraph);

    string HeadingStyle(int level);
}

public sealed class ParagraphSemantics(IStyleResolver resolver, IStyleChain chain) : IParagraphSemantics
{
    private const string HeadingPrefix = "Heading ";

    public int HeadingLevel(StyleSheet sheet, TextParagraph paragraph)
    {
        foreach (var style in chain.RootFirst(sheet.Paragraph, paragraph.StyleName).Reverse())
        {
            if (style.Name.StartsWith(HeadingPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(style.Name.AsSpan(HeadingPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var named))
                return Math.Clamp(named, 1, 10);
        }

        var outline = paragraph.OutlineLevel ?? resolver.ResolveParagraphStyle(sheet, paragraph.StyleName).OutlineLevel;
        return outline is > 0 ? Math.Min(outline.Value, 10) : 0;
    }

    public ListPlacement List(StyleSheet sheet, TextParagraph paragraph)
    {
        var name = paragraph.ListStyleName ?? resolver.ResolveParagraphStyle(sheet, paragraph.StyleName).ListStyleName;
        if (name is null && !paragraph.IsListItem) return ListPlacement.None;
        var level = Math.Clamp(paragraph.BulletLevel, 0, ListStyle.LevelCount - 1);
        var format = sheet.List.Find(name)?.Level(level).Format
                     ?? (paragraph.Numbered ? NumberFormat.Arabic : NumberFormat.Bullet);
        if (paragraph.Numbered && format is NumberFormat.Bullet or NumberFormat.None) format = NumberFormat.Arabic;
        if (paragraph.Bullet && !paragraph.Numbered) format = NumberFormat.Bullet;
        var kind = format switch
        {
            NumberFormat.None => ListKind.None,
            NumberFormat.Bullet => ListKind.Bullet,
            _ => ListKind.Numbered,
        };
        return kind == ListKind.None ? ListPlacement.None : new ListPlacement(kind, level, format, name);
    }

    public string HeadingStyle(int level) => DefaultStyleNames.HeadingLevel(Math.Clamp(level, 1, 10));
}
