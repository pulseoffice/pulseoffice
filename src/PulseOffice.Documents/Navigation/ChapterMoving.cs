using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Navigation;

public interface IChapterMoving
{
    bool IsChapter(TextDocument document, string headingId);

    DocumentEdit Move(TextDocument document, UndoStack history, string headingId, string? beforeHeadingId);

    DocumentEdit MoveUp(TextDocument document, UndoStack history, string headingId);

    DocumentEdit MoveDown(TextDocument document, UndoStack history, string headingId);

    DocumentEdit Shift(TextDocument document, UndoStack history, string headingId, int delta);
}

public sealed class ChapterMoving(IDocumentOutline outline) : IChapterMoving
{
    private const int DeepestLevel = 10;

    public bool IsChapter(TextDocument document, string headingId) => Span(document, headingId) is not null;

    public DocumentEdit Move(TextDocument document, UndoStack history, string headingId, string? beforeHeadingId)
    {
        var recorder = new EditRecorder(document, "move-chapter");
        if (Span(document, headingId) is not { } span || beforeHeadingId == headingId) return recorder.Commit(history);
        var body = document.Body;
        var target = beforeHeadingId is null ? body.Count : body.IndexOf(beforeHeadingId);
        if (target < 0 || target > span.Start && target < span.Start + span.Count) return recorder.Commit(history);
        recorder.Touch(body);
        var moved = body.Blocks.Skip(span.Start).Take(span.Count).ToList();
        body.RemoveRange(span.Start, span.Count);
        if (target > span.Start) target -= span.Count;
        body.InsertRange(target, moved);
        return recorder.Commit(history);
    }

    public DocumentEdit MoveUp(TextDocument document, UndoStack history, string headingId)
    {
        if (Span(document, headingId) is not { } span) return new EditRecorder(document, "move-chapter").Commit(history);
        for (var index = span.Start - 1; index >= 0; index--)
        {
            if (Level(document, document.Body[index]) is not { } level || level > span.Level) continue;
            return level == span.Level ? Move(document, history, headingId, document.Body[index].Id) : new EditRecorder(document, "move-chapter").Commit(history);
        }
        return new EditRecorder(document, "move-chapter").Commit(history);
    }

    public DocumentEdit MoveDown(TextDocument document, UndoStack history, string headingId)
    {
        if (Span(document, headingId) is not { } span) return new EditRecorder(document, "move-chapter").Commit(history);
        var next = span.Start + span.Count;
        if (next >= document.Body.Count || Level(document, document.Body[next]) != span.Level) return new EditRecorder(document, "move-chapter").Commit(history);
        var following = Span(document, document.Body[next].Id)!.Value;
        var after = following.Start + following.Count;
        return Move(document, history, headingId, after < document.Body.Count ? document.Body[after].Id : null);
    }

    public DocumentEdit Shift(TextDocument document, UndoStack history, string headingId, int delta)
    {
        var recorder = new EditRecorder(document, "shift-chapter");
        if (Span(document, headingId) is not { } span) return recorder.Commit(history);
        if (span.Level + delta is < 1 or > DeepestLevel) return recorder.Commit(history);
        foreach (var paragraph in document.Body.Blocks.Skip(span.Start).Take(span.Count).OfType<ParagraphBlock>())
        {
            if (outline.LevelOf(document, paragraph) is not { } level) continue;
            var shifted = Math.Clamp(level + delta, 1, DeepestLevel);
            recorder.Touch(paragraph);
            var content = paragraph.Content;
            paragraph.Content = content.OutlineLevel is not null || !(content.StyleName ?? string.Empty).StartsWith("Heading ", StringComparison.Ordinal)
                ? content with { OutlineLevel = shifted }
                : content with { StyleName = DefaultStyleNames.HeadingLevel(shifted) };
        }
        return recorder.Commit(history);
    }

    private int? Level(TextDocument document, Block block) => block is ParagraphBlock paragraph ? outline.LevelOf(document, paragraph) : null;

    private (int Start, int Count, int Level)? Span(TextDocument document, string headingId)
    {
        var body = document.Body;
        var start = body.IndexOf(headingId);
        if (start < 0 || Level(document, body[start]) is not { } level) return null;
        var end = start + 1;
        while (end < body.Count && (Level(document, body[end]) is not { } other || other > level)) end++;
        return (start, end - start, level);
    }
}
