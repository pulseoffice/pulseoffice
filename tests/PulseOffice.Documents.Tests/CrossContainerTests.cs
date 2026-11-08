using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class CrossContainerTests
{
    private readonly Fixture _f = new();

    private TableBlock TableAfterFirst()
    {
        _f.Type("Prima", "Dopo");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 2, 2);
        var table = (TableBlock)_f.Document.Body[1];
        var texts = new[] { "a1", "b1", "a2", "b2" };
        var cells = table.Rows.SelectMany(row => row.Cells).ToList();
        for (var i = 0; i < 4; i++)
        {
            var paragraph = (ParagraphBlock)cells[i].Content[0];
            _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(paragraph.Id, 0), texts[i]);
        }
        return table;
    }

    private ParagraphBlock CellParagraph(TableBlock table, int row, int column) => (ParagraphBlock)table.Cell(row, column).Content[0];

    [Fact]
    public void DalCorpoDentroUnaCellaSvuotaLeCelleMaTieneLaTabella()
    {
        var table = TableAfterFirst();
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 2), new DocumentPosition(CellParagraph(table, 1, 0).Id, 1)));

        Assert.Equal("Pr", _f.First.Text);
        Assert.Same(table, _f.Document.Body[1]);
        Assert.Equal(["", "", "2", "b2"], table.Rows.SelectMany(row => row.Cells).Select(cell => ((ParagraphBlock)cell.Content[0]).Text));
        Assert.Equal("Dopo", _f.Paragraph(2).Text);

        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _f.History.Redo();
        Assert.Equal("Pr", _f.First.Text);
    }

    [Fact]
    public void DaUnaCellaAlCorpoDopoLaTabella()
    {
        var table = TableAfterFirst();
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(new DocumentPosition(CellParagraph(table, 0, 1).Id, 1), _f.At(2, 2)));

        Assert.Equal(["a1", "b", "", ""], table.Rows.SelectMany(row => row.Cells).Select(cell => ((ParagraphBlock)cell.Content[0]).Text));
        Assert.Equal("po", _f.Paragraph(2).Text);
        Assert.Equal("Prima", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void UnaTabellaTuttaDentroLIntervalloSparisce()
    {
        TableAfterFirst();
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 3), _f.At(2, 1)));

        Assert.Single(_f.Document.Body.Blocks);
        Assert.Equal("Priopo", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _f.History.Redo();
        Assert.Equal("Priopo", _f.First.Text);
    }

    [Fact]
    public void LaSelezioneEsattaDiUnaTabellaLaElimina()
    {
        var table = TableAfterFirst();
        var start = new DocumentPosition(CellParagraph(table, 0, 0).Id, 0);
        var end = new DocumentPosition(CellParagraph(table, 1, 1).Id, 2);
        using var tracker = new PositionTracker(_f.Document);
        var caret = tracker.Track(end);

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(start, end));

        Assert.Equal(["Prima", "Dopo"], _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
        Assert.Equal(_f.Paragraph(1).Id, caret.Position.ParagraphId);
    }

    [Fact]
    public void DaCellaACellaSvuotaQuelleInMezzoESoloLaCodaDellaPrima()
    {
        var table = TableAfterFirst();
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History,
            new DocumentRange(new DocumentPosition(CellParagraph(table, 0, 0).Id, 1), new DocumentPosition(CellParagraph(table, 1, 1).Id, 1)));

        Assert.Equal(["a", "", "", "2"], table.Rows.SelectMany(row => row.Cells).Select(cell => ((ParagraphBlock)cell.Content[0]).Text));
        Assert.Equal(3, _f.Document.Body.Count);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void UnaCellaConPiuParagrafiSiUnisceComeNelCorpo()
    {
        var table = TableAfterFirst();
        var cell = CellParagraph(table, 0, 0);
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(cell.Id, 2), "\nx\nyz");
        var last = (ParagraphBlock)table.Cell(0, 0).Content[2];

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 5), new DocumentPosition(last.Id, 1)));

        Assert.Single(table.Cell(0, 0).Content.Blocks);
        Assert.Equal("z", ((ParagraphBlock)table.Cell(0, 0).Content[0]).Text);
    }

    [Fact]
    public void UnaSezioneAttraversataPerdeSoloIlContenutoSelezionato()
    {
        _f.Type("Uno", "Fine");
        var section = new SectionBlock("s9", "Sezione", new BlockContainer([new ParagraphBlock("sp1", "Dentro"), new ParagraphBlock("sp2", "Ancora")]));
        _f.Document.Body.Insert(1, section);
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), new DocumentPosition("sp1", 3)));

        Assert.Equal("U", _f.First.Text);
        Assert.Same(section, _f.Document.Body[1]);
        Assert.Equal(["tro", "Ancora"], section.Content.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void UnaSezioneCopertaDallInizioSparisce()
    {
        _f.Type("Uno", "Fine");
        _f.Document.Body.Insert(1, new SectionBlock("s9", "Sezione", new BlockContainer([new ParagraphBlock("sp1", "Dentro")])));

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(new DocumentPosition("sp1", 0), _f.At(2, 2)));

        Assert.Equal(["Uno", "ne"], _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
    }

    [Fact]
    public void LeNoteDentroUnaTabellaRimossaSparisconoETornano()
    {
        var table = TableAfterFirst();
        _f.Editor.InsertNote(_f.Document, _f.History, new DocumentPosition(CellParagraph(table, 1, 1).Id, 1), NoteKind.Footnote, "nota");
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(2, 1)));

        Assert.Empty(_f.Document.Notes.All);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        Assert.Single(_f.Document.Notes.All);
    }

    [Fact]
    public void UnirePortaConSeGliOggettiAncoratiAlSecondoParagrafo()
    {
        _f.Type("Uno", "Due");
        var second = _f.Paragraph(1).Id;
        var item = new AnchoredObject("o1", new ParagraphAnchor(second), new TextBoxContent(new BlockContainer([new ParagraphBlock("bx", "box")])),
            new PulseOffice.Foundation.Units.DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(2)));
        _f.Editor.InsertObject(_f.Document, _f.History, item, _f.At(1, 0));
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 3), _f.At(1, 0)));

        Assert.Equal(new ParagraphAnchor(_f.First.Id), _f.Document.Objects.Find("o1")!.Anchor);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        Assert.Equal(new ParagraphAnchor(second), _f.Document.Objects.Find("o1")!.Anchor);
    }

    [Fact]
    public void StorieDiverseEOrdineInversoSonoRifiutati()
    {
        var table = TableAfterFirst();
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 1), NoteKind.Footnote, "nota");
        var noteParagraph = (ParagraphBlock)_f.Document.Notes.All[0].Content[0];

        Assert.Throws<InvalidOperationException>(() =>
            _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), new DocumentPosition(noteParagraph.Id, 1))));
        Assert.Throws<InvalidOperationException>(() =>
            _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(new DocumentPosition(CellParagraph(table, 0, 0).Id, 1), _f.At(0, 1))));
    }

    [Fact]
    public void ISegniInLineaInUnParagrafoRimossoSparisconoSoloConLui()
    {
        _f.Type("Uno", "Due", "Tre");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.In(_f.Paragraph(1).Id, 0, 3), "segno");
        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(2, 1)));

        Assert.DoesNotContain(_f.First.Marks, mark => mark is BookmarkMark);
        Assert.Equal(DefaultStyleNames.Standard, _f.First.StyleName);
    }
}
