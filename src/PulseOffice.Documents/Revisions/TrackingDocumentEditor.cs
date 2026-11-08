using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Revisions;

public sealed class TrackingDocumentEditor(IDirectDocumentEditor inner, ITrackedEditing tracked, IRevisionAuthor author, IStoryCatalog stories) : IDocumentEditor
{
    public DocumentEdit InsertText(TextDocument document, UndoStack history, DocumentPosition at, string text) =>
        document.Changes.IsRecording ? tracked.InsertText(document, history, at, text, author.Sign()) : inner.InsertText(document, history, at, text);

    public DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range) =>
        document.Changes.IsRecording ? tracked.Delete(document, history, range, author.Sign()) : inner.Delete(document, history, range);

    public DocumentEdit SplitParagraph(TextDocument document, UndoStack history, DocumentPosition at) =>
        document.Changes.IsRecording ? tracked.SplitParagraph(document, history, at, author.Sign()) : inner.SplitParagraph(document, history, at);

    public DocumentEdit MergeWithNext(TextDocument document, UndoStack history, string paragraphId)
    {
        if (!document.Changes.IsRecording) return inner.MergeWithNext(document, history, paragraphId);
        var found = stories.Find(document, paragraphId);
        var container = found.Location.Container;
        if (found.Location.Index + 1 >= container.Count || container[found.Location.Index + 1] is not Blocks.ParagraphBlock next)
            return inner.MergeWithNext(document, history, paragraphId);
        return tracked.Delete(document, history, new DocumentRange(new DocumentPosition(paragraphId, found.Paragraph.Length), new DocumentPosition(next.Id, 0)), author.Sign());
    }

    public DocumentEdit ApplyParagraphStyle(TextDocument document, UndoStack history, DocumentRange range, string styleName) =>
        Paragraphs(document, history, range, () => inner.ApplyParagraphStyle(document, history, range, styleName));

    public DocumentEdit ApplyCharacterStyle(TextDocument document, UndoStack history, DocumentRange range, string? styleName) =>
        Characters(document, history, range, () => inner.ApplyCharacterStyle(document, history, range, styleName));

    public DocumentEdit SetCharacter(TextDocument document, UndoStack history, DocumentRange range, CharacterProperties properties) =>
        Characters(document, history, range, () => inner.SetCharacter(document, history, range, properties));

    public DocumentEdit SetParagraph(TextDocument document, UndoStack history, DocumentRange range, ParagraphProperties properties) =>
        Paragraphs(document, history, range, () => inner.SetParagraph(document, history, range, properties));

    public DocumentEdit InsertTable(TextDocument document, UndoStack history, DocumentPosition at, int rows, int columns) =>
        inner.InsertTable(document, history, at, rows, columns);

    public DocumentEdit InsertNote(TextDocument document, UndoStack history, DocumentPosition at, NoteKind kind, string text) =>
        inner.InsertNote(document, history, at, kind, text);

    public DocumentEdit AddComment(TextDocument document, UndoStack history, DocumentRange range, string author, DateTimeOffset date, string text, string? parentId = null) =>
        inner.AddComment(document, history, range, author, date, text, parentId);

    public DocumentEdit AddBookmark(TextDocument document, UndoStack history, DocumentRange range, string name) =>
        inner.AddBookmark(document, history, range, name);

    public DocumentEdit InsertObject(TextDocument document, UndoStack history, AnchoredObject item, DocumentPosition at) =>
        inner.InsertObject(document, history, item, at);

    public DocumentEdit InsertField(TextDocument document, UndoStack history, DocumentPosition at, TextFieldKind field, string display, string? argument = null)
    {
        if (!document.Changes.IsRecording) return inner.InsertField(document, history, at, field, display, argument);
        using var transaction = history.BeginTransaction("insert-field");
        var edit = inner.InsertField(document, history, at, field, display, argument);
        var inserted = edit.Changes.OfType<TextInserted>().FirstOrDefault();
        if (inserted is null) return edit;
        return tracked.MarkInserted(document, history, edit, new DocumentPosition(inserted.ParagraphId, inserted.Offset),
            new DocumentPosition(inserted.ParagraphId, inserted.Offset + inserted.Length), author.Sign());
    }

    public DocumentEdit SetPageStyle(TextDocument document, UndoStack history, string paragraphId, string? pageStyleName, int? pageNumberRestart = null) =>
        Paragraphs(document, history, DocumentRange.At(new DocumentPosition(paragraphId, 0)), () => inner.SetPageStyle(document, history, paragraphId, pageStyleName, pageNumberRestart));

    public DocumentEdit SetList(TextDocument document, UndoStack history, DocumentRange range, string? listStyleName, int level) =>
        Paragraphs(document, history, range, () => inner.SetList(document, history, range, listStyleName, level));

    public DocumentEdit MapRuns(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextRun, TextRun> map) =>
        Characters(document, history, range, () => inner.MapRuns(document, history, range, description, map));

    public DocumentEdit TransformText(TextDocument document, UndoStack history, DocumentRange range, string description, Func<string, string> transform) =>
        document.Changes.IsRecording ? tracked.ReplaceText(document, history, range, description, transform, author.Sign()) : inner.TransformText(document, history, range, description, transform);

    public DocumentEdit MapParagraphs(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextParagraph, TextParagraph> map) =>
        Paragraphs(document, history, range, () => inner.MapParagraphs(document, history, range, description, map));

    public DocumentEdit ClearDirectFormatting(TextDocument document, UndoStack history, DocumentRange range) =>
        Characters(document, history, range, () => inner.ClearDirectFormatting(document, history, range));

    public DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, Clipboard.DocumentFragment fragment) =>
        document.Changes.IsRecording ? tracked.Paste(document, history, at, fragment, author.Sign()) : inner.Paste(document, history, at, fragment);

    public DocumentEdit RemoveObject(TextDocument document, UndoStack history, string objectId) =>
        inner.RemoveObject(document, history, objectId);

    public DocumentEdit ChangeObject(TextDocument document, UndoStack history, string objectId, Action<AnchoredObject> change, DocumentPosition? markAt = null) =>
        inner.ChangeObject(document, history, objectId, change, markAt);

    private DocumentEdit Characters(TextDocument document, UndoStack history, DocumentRange range, Func<DocumentEdit> apply) =>
        document.Changes.IsRecording ? tracked.Characters(document, history, range, author.Sign(), apply) : apply();

    private DocumentEdit Paragraphs(TextDocument document, UndoStack history, DocumentRange range, Func<DocumentEdit> apply) =>
        document.Changes.IsRecording ? tracked.Paragraphs(document, history, range, author.Sign(), apply) : apply();
}
