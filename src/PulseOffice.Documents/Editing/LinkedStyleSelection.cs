namespace PulseOffice.Documents.Editing;

public interface ILinkedStyleSelection
{
    string? CharacterStyle(TextDocument document, DocumentRange range, string paragraphStyle);
}

public sealed class LinkedStyleSelection(IStoryCatalog stories) : ILinkedStyleSelection
{
    public string? CharacterStyle(TextDocument document, DocumentRange range, string paragraphStyle)
    {
        if (range.Start.ParagraphId != range.End.ParagraphId || range.Start.Offset == range.End.Offset) return null;
        if (document.Styles.Paragraph.Find(paragraphStyle)?.LinkedStyleName is not { } linked || !document.Styles.Character.Contains(linked)) return null;
        var length = stories.Find(document, range.Start.ParagraphId).Paragraph.Length;
        var start = Math.Min(range.Start.Offset, range.End.Offset);
        var end = Math.Max(range.Start.Offset, range.End.Offset);
        return start <= 0 && end >= length ? null : linked;
    }
}
