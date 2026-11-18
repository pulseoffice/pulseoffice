using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public sealed record LinkedCharacterStyle(string Name, CharacterStyle? Created);

public interface ILinkedStyleSelection
{
    LinkedCharacterStyle? CharacterStyle(TextDocument document, DocumentRange range, string paragraphStyle);
}

public sealed class LinkedStyleSelection(IStoryCatalog stories) : ILinkedStyleSelection
{
    public const string PartnerSuffix = " Char";

    public LinkedCharacterStyle? CharacterStyle(TextDocument document, DocumentRange range, string paragraphStyle)
    {
        if (range.Start.ParagraphId != range.End.ParagraphId || range.Start.Offset == range.End.Offset) return null;
        if (document.Styles.Paragraph.Find(paragraphStyle) is not { } style) return null;
        var length = stories.Find(document, range.Start.ParagraphId).Paragraph.Length;
        var start = Math.Min(range.Start.Offset, range.End.Offset);
        var end = Math.Max(range.Start.Offset, range.End.Offset);
        if (start <= 0 && end >= length) return null;
        if (style.LinkedStyleName is { } linked && document.Styles.Character.Contains(linked)) return new LinkedCharacterStyle(linked, null);
        if (paragraphStyle == DefaultStyleNames.Standard) return null;
        var name = FreeName(document, paragraphStyle + PartnerSuffix);
        return new LinkedCharacterStyle(name, new CharacterStyle(name)
        {
            Character = style.Character,
            LinkedStyleName = paragraphStyle,
            DisplayName = style.DisplayName is { } display ? display + PartnerSuffix : null,
        });
    }

    private static string FreeName(TextDocument document, string wanted)
    {
        var name = wanted;
        for (var index = 2; document.Styles.Character.Contains(name) || document.Styles.Paragraph.Contains(name); index++) name = $"{wanted} {index}";
        return name;
    }
}
