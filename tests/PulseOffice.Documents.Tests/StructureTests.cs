using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class PositionTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void LaPosizioneSegueInserimentiDivisioniEUnioni()
    {
        _f.Type("abcdef");
        using var tracker = new PositionTracker(_f.Document);
        var tracked = tracker.Track(_f.At(0, 4));

        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 1), "XY");
        Assert.Equal(_f.At(0, 6), tracked.Position);

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 3));
        Assert.Equal(_f.At(1, 3), tracked.Position);

        _f.Editor.MergeWithNext(_f.Document, _f.History, _f.First.Id);
        Assert.Equal(_f.At(0, 6), tracked.Position);

        _f.History.Undo();
        Assert.Equal(_f.At(1, 3), tracked.Position);
        _f.History.Undo();
        Assert.Equal(_f.At(0, 6), tracked.Position);
        _f.History.Undo();
        Assert.Equal(_f.At(0, 4), tracked.Position);
    }

    [Fact]
    public void UnaCancellazioneFraParagrafiPortaLaPosizioneNelPrimo()
    {
        _f.Type("uno", "due", "tre");
        var third = _f.Paragraph(2).Id;
        using var tracker = new PositionTracker(_f.Document);
        var inside = tracker.Track(new DocumentPosition(_f.Paragraph(1).Id, 1));
        var after = tracker.Track(new DocumentPosition(third, 2));

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), new DocumentPosition(third, 1)));

        Assert.Equal(_f.At(0, 1), inside.Position);
        Assert.Equal(_f.At(0, 2), after.Position);
        Assert.Equal("ure", _f.First.Text);

        _f.History.Undo();
        Assert.Equal(new DocumentPosition(third, 2), after.Position);
    }

    [Fact]
    public void LeModificheAnnunciano()
    {
        var changes = new List<TextChange>();
        _f.Document.TextChanged += (_, e) => changes.AddRange(e.Changes);

        _f.Type("ab");

        Assert.Equal(new TextInserted(_f.First.Id, 0, 2), Assert.Single(changes));
    }
}

public sealed class TableTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void LaTabellaSiInserisceAMetaParagrafoESiAnnulla()
    {
        _f.Type("primadopo");
        var before = _f.Describe();

        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(0, 5), 2, 3);

        Assert.Equal(3, _f.Document.Body.Count);
        var table = Assert.IsType<TableBlock>(_f.Document.Body[1]);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(3, table.ColumnCount);
        Assert.Equal("prima", _f.Paragraph(0).Text);
        Assert.Equal("dopo", _f.Paragraph(2).Text);
        Assert.Equal(DefaultStyleNames.TableContents, ((ParagraphBlock)table.Cell(1, 2).Content[0]).StyleName);

        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
    }

    [Fact]
    public void IlTestoNelleCelleSiTrovaESiModifica()
    {
        _f.Type("x");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(0, 1), 2, 2);
        var table = (TableBlock)_f.Document.Body[1];
        var cell = (ParagraphBlock)table.Cell(1, 0).Content[0];

        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(cell.Id, 0), "Cella");

        var location = _f.Tree.Locate(_f.Document.Body, cell.Id)!;
        Assert.Equal([1, 1, 0, 0], location.Path.Steps);
        Assert.Same(cell, _f.Tree.Resolve(_f.Document.Body, location.Path));
        Assert.Equal("x\n\t\nCella\t\n", new BlockText().Of(_f.Document.Body));
    }

    [Fact]
    public void UnaCancellazioneCheAttraversaLaTabellaLaRimuove()
    {
        _f.Type("ab", "cd");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(0, 2), 1, 1);
        var last = (ParagraphBlock)_f.Document.Body[^1];
        Assert.Equal(3, _f.Document.Body.Count);
        var before = _f.Describe();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), new DocumentPosition(last.Id, 1)));

        Assert.Single(_f.Document.Body.Blocks);
        Assert.Equal("ad", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.Describe());
    }

    [Fact]
    public void SezioniETabelleSonoVisitate()
    {
        var section = new SectionBlock("s1", "Area", new BlockContainer([new ParagraphBlock("p9", "dentro")]))
        {
            Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { Separator = true },
        };
        _f.Document.Body.Add(section);

        Assert.Contains(_f.Tree.Paragraphs(_f.Document.Body), p => p.Id == "p9");
        Assert.Equal([1, 0], _f.Tree.Locate(_f.Document.Body, "p9")!.Path.Steps);
        Assert.Equal("dentro", ((ParagraphBlock)_f.Tree.Resolve(_f.Document.Body, new BlockPath([1, 0]))!).Text);
        Assert.Equal(2, ((SectionBlock)section.Clone()).Columns.Count);
    }
}

public sealed class ObjectAndChangeTests
{
    private readonly Fixture _f = new();

