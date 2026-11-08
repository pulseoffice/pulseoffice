using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Documents.Commenting;

public sealed record CommentAnchor(Comment Comment, DocumentPosition Start, DocumentPosition End);

public interface ICommentAnchors
{
    IReadOnlyList<CommentAnchor> All(TextDocument document);

    CommentAnchor? Find(TextDocument document, string commentId);

    CommentAnchor? At(TextDocument document, DocumentPosition position);
}

public interface ICommentEditing
{
    DocumentEdit Reply(TextDocument document, UndoStack history, string parentId, string author, DateTimeOffset date, string text);

    DocumentEdit Edit(TextDocument document, UndoStack history, string commentId, string text);

    DocumentEdit Resolve(TextDocument document, UndoStack history, string commentId, bool resolved);

    DocumentEdit Delete(TextDocument document, UndoStack history, IEnumerable<string> commentIds);

    DocumentEdit Promote(TextDocument document, UndoStack history, string replyId, DocumentPosition at);
}

public sealed class CommentAnchors(IStoryCatalog stories) : ICommentAnchors
{
    public IReadOnlyList<CommentAnchor> All(TextDocument document)
    {
        if (document.Comments.All.Count == 0) return [];
        var open = new Dictionary<string, DocumentPosition>(StringComparer.Ordinal);
        var found = new List<CommentAnchor>();
        foreach (var paragraph in stories.Paragraphs(document))
        {
            foreach (var mark in paragraph.Marks.OfType<CommentMark>().OrderBy(mark => mark.Offset))
            {
                if (document.Comments.Find(mark.CommentId) is not { } comment) continue;
                var at = new DocumentPosition(paragraph.Id, mark.Offset);
                switch (mark.Edge)
                {
                    case MarkEdge.Start:
                        open[mark.CommentId] = at;
                        found.Add(new CommentAnchor(comment, at, at));
                        break;
                    case MarkEdge.End when open.Remove(mark.CommentId, out var start):
                        var index = found.FindIndex(anchor => anchor.Comment.Id == mark.CommentId);
                        if (index >= 0) found[index] = new CommentAnchor(comment, start, at);
                        break;
                    case MarkEdge.End:
                        break;
                    default:
                        found.Add(new CommentAnchor(comment, at, at));
                        break;
                }
            }
        }
        return found;
    }

    public CommentAnchor? Find(TextDocument document, string commentId)
    {
        var root = Root(document, commentId);
        return All(document).FirstOrDefault(anchor => anchor.Comment.Id == root);
    }

    public CommentAnchor? At(TextDocument document, DocumentPosition position) =>
        All(document).LastOrDefault(anchor => anchor.Start.ParagraphId == position.ParagraphId && anchor.End.ParagraphId == position.ParagraphId
            && anchor.Start.Offset <= position.Offset && anchor.End.Offset >= position.Offset)
        ?? All(document).LastOrDefault(anchor => anchor.Start.ParagraphId == position.ParagraphId && anchor.Start.Offset <= position.Offset && anchor.End.ParagraphId != position.ParagraphId)
        ?? All(document).LastOrDefault(anchor => anchor.End.ParagraphId == position.ParagraphId && anchor.End.Offset >= position.Offset && anchor.Start.ParagraphId != position.ParagraphId);

    private string Root(TextDocument document, string commentId)
    {
        var current = document.Comments.Find(commentId);
        var guard = 0;
        while (current?.ParentId is { } parent && document.Comments.Find(parent) is { } above && guard++ < 1000) current = above;
        return current?.Id ?? commentId;
    }
}

public sealed class CommentEditing(IStoryCatalog stories, IParagraphText text) : ICommentEditing
{
    public DocumentEdit Promote(TextDocument document, UndoStack history, string replyId, DocumentPosition at)
    {
        var recorder = Recorder(document, "promote-comment");
        if (document.Comments.Find(replyId) is not { ParentId: not null } reply) return recorder.Commit(history);
        var copy = reply.Clone();
        copy.ParentId = null;
        Swap(document, copy);
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        recorder.Touch(paragraph);
        paragraph.State = text.AddMark(paragraph.State, new CommentMark(at.Offset, replyId, MarkEdge.Point));
        return recorder.Commit(history);
    }

    private const string ParagraphPrefix = "p";

    public DocumentEdit Reply(TextDocument document, UndoStack history, string parentId, string author, DateTimeOffset date, string text)
    {
        var recorder = Recorder(document, "reply-comment");
        if (document.Comments.Find(parentId) is null) return recorder.Commit(history);
        document.Comments.Add(new Comment(document.NextId("c"), author, date, Content(document, text)) { ParentId = parentId });
        return recorder.Commit(history);
    }

    public DocumentEdit Edit(TextDocument document, UndoStack history, string commentId, string text)
    {
        var recorder = Recorder(document, "edit-comment");
        if (document.Comments.Find(commentId) is not { } comment) return recorder.Commit(history);
        Swap(document, new Comment(comment.Id, comment.Author, comment.Date, Content(document, text))
        {
            Initials = comment.Initials,
            ParentId = comment.ParentId,
            Resolved = comment.Resolved,
        });
        return recorder.Commit(history);
    }

    public DocumentEdit Resolve(TextDocument document, UndoStack history, string commentId, bool resolved)
    {
        var recorder = Recorder(document, "resolve-comment");
        foreach (var comment in document.Comments.Thread(commentId).ToList())
        {
            var copy = comment.Clone();
            copy.Resolved = resolved;
            Swap(document, copy);
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Delete(TextDocument document, UndoStack history, IEnumerable<string> commentIds)
    {
        var recorder = Recorder(document, "delete-comment");
        var gone = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in commentIds)
        {
            foreach (var comment in document.Comments.Thread(id)) gone.Add(comment.Id);
        }
        if (gone.Count == 0) return recorder.Commit(history);
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is CommentMark found && gone.Contains(found.CommentId))).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => mark is not CommentMark found || !gone.Contains(found.CommentId))] };
        }
        document.Comments.Replace(document.Comments.All.Where(comment => !gone.Contains(comment.Id)));
        return recorder.Commit(history);
    }

    private EditRecorder Recorder(TextDocument document, string description) =>
        new EditRecorder(document, description).Touch(document.Comments, () => document.Comments.All, document.Comments.Replace);

    private void Swap(TextDocument document, Comment replacement) =>
        document.Comments.Replace(document.Comments.All.Select(comment => comment.Id == replacement.Id ? replacement : comment));

    private BlockContainer Content(TextDocument document, string text) =>
        new(text.Replace("\r\n", "\n").Split('\n').Select(line => (Block)new ParagraphBlock(document.NextId(ParagraphPrefix), line)));
}
