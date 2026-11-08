using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record DirectCursorTarget(string AfterParagraphId, int EmptyLines, double Indent);

public interface IDirectCursor
{
    DocumentPosition Place(TextDocument document, UndoStack history, DirectCursorTarget target);
}

public sealed class DirectCursor(IStoryCatalog stories) : IDirectCursor
{
    public DocumentPosition Place(TextDocument document, UndoStack history, DirectCursorTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var found = stories.Find(document, target.AfterParagraphId);
        var container = found.Location.Container;
        var recorder = new EditRecorder(document, "direct-cursor");
        recorder.Touch(container);
        var count = Math.Max(1, target.EmptyLines);
        ParagraphBlock last = found.Paragraph;
        for (var index = 0; index < count; index++)
        {
            last = new ParagraphBlock(document.NextId("p"), string.Empty, found.Paragraph.StyleName);
            container.Insert(found.Location.Index + 1 + index, last);
        }
        last.Content = last.Content with { MarginLeft = Math.Max(0, Math.Round(target.Indent, 2)) };
        recorder.Commit(history);
        return new DocumentPosition(last.Id, 0);
    }
}
