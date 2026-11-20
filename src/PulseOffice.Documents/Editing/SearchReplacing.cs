using PulseOffice.Document.Commands;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public interface ISearchReplacing
{
    DocumentRange Replace(IDocumentEditor editor, TextDocument document, UndoStack history, SearchMatch match, string text, SearchFormat? format);
}

public sealed class SearchReplacing(IStoryCatalog stories, ISectionEditing sections) : ISearchReplacing
{
    public SearchReplacing(IStoryCatalog stories) : this(stories, new SectionEditing(stories, new Blocks.BlockTree()))
    {
    }

    public DocumentRange Replace(IDocumentEditor editor, TextDocument document, UndoStack history, SearchMatch match, string text, SearchFormat? format)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 && format is { IsEmpty: false })
        {
            Format(editor, document, history, match.Range, format);
            return match.Range;
        }
        var start = match.Range.Start;
        foreach (var boundary in match.Breaks.Where(item => item.Kind == SearchBreak.Section))
            DissolveSectionAt(document, history, boundary.ParagraphId);
        if (!match.Range.IsCollapsed) editor.Delete(document, history, match.Range);
        foreach (var gone in match.Breaks.Where(item => item.ParagraphId == start.ParagraphId))
            SetBreak(editor, document, history, gone.ParagraphId, gone.Kind, false);
        var end = Insert(editor, document, history, start, text);
        var inserted = new DocumentRange(start, end);
        if (format is { IsEmpty: false } && !inserted.IsCollapsed) Format(editor, document, history, inserted, format);
        return inserted;
    }

    private void DissolveSectionAt(TextDocument document, UndoStack history, string paragraphId)
    {
        var location = stories.Find(document, paragraphId).Location;
        if (location.Index > 0 && location.Container[location.Index - 1] is Blocks.SectionBlock before)
        {
            sections.Unwrap(document, history, before.Id);
            return;
        }
        if (location.Index == 0 && sections.Around(document, paragraphId) is { } around && ReferenceEquals(around.Content, location.Container))
            sections.Unwrap(document, history, around.Id);
    }

    private DocumentPosition Insert(IDocumentEditor editor, TextDocument document, UndoStack history, DocumentPosition at, string text)
    {
        var segment = new System.Text.StringBuilder();
        var position = at;
        var split = false;
        foreach (var character in text)
        {
            if (character is not (SearchMarks.Paragraph or SearchMarks.Page or SearchMarks.Column or SearchMarks.Section))
            {
                segment.Append(character);
                split = false;
                continue;
            }
            position = Flush(editor, document, history, position, segment);
            if (character == SearchMarks.Paragraph || !split && position.Offset > 0)
            {
                position = Split(editor, document, history, position);
                split = character == SearchMarks.Paragraph;
            }
            if (character != SearchMarks.Paragraph)
                SetBreak(editor, document, history, position.ParagraphId, character switch
                {
                    SearchMarks.Column => SearchBreak.Column,
                    SearchMarks.Section => SearchBreak.Section,
                    _ => SearchBreak.Page,
                }, true);
        }
        return Flush(editor, document, history, position, segment);
    }

    private DocumentPosition Flush(IDocumentEditor editor, TextDocument document, UndoStack history, DocumentPosition at, System.Text.StringBuilder segment)
    {
        if (segment.Length == 0) return at;
        var value = segment.ToString();
        segment.Clear();
        editor.InsertText(document, history, at, value);
        return at with { Offset = at.Offset + value.Length };
    }

    private DocumentPosition Split(IDocumentEditor editor, TextDocument document, UndoStack history, DocumentPosition at)
    {
        editor.SplitParagraph(document, history, at);
        var location = stories.Find(document, at.ParagraphId).Location;
        return new DocumentPosition(location.Container[location.Index + 1].Id, 0);
    }

    private void SetBreak(IDocumentEditor editor, TextDocument document, UndoStack history, string paragraphId, SearchBreak kind, bool on)
    {
        var range = DocumentRange.In(paragraphId, 0, 0);
        var pageStyle = kind == SearchBreak.Section && on ? PageStyleBefore(document, paragraphId) : null;
        editor.MapParagraphs(document, history, range, "find-break", paragraph => kind switch
        {
            SearchBreak.Page => paragraph with { PageBreakBefore = on ? true : null },
            SearchBreak.Column => paragraph with { ColumnBreakBefore = on ? true : null },
            _ when on => paragraph with { PageStyleName = pageStyle },
            _ => paragraph with { PageStyleName = null, PageNumberRestart = null },
        });
    }

    private string PageStyleBefore(TextDocument document, string paragraphId)
    {
        var location = stories.Find(document, paragraphId).Location;
        for (var index = location.Index - 1; index >= 0; index--)
            if (location.Container[index] is Blocks.ParagraphBlock { Content.PageStyleName: { } name }) return name;
        return document.InitialPageStyle;
    }

    private void Format(IDocumentEditor editor, TextDocument document, UndoStack history, DocumentRange range, SearchFormat format)
    {
        if (format.ParagraphStyle is { } paragraph && document.Styles.Paragraph.Contains(paragraph)) editor.ApplyParagraphStyle(document, history, range, paragraph);
        if (format.CharacterStyle is { } character && document.Styles.Character.Contains(character)) editor.ApplyCharacterStyle(document, history, range, character);
        if (format.HasCharacterFormat)
            editor.SetCharacter(document, history, range, new CharacterProperties { Bold = format.Bold, Italic = format.Italic, Underline = format.Underline });
    }
}
