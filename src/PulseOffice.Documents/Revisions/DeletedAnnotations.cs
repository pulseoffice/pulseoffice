using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Revisions;

public interface IDeletedAnnotations
{
    BlockContainer Keep(TextDocument document, EditRecorder recorder, BlockContainer removed, IReadOnlyDictionary<string, string> origins);

    DocumentFragment Revival(TextDocument document, Deletion deletion);

    void Release(TextDocument document, EditRecorder recorder, Deletion deletion);
}

public sealed class DeletedAnnotations(IStoryCatalog stories, IBlockTree tree, IObjectAnchoring anchoring) : IDeletedAnnotations
{
    public BlockContainer Keep(TextDocument document, EditRecorder recorder, BlockContainer removed, IReadOnlyDictionary<string, string> origins)
    {
        var paragraphs = stories.Paragraphs(document).ToList();
        var live = paragraphs.SelectMany(paragraph => paragraph.Marks).Select(mark => mark.Key).ToHashSet(StringComparer.Ordinal);
        var alive = paragraphs.Select(paragraph => paragraph.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var paragraph in tree.Paragraphs(removed))
        {
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => Travels(document, mark) && !live.Contains(mark.Key))] };
        }
        foreach (var (origin, copy) in origins.Where(pair => !alive.Contains(pair.Key)))
            anchoring.Move(document, recorder, origin, copy);
        return removed;
    }

    public DocumentFragment Revival(TextDocument document, Deletion deletion)
    {
        var blocks = deletion.Removed.Clone();
        var held = Held(document, deletion);
        return new DocumentFragment(blocks, document.Styles, [.. held.Notes.Select(note => note.Clone())], [.. held.Comments.Select(comment => comment.Clone())],
            [.. held.Objects.Select(item => item.Clone())]);
    }

    public void Release(TextDocument document, EditRecorder recorder, Deletion deletion)
    {
        var held = Held(document, deletion);
        if (held.Notes.Count > 0)
        {
            recorder.Touch(document.Notes, () => document.Notes.All, document.Notes.Replace);
            foreach (var note in held.Notes) document.Notes.Remove(note.Id);
        }
        if (held.Comments.Count > 0)
        {
            recorder.Touch(document.Comments, () => document.Comments.All, document.Comments.Replace);
            foreach (var comment in held.Comments) document.Comments.Remove(comment.Id);
        }
        if (held.Objects.Count > 0)
        {
            recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
            foreach (var item in held.Objects) document.Objects.Remove(item.Id);
        }
    }

    private HeldAnnotations Held(TextDocument document, Deletion deletion)
    {
        var paragraphs = tree.Paragraphs(deletion.Removed).ToList();
        var marks = paragraphs.SelectMany(paragraph => paragraph.Marks).ToList();
        var notes = marks.OfType<NoteMark>().Select(mark => document.Notes.Find(mark.NoteId)).OfType<Note>().DistinctBy(note => note.Id).ToList();
        var comments = marks.OfType<CommentMark>().Select(mark => mark.CommentId).Distinct(StringComparer.Ordinal)
            .SelectMany(document.Comments.Thread).DistinctBy(comment => comment.Id).ToList();
        var objects = marks.OfType<ObjectMark>().Select(mark => document.Objects.Find(mark.ObjectId)).OfType<AnchoredObject>()
            .Concat(paragraphs.SelectMany(paragraph => document.Objects.AnchoredTo(paragraph.Id))).DistinctBy(item => item.Id).ToList();
        return new HeldAnnotations(notes, comments, objects);
    }

    private bool Travels(TextDocument document, InlineMark mark) => mark switch
    {
        NoteMark note => document.Notes.Find(note.NoteId) is not null,
        ObjectMark item => document.Objects.Find(item.ObjectId) is not null,
        CommentMark comment => document.Comments.Find(comment.CommentId) is not null,
        FieldCodeMark or ContentControlMark => true,
        _ => false,
    };

    private sealed record HeldAnnotations(IReadOnlyList<Note> Notes, IReadOnlyList<Comment> Comments, IReadOnlyList<AnchoredObject> Objects);
}
