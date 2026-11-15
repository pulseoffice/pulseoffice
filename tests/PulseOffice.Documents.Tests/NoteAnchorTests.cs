using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class NoteAnchorTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void Scrivere_dopo_il_richiamo_della_nota_lascia_il_richiamo_sulla_parola()
    {
        _f.Type("anonimo");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 7), NoteKind.Footnote, "fonte");

        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 7), ", ha raggiunto");

        Assert.Equal(7, _f.First.State.Marks.OfType<NoteMark>().Single().Offset);
    }

    [Fact]
    public void Invio_dopo_il_richiamo_non_porta_la_nota_nel_paragrafo_nuovo()
    {
        _f.Type("anonimo");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 7), NoteKind.Footnote, "fonte");

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 7));

        Assert.Single(_f.First.State.Marks.OfType<NoteMark>());
        Assert.Empty(_f.Paragraph(1).State.Marks.OfType<NoteMark>());
    }

    [Fact]
    public void A_note_cannot_be_inserted_inside_a_note_or_a_comment()
    {
        _f.Type("anonimo");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 7), NoteKind.Footnote, "fonte");
        _f.Editor.AddComment(_f.Document, _f.History, new PulseOffice.Documents.Editing.DocumentRange(_f.At(0, 0), _f.At(0, 3)), "a", DateTimeOffset.UnixEpoch, "commento");
        var inNote = _f.Document.Notes.All[0].Content.Blocks.OfType<ParagraphBlock>().Single().Id;
        var inComment = _f.Document.Comments.All[0].Content.Blocks.OfType<ParagraphBlock>().Single().Id;

        Assert.True(_f.Stories.AcceptsNotes(_f.Document, _f.First.Id));
        Assert.False(_f.Stories.AcceptsNotes(_f.Document, inNote));
        Assert.False(_f.Stories.AcceptsNotes(_f.Document, inComment));
        Assert.Throws<InvalidOperationException>(() => _f.Editor.InsertNote(_f.Document, _f.History, new PulseOffice.Documents.Editing.DocumentPosition(inNote, 2), NoteKind.Footnote, "nested"));
        Assert.Single(_f.Document.Notes.All);
    }
}
