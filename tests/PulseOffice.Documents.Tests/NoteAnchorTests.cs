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
}
