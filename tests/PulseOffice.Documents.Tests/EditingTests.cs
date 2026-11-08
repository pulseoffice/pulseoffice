using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class EditingTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void ScrivereSuPiuRigheCreaParagrafi()
    {
        _f.Type("Uno", "Due", "Tre");

        Assert.Equal(["Uno", "Due", "Tre"], _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
        Assert.All(_f.Document.Body.Blocks.Cast<ParagraphBlock>(), p => Assert.Equal(DefaultStyleNames.Standard, p.StyleName));
    }

    [Fact]
    public void ScrivereEUnSoloAnnullamentoCheRiportaEsattamente()
    {
        var before = _f.Describe();
        _f.Type("Uno", "Due");
        var after = _f.Describe();

        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
        Assert.False(_f.History.CanUndo);

        _f.History.Redo();
        Assert.Equal(after, _f.Describe());
    }

    [Fact]
    public void CancellareFraParagrafiLiUnisceEAnnullareLiRestituisce()
    {
        _f.Type("Primo", "Secondo", "Terzo");
        var before = _f.Describe();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 2), _f.At(2, 3)));

        Assert.Single(_f.Document.Body.Blocks);
        Assert.Equal("Przo", _f.First.Text);

        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
        _f.History.Redo();
        Assert.Equal("Przo", _f.First.Text);
    }

    [Fact]
    public void DividereInFondoAUnTitoloPassaAlloStileSeguente()
    {
        _f.Type("Capitolo");
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Heading 1");

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 8));

        Assert.Equal("Heading 1", _f.Paragraph(0).StyleName);
        Assert.Equal(DefaultStyleNames.TextBody, _f.Paragraph(1).StyleName);
    }

    [Fact]
    public void DividereAMetaConservaLoStile()
    {
        _f.Type("Capitolo");
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Heading 1");
        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 4));

        Assert.Equal(["Capi", "tolo"], _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
        Assert.Equal("Heading 1", _f.Paragraph(1).StyleName);
    }

    [Fact]
    public void UnireDueParagrafiEAnnullare()
    {
        _f.Type("Alfa", "Beta");
        var before = _f.Describe();

        _f.Editor.MergeWithNext(_f.Document, _f.History, _f.First.Id);
        Assert.Equal("AlfaBeta", _f.First.Text);

        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
    }

    [Fact]
    public void IlGrassettoSuUnIntervalloSpezzaLePorzioniESiAnnulla()
    {
        _f.Type("Ciao mondo");
        var before = _f.Describe();

        _f.Editor.SetCharacter(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 5, 10), new CharacterProperties { Bold = true });

        Assert.Equal(["Ciao ", "mondo"], _f.First.Content.Runs.Select(r => r.Text));
        Assert.True(_f.First.Content.Runs[1].Bold);

        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 10), "!");
        Assert.True(_f.First.Content.Runs[1].Bold);
        Assert.Equal("mondo!", _f.First.Content.Runs[1].Text);

        _f.History.Undo();
        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
    }

    [Fact]
    public void LoStileDiCarattereSiApplicaSuPiuParagrafi()
    {
        _f.Type("Uno", "Due");
        _f.Editor.ApplyCharacterStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(1, 2)), DefaultStyleNames.Emphasis);

        Assert.Equal(DefaultStyleNames.Emphasis, _f.Paragraph(0).Content.Runs[1].StyleName);
        Assert.Null(_f.Paragraph(0).Content.Runs[0].StyleName);
        Assert.Equal(DefaultStyleNames.Emphasis, _f.Paragraph(1).Content.Runs[0].StyleName);
        Assert.Null(_f.Paragraph(1).Content.Runs[1].StyleName);
        Assert.Throws<KeyNotFoundException>(() => _f.Editor.ApplyCharacterStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Nope"));
    }

    [Fact]
    public void IlRientroDirettoSiAnnulla()
    {
        _f.Type("Testo");
        _f.Editor.SetParagraph(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), new ParagraphProperties { Align = TextAlign.Justify, MarginLeft = 20 });

        Assert.Equal(TextAlign.Justify, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).Align);
        Assert.Equal(20, _f.First.Content.MarginLeft);

        _f.History.Undo();
        Assert.Null(_f.First.Content.MarginLeft);
        Assert.Equal(TextAlign.Left, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).Align);
    }

    [Fact]
    public void LeTransazioniRaggruppanoPiuModifiche()
    {
        _f.Type("abc");
        var before = _f.Describe();
        using (_f.History.BeginTransaction("group"))
        {
            _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 3), "def");
            _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 3));
        }

        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
    }

    [Fact]
    public void IlCampoEUnaPorzioneASeStante()
    {
        _f.Type("Pagina ");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 7), TextFieldKind.PageNumber, "1");
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 8), " di 3");

        Assert.Equal(["Pagina ", "1", " di 3"], _f.First.Content.Runs.Select(r => r.Text));
        Assert.Equal(TextFieldKind.PageNumber, _f.First.Content.Runs[1].Field);
        Assert.Equal(TextFieldKind.None, _f.First.Content.Runs[2].Field);
    }

    [Fact]
    public void IlSegnalibroSiSpostaConIlTesto()
    {
        _f.Type("abcdef");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 2, 4), "mark");
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 0), "XX");
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 6), "Y");

        var marks = _f.First.Marks.Cast<BookmarkMark>().ToList();
        Assert.Equal(4, marks.Single(m => m.Edge == MarkEdge.Start).Offset);
        Assert.Equal(7, marks.Single(m => m.Edge == MarkEdge.End).Offset);
    }

    [Fact]
    public void CancellareIlRichiamoEliminaLaNotaEAnnullareLaRimette()
    {
        _f.Type("Testo");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 5), NoteKind.Footnote, "Fonte");
        var note = Assert.Single(_f.Document.Notes.All);
        Assert.Equal("Fonte", ((ParagraphBlock)note.Content[0]).Text);
        Assert.Equal(DefaultStyleNames.Footnote, ((ParagraphBlock)note.Content[0]).StyleName);
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 5), "!");
        Assert.Equal(5, Assert.IsType<NoteMark>(Assert.Single(_f.First.Marks)).Offset);
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 5, 6));
        Assert.Equal(5, Assert.IsType<NoteMark>(Assert.Single(_f.First.Marks)).Offset);
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 2), NoteKind.Footnote, "Altra");
        Assert.Equal(2, _f.Document.Notes.All.Count);

        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 1, 3));
        Assert.Single(_f.Document.Notes.All);
        Assert.Single(_f.First.Marks);
        _f.History.Undo();
        Assert.Equal(2, _f.First.Marks.Count);
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 5));
        Assert.Single(_f.Document.Notes.All);
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 0));
        _f.History.Undo();
        Assert.Equal(2, _f.Document.Notes.All.Count);
        Assert.Equal(2, _f.First.Marks.Count);
    }

    [Fact]
    public void LaNotaSiModificaComeOgniAltroTesto()
    {
        _f.Type("Testo");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 5), NoteKind.Endnote, "Fonte");
        var paragraph = (ParagraphBlock)_f.Document.Notes.All[0].Content[0];

        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(paragraph.Id, 5), " citata");

        Assert.Equal("Fonte citata", paragraph.Text);
        Assert.Equal(NoteKind.Endnote, _f.Document.Notes.All[0].Kind);
    }

    [Fact]
    public void CommentiConRisposte()
    {
        _f.Type("Una frase da discutere");
        var created = _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 4, 9), "Ada", DateTimeOffset.UnixEpoch, "Perché?");
        var root = _f.Document.Comments.All[0];
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Bob", DateTimeOffset.UnixEpoch, "Così", root.Id);
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Ada", DateTimeOffset.UnixEpoch, "Ok", _f.Document.Comments.All[1].Id);

        Assert.Equal(["Ada", "Bob", "Ada"], _f.Document.Comments.Thread(root.Id).Select(c => c.Author));
        Assert.Equal(2, _f.First.Marks.Count);
        Assert.True(_f.Document.Comments.All[1].IsReply);
        Assert.Equal("add-comment", created.Description);

        _f.History.Undo();
        _f.History.Undo();
        _f.History.Undo();
        Assert.Empty(_f.Document.Comments.All);
        Assert.Empty(_f.First.Marks);
    }
}
