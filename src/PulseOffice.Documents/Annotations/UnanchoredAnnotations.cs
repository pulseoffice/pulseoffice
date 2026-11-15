using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Documents.Annotations;

public sealed record UnanchoredRemoval(IReadOnlyList<Comment> Comments, IReadOnlyList<Note> Notes)
{
    public bool Any => Comments.Count > 0 || Notes.Count > 0;
}

public interface IUnanchoredAnnotations
{
    UnanchoredRemoval Remove(TextDocument document);
}

public sealed class UnanchoredAnnotations(IStoryCatalog stories, IBlockTree tree) : IUnanchoredAnnotations
{
    public UnanchoredRemoval Remove(TextDocument document)
    {
        var removed = document.Changes.All.OfType<Deletion>().SelectMany(deletion => tree.Paragraphs(deletion.Removed)).ToList();
        var paragraphs = stories.Paragraphs(document).ToList();
        AnchorEndsWithoutStart(paragraphs, paragraphs.Concat(removed).SelectMany(paragraph => paragraph.Marks).OfType<CommentMark>().ToList());
        var marks = paragraphs.Concat(removed).SelectMany(paragraph => paragraph.Marks).ToList();
        var comments = marks.OfType<CommentMark>().Select(mark => mark.CommentId).ToHashSet(StringComparer.Ordinal);
        var notes = marks.OfType<NoteMark>().Select(mark => mark.NoteId).ToHashSet(StringComparer.Ordinal);
        var orphanComments = document.Comments.All.Where(comment => !comments.Contains(Root(document, comment).Id)).ToList();
        var orphanNotes = document.Notes.All.Where(note => !notes.Contains(note.Id)).ToList();
        foreach (var orphan in orphanComments) document.Comments.Remove(orphan.Id);
        foreach (var orphan in orphanNotes) document.Notes.Remove(orphan.Id);
        return new UnanchoredRemoval(orphanComments, orphanNotes);
    }

    private void AnchorEndsWithoutStart(IReadOnlyList<ParagraphBlock> paragraphs, IReadOnlyList<CommentMark> marks)
    {
        var started = marks.Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.CommentId).ToHashSet(StringComparer.Ordinal);
        var endedOnly = marks.Where(mark => mark.Edge == MarkEdge.End && !started.Contains(mark.CommentId)).Select(mark => mark.CommentId).ToHashSet(StringComparer.Ordinal);
        if (endedOnly.Count == 0) return;
        foreach (var paragraph in paragraphs.Where(paragraph => paragraph.Marks.Any(mark => mark is CommentMark { Edge: MarkEdge.End } end && endedOnly.Contains(end.CommentId))))
            paragraph.Marks = [.. paragraph.Marks.Select(mark => mark is CommentMark { Edge: MarkEdge.End } end && endedOnly.Contains(end.CommentId) ? end with { Edge = MarkEdge.Point } : mark)];
    }

    private Comment Root(TextDocument document, Comment comment)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = comment;
        while (current.ParentId is { } parent && visited.Add(current.Id) && document.Comments.Find(parent) is { } next) current = next;
        return current;
    }
}
