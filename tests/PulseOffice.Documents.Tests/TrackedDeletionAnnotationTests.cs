using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TrackedDeletionAnnotationTests
{
    private sealed class Author : IRevisionAuthor
    {
        public RevisionSignature Sign() => new("Anna", DateTimeOffset.UnixEpoch);
    }

    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public TrackedDeletionAnnotationTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().AddSingleton<IRevisionAuthor>(new Author()).BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    private ParagraphBlock WithNote()
    {
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), "abcdefgh");
        _editor.InsertNote(_document, _history, new DocumentPosition(First.Id, 4), NoteKind.Footnote, "nota");
        return First;
    }

    [Fact]
    public void RejectingATrackedDeletionBringsTheFootnoteBack()
    {
        var first = WithNote();
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(first.Id, 2, 7));

        Assert.Single(_document.Notes.All);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var note = Assert.Single(_document.Notes.All);
        Assert.Equal("abcdefgh", First.Text);
        Assert.Equal(note.Id, Assert.Single(First.Marks.OfType<NoteMark>()).NoteId);
    }

    [Fact]
    public void AcceptingATrackedDeletionRemovesTheFootnoteAndUndoBringsItBack()
    {
        var first = WithNote();
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(first.Id, 2, 7));

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Accept);

        Assert.Empty(_document.Notes.All);
        _history.Undo();
        Assert.Single(_document.Notes.All);
    }

    [Fact]
    public void RejectingATrackedDeletionOfAWholeFieldBringsTheFieldBack()
    {
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), "ab2026cd");
        First.Marks =
        [
            new FieldCodeMark(2, "field1", FieldCodePart.Begin, " DATE "),
            new FieldCodeMark(2, "field1", FieldCodePart.Separate, string.Empty),
            new FieldCodeMark(6, "field1", FieldCodePart.End, string.Empty),
        ];
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 1, 7));

        Assert.DoesNotContain(First.Marks, mark => mark is FieldCodeMark);
        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var parts = First.Marks.OfType<FieldCodeMark>().ToList();
        Assert.Equal(3, parts.Count);
        Assert.Single(parts.Select(mark => mark.FieldId).Distinct());
        Assert.Equal(2, parts.Single(mark => mark.Part == FieldCodePart.Begin).Offset);
        Assert.Equal(6, parts.Single(mark => mark.Part == FieldCodePart.End).Offset);
    }

    [Fact]
    public void RejectingATrackedDeletionNeverLeavesABookmarkEndBeforeItsStart()
    {
        _editor.InsertText(_document, _history, new DocumentPosition(First.Id, 0), "abcdefgh");
        First.Marks = [new BookmarkMark(3, "bm", MarkEdge.Start), new BookmarkMark(5, "bm", MarkEdge.End)];
        _document.Changes.IsRecording = true;
        _editor.Delete(_document, _history, DocumentRange.In(First.Id, 2, 7));

        _resolver.ResolveAll(_document, _history, RevisionVerdict.Reject);

        var start = First.Marks.OfType<BookmarkMark>().Single(mark => mark.Edge == MarkEdge.Start);
        var end = First.Marks.OfType<BookmarkMark>().Single(mark => mark.Edge == MarkEdge.End);
        Assert.True(start.Offset <= end.Offset);
    }
}