    private AnchoredObject Image(ObjectAnchor anchor) => new("o1", anchor,
        new EmbeddedContent(new PackagedObject { Id = "img", FileName = "a.png", MediaType = "image/png", Data = new byte[] { 1, 2, 3 } }),
        new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)))
    { Wrap = WrapMode.TopAndBottom };

    [Fact]
    public void LOggettoComeCarattereSegueIlTestoESparisceConEsso()
    {
        _f.Type("abcd");
        _f.Editor.InsertObject(_f.Document, _f.History, Image(new AsCharacterAnchor(_f.First.Id)), _f.At(0, 2));
        Assert.Single(_f.Document.Objects.All);
        Assert.Equal(2, Assert.IsType<ObjectMark>(Assert.Single(_f.First.Marks)).Offset);

        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 1, 3));
        Assert.Empty(_f.Document.Objects.All);

        _f.History.Undo();
        Assert.Equal(WrapMode.TopAndBottom, Assert.Single(_f.Document.Objects.All).Wrap);
    }

    [Fact]
    public void LOggettoAncoratoAlParagrafoNonLasciaSegniNelTesto()
    {
        _f.Type("abcd");
        _f.Editor.InsertObject(_f.Document, _f.History, Image(new ParagraphAnchor(_f.First.Id)), _f.At(0, 0));

        Assert.Empty(_f.First.Marks);
        Assert.Single(_f.Document.Objects.AnchoredTo(_f.First.Id));
    }

    [Fact]
    public void UnaCasellaDiTestoEUnaStoriaModificabile()
    {
        var box = new TextBoxContent(new BlockContainer([new ParagraphBlock("pb", "box", DefaultStyleNames.FrameContents)]));
        _f.Document.Objects.Add(new AnchoredObject("f1", new PageAnchor(1), box, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2))));

        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition("pb", 3), "!");

        Assert.Equal("box!", ((ParagraphBlock)box.Content[0]).Text);
    }

    [Fact]
    public void LeRevisioniPortanoAutoreDataEContenuto()
    {
        var date = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
        _f.Document.Changes.IsRecording = true;
        _f.Document.Changes.Add(new Insertion("ct1", "Ada", date) { Comment = "aggiunta" });
        _f.Document.Changes.Add(new Deletion("ct2", "Bob", date, new BlockContainer([new ParagraphBlock("pd", "tolto")])));
        _f.Document.Changes.Add(new FormatChange("ct3", "Ada", date, [new TextRun("x")]));
        _f.First.Marks = [new ChangeMark(0, "ct1", MarkEdge.Start), new ChangeMark(0, "ct1", MarkEdge.End), new ChangeMark(0, "ct2", MarkEdge.Point)];

        Assert.Equal([ChangeKind.Insertion, ChangeKind.Deletion, ChangeKind.Format], _f.Document.Changes.All.Select(c => c.Kind));
        Assert.Equal(2, _f.Document.Changes.By("Ada").Count());
        Assert.Equal("tolto", ((ParagraphBlock)((Deletion)_f.Document.Changes.Find("ct2")!).Removed[0]).Text);

        _f.Editor.InsertText(_f.Document, _f.History, _f.At(0, 0), "nuovo");
        Assert.Equal([0, 5, 5], _f.First.Marks.Select(m => m.Offset));
    }
}

public sealed class CloneTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void IlDocumentoClonatoEIndipendente()
    {
        _f.Type("Uno", "Due");
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 3), NoteKind.Footnote, "n");
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 1), "Ada", DateTimeOffset.UnixEpoch, "c");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 3), 1, 1);
        _f.Document.Properties.Title = "Titolo";
        _f.Document.Variables["v"] = "1";
        _f.Document.Macros.Store(new Document.Forms.DocumentMacro("M", Document.Forms.MacroLanguage.JavaScript, "1"));
        var original = _f.Describe();

        var copy = _f.Document.Clone();
        var history = new UndoStack();
        _f.Editor.InsertText(copy, history, new DocumentPosition(_f.First.Id, 0), "X");
        ((ParagraphBlock)copy.Notes.All[0].Content[0]).Content = new TextParagraph("cambiata");
        copy.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Character = new() { Bold = true };
        copy.Properties.Title = "Altro";
        var copyTable = (TableBlock)copy.Body[2];
        copyTable.Rows.Clear();

        Assert.Equal(original, _f.Describe());
        Assert.Equal("n", ((ParagraphBlock)_f.Document.Notes.All[0].Content[0]).Text);
        Assert.Null(_f.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Character.Bold);
        Assert.Equal("Titolo", _f.Document.Properties.Title);
        Assert.Single(((TableBlock)_f.Document.Body[2]).Rows);
        Assert.Equal("1", copy.Variables["v"]);
        Assert.NotNull(copy.Macros.Find("M"));
        Assert.Single(copy.Comments.All);
        Assert.NotEqual(copy.NextId("p"), _f.First.Id);
    }

    [Fact]
    public void GliIdentificativiNuoviNonCollidonoDopoUnaRiserva()
    {
        var ids = new IdSequence();
        ids.Reserve("p41");
        ids.Reserve("p7");
        ids.Reserve("strano");

        Assert.Equal("p42", ids.Next("p"));
        Assert.Equal("t1", ids.Next("t"));
    }
}
