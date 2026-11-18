using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round219NoteRemovalTests
{
    [Fact]
    public void Removing_a_note_drops_its_reference_and_undo_brings_both_back()
    {
        var f = new Fixture();
        f.Type("word");
        f.Editor.InsertNote(f.Document, f.History, f.At(0, 4), NoteKind.Footnote, "note text");
        var note = Assert.Single(f.Document.Notes.All);
        var before = f.Snapshot();

        f.Editor.RemoveNote(f.Document, f.History, note.Id);

        Assert.Empty(f.Document.Notes.All);
        Assert.Empty(f.First.Marks.OfType<NoteMark>());
        Assert.Equal("word", f.First.Content.Text);
        f.History.Undo();
        Assert.Equal(before, f.Snapshot());
    }
}
