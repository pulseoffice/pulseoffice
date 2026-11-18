using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Revisions;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round223TrackedNoteRemovalTests
{
    private readonly IDocumentEditor _editor;
    private readonly IRevisionResolver _resolver;
    private readonly TextDocument _document;
    private readonly UndoStack _history = new();

    public Round223TrackedNoteRemovalTests()
    {
        var services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();
        _editor = services.GetRequiredService<IDocumentEditor>();
        _resolver = services.GetRequiredService<IRevisionResolver>();
        _document = services.GetRequiredService<ITextDocumentFactory>().Create();
        _document.Body.Replace([new ParagraphBlock("p1", "word") { Marks = [new NoteMark(4, "n1")] }]);
        _document.Notes.Add(new Note("n1", NoteKind.Footnote, new BlockContainer([new ParagraphBlock("np", "note text")])));
        _document.Changes.IsRecording = true;
    }

    private ParagraphBlock First => (ParagraphBlock)_document.Body[0];

    [Fact]
    public void Removing_a_note_while_recording_records_a_deletion_that_holds_it()
    {
        _editor.RemoveNote(_document, _history, "n1");

        var deletion = Assert.Single(_document.Changes.All.OfType<Deletion>());
        Assert.Empty(First.Marks.OfType<NoteMark>());
        Assert.NotNull(_document.Notes.Find("n1"));
        Assert.Contains(deletion.Removed.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks), mark => mark is NoteMark { NoteId: "n1" });
        Assert.Equal("word", First.Text);
    }

    [Fact]
    public void Accepting_the_deletion_removes_the_note()
    {
        _editor.RemoveNote(_document, _history, "n1");
        var deletion = Assert.Single(_document.Changes.All.OfType<Deletion>());

        _resolver.Resolve(_document, _history, [deletion.Id], RevisionVerdict.Accept);

        Assert.Empty(_document.Notes.All);
        Assert.Empty(_document.Changes.All);
        Assert.Equal("word", First.Text);
    }

    [Fact]
    public void Rejecting_the_deletion_brings_the_reference_and_the_note_back()
    {
        _editor.RemoveNote(_document, _history, "n1");
        var deletion = Assert.Single(_document.Changes.All.OfType<Deletion>());

        _resolver.Resolve(_document, _history, [deletion.Id], RevisionVerdict.Reject);

        var note = Assert.Single(_document.Notes.All);
        var mark = Assert.Single(First.Marks.OfType<NoteMark>());
        Assert.Equal(note.Id, mark.NoteId);
        Assert.Equal(4, mark.Offset);
        Assert.Equal("word", First.Text);
        Assert.Empty(_document.Changes.All);
    }
}
