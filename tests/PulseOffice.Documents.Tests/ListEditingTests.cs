using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ListEditingTests
{
    private readonly Fixture _f = new();
    private readonly ListEditing _lists;

    public ListEditingTests()
    {
        _lists = new ListEditing(_f.Stories, _f.Resolver);
        _f.Type("A", "A1", "A2", "B", "C");
        _f.Editor.SetList(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(4, 0)), DefaultStyleNames.Numbering123, 0);
        _f.Editor.SetList(_f.Document, _f.History, new DocumentRange(_f.At(1, 0), _f.At(2, 0)), DefaultStyleNames.Numbering123, 1);
    }

    private IEnumerable<string> Texts => _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(paragraph => paragraph.Text);

    private IEnumerable<int> Levels => _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(paragraph => paragraph.Content.BulletLevel);

    [Fact]
    public void SetListSegnaLeVociComeNumerate()
    {
        Assert.All(_f.Document.Body.Blocks.Cast<ParagraphBlock>(), paragraph => Assert.True(paragraph.Content.Numbered));
    }

    [Fact]
    public void AbbassareConISottopuntiPortaGiuAncheIFigli()
    {
        _lists.ShiftLevel(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), 1, true);

        Assert.Equal([1, 2, 2, 0, 0], Levels);
        _f.History.Undo();
        Assert.Equal([0, 1, 1, 0, 0], Levels);
    }

    [Fact]
    public void SpostareInGiuConISottopuntiScavalcaLaVoceSeguente()
    {
        _lists.Move(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), 1, true);

        Assert.Equal(["B", "A", "A1", "A2", "C"], Texts);
        _f.History.Undo();
        Assert.Equal(["A", "A1", "A2", "B", "C"], Texts);
    }

    [Fact]
    public void SpostareInSuConISottopuntiScavalcaIlGruppoPrecedente()
    {
        _lists.Move(_f.Document, _f.History, DocumentRange.At(_f.At(3, 0)), -1, true);

        Assert.Equal(["B", "A", "A1", "A2", "C"], Texts);
    }

    [Fact]
    public void Continuing_is_possible_only_where_it_changes_something()
    {
        Assert.False(_lists.CanContinue(_f.Document, DocumentRange.At(_f.At(4, 0))));
        _lists.Restart(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)), 7);
        Assert.True(_lists.CanContinue(_f.Document, DocumentRange.At(_f.At(4, 0))));
    }

    [Fact]
    public void LaVoceSenzaNumeroELaRipartenzaSiAnnullano()
    {
        _lists.SetNumbered(_f.Document, _f.History, DocumentRange.At(_f.At(3, 0)), false);
        _lists.Restart(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)), 7);

        Assert.True(_f.Paragraph(3).Content.ListHeader);
        Assert.False(_f.Paragraph(3).Content.IsListItem);
        Assert.Equal(7, _f.Paragraph(4).Content.ListRestart);

        _lists.Continue(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)));
        Assert.Null(_f.Paragraph(4).Content.ListRestart);
        _f.History.Undo();
        _f.History.Undo();
        _f.History.Undo();
        Assert.False(_f.Paragraph(3).Content.ListHeader);
    }

    [Fact]
    public void IlParagrafoSeguenteDelLivelloSaltaISottopunti()
    {
        Assert.Equal(_f.Paragraph(3).Id, _lists.NextInLevel(_f.Document, _f.Paragraph(0).Id, 1));
        Assert.Equal(_f.Paragraph(2).Id, _lists.NextInLevel(_f.Document, _f.Paragraph(1).Id, 1));
        Assert.Null(_lists.NextInLevel(_f.Document, _f.Paragraph(2).Id, 1));
    }

    [Fact]
    public void IlTitoloSiAbbassaCambiandoStile()
    {
        _f.Editor.SetList(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)), null, 0);
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)), DefaultStyleNames.HeadingLevel(1));

        _lists.ShiftLevel(_f.Document, _f.History, DocumentRange.At(_f.At(4, 0)), 1, false);

        Assert.Equal(DefaultStyleNames.HeadingLevel(2), _f.Paragraph(4).StyleName);
    }

    [Fact]
    public void LeModificheAgliStiliSiAnnullano()
    {
        var styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree));
        styles.Change(_f.Document, _f.History, _f.Document.Styles.List, DefaultStyleNames.Numbering123,
            list => list.Levels = [.. list.Levels.Select(level => level with { Suffix = ")" })], "list-style");
        styles.ChangeOutline(_f.Document, _f.History, new ListStyle(StyleSheet.OutlineStyleName, [new ListLevel(NumberFormat.UpperRoman)]), "outline");

        Assert.Equal(")", _f.Document.Styles.List.Find(DefaultStyleNames.Numbering123)!.Level(0).Suffix);
        Assert.Equal(NumberFormat.UpperRoman, _f.Document.Styles.Outline.Level(0).Format);
        _f.History.Undo();
        _f.History.Undo();
        Assert.Equal(".", _f.Document.Styles.List.Find(DefaultStyleNames.Numbering123)!.Level(0).Suffix);
        Assert.Equal(NumberFormat.None, _f.Document.Styles.Outline.Level(0).Format);
    }
}
