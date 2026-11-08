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
        var removed = document.Changes.All.OfType<Deletion>().SelectMany(deletion => tree.Paragraphs(deletion.Removed));
        var marks = stories.Paragraphs(document).Concat(removed).SelectMany(paragraph => paragraph.Marks).ToList();
        var comments = marks.OfType<CommentMark>().Select(mark => mark.CommentId).ToHashSet(StringComparer.Ordinal);
        var notes = marks.OfType<NoteMark>().Select(mark => mark.NoteId).ToHashSet(StringComparer.Ordinal);
        var orphanComments = document.Comments.All.Where(comment => !comments.Contains(Root(document, comment).Id)).ToList();
        var orphanNotes = document.Notes.All.Where(note => !notes.Contains(note.Id)).ToList();
        foreach (var orphan in orphanComments) document.Comments.Remove(orphan.Id);
        foreach (var orphan in orphanNotes) document.Notes.Remove(orphan.Id);
        return new UnanchoredRemoval(orphanComments, orphanNotes);
    }

    private Comment Root(TextDocument document, Comment comment)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = comment;
        while (current.ParentId is { } parent && visited.Add(current.Id) && document.Comments.Find(parent) is { } next) current = next;
        return current;
    }
}
