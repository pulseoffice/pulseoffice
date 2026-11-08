using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ResidueTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void CancellareTuttoIlTestoCommentatoToglieIlCommentoEAnnullareLoRiporta()
    {
        _f.Type("Testo commentato qui");
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 6, 16), "Ada", DateTimeOffset.UnixEpoch, "nota");
        var before = _f.Snapshot();
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 5, 17));
        Assert.Empty(_f.Document.Comments.All);
        Assert.DoesNotContain(_f.First.Marks, mark => mark is CommentMark);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void UnCommentoSuDueParagrafiCadeSeSiCancellaLaSuaAncora()
    {
        _f.Type("Uno due", "tre quattro");
        _f.Editor.AddComment(_f.Document, _f.History, new DocumentRange(_f.At(0, 4), _f.At(1, 3)), "Ada", DateTimeOffset.UnixEpoch, "nota");
        _f.Editor.AddComment(_f.Document, _f.History, default, "Bob", DateTimeOffset.UnixEpoch, "risposta", _f.Document.Comments.All[0].Id);
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 2, 6));
        Assert.Empty(_f.Document.Comments.All);
        Assert.DoesNotContain(_f.Paragraph(1).Marks, mark => mark is CommentMark);
    }

    [Fact]
    public void CancellareSoloUnaParteDelTestoCommentatoLasciaIlCommento()
    {
        _f.Type("Testo commentato qui");
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 6, 16), "Ada", DateTimeOffset.UnixEpoch, "nota");
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 8, 12));
        Assert.Single(_f.Document.Comments.All);
    }

    [Fact]
    public void DividereDentroUnCampoLoTieneIntero()
    {
        _f.Type("ab");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 1), TextFieldKind.PageNumber, "123");
        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 2));
        Assert.Equal("a123", _f.First.Content.Text);
        Assert.Equal("b", _f.Paragraph(1).Content.Text);
        Assert.Contains(_f.First.Content.Runs, run => run.Field == TextFieldKind.PageNumber && run.Text == "123");
    }

    [Fact]
    public void CancellareMezzoCampoCancellaTuttoIlCampo()
    {
        _f.Type("ab");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 1), TextFieldKind.PageNumber, "123");
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 2, 4));
        Assert.Equal("ab", _f.First.Content.Text);
        Assert.DoesNotContain(_f.First.Content.Runs, run => run.Field != TextFieldKind.None);
    }

    [Fact]
    public void ScrivereDentroUnCampoScriveDopoIlCampo()
    {
        _f.Type("ab");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 1), TextFieldKind.PageNumber, "123");
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 2), "x");
        Assert.Equal("a123xb", _f.First.Content.Text);
    }

    [Fact]
    public void LeOperazioniCheNonCambianoNullaNonEntranoNellaCronologia()
    {
        _f.Type("Testo");
        _f.Editor.SetCharacter(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 5), new CharacterProperties { Bold = true });
        var point = _f.History.StatePoint;
        _f.Editor.SetCharacter(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 5), new CharacterProperties { Bold = true });
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 2, 2));
        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 1), string.Empty);
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 0), _f.First.StyleName ?? DefaultStyleNames.Standard);
        Assert.Same(point, _f.History.StatePoint);
    }

    [Fact]
    public void IlGrassettoDirettoDelParagrafoVinceSulloStileDelParagrafo()
    {
        var paragraph = new TextParagraph("Testo") { Bold = true, StyleName = DefaultStyleNames.Standard }.WithRuns([new TextRun("Testo")]);
        var effective = _f.Resolver.EffectiveParagraph(_f.Document.Styles, paragraph);
        Assert.True(effective.Bold);
        Assert.True(effective.Runs.Single().Bold);
        var plain = _f.Resolver.EffectiveParagraph(_f.Document.Styles, new TextParagraph("Testo") { Bold = true });
        Assert.True(plain.SingleRun().Bold);
    }
}
