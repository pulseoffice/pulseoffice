using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class AuditTests
{
    private readonly Fixture _f = new();

    private TableBlock TableAfterFirst()
    {
        _f.Type("Prima", "Dopo");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 1, 2);
        var table = (TableBlock)_f.Document.Body[1];
        foreach (var cell in table.Rows.SelectMany(row => row.Cells))
        {
            _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(((ParagraphBlock)cell.Content[0]).Id, 0), "cc");
        }
        return table;
    }

    [Fact]
    public void UnaTabellaNuovaOccupaLaLarghezzaDelTestoEInUnaCellaQuellaDellaCella()
    {
        var table = TableAfterFirst();
        var page = _f.Document.Styles.Page.Find(_f.Document.InitialPageStyle)!;
        var text = page.Size.Width.Value - page.Margins.Left.Value - page.Margins.Right.Value;
        Assert.InRange(table.Columns.Sum(column => column.Value), text - 2, text);

        var cell = table.Rows[0].Cells[1];
        _f.Editor.InsertTable(_f.Document, _f.History, new DocumentPosition(((ParagraphBlock)cell.Content[0]).Id, 0), 2, 3);

        var nested = cell.Content.Blocks.OfType<TableBlock>().Single();
        Assert.InRange(nested.Columns.Sum(column => column.Value), table.Columns[1].Value - 300, table.Columns[1].Value);
    }

    [Fact]
    public void IlFormatoSuUnIntervalloCheAttraversaUnaTabellaToccaLeCelle()
    {
        var table = TableAfterFirst();
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(2, 4)), new CharacterProperties { Bold = true });
        Assert.All(table.Rows.SelectMany(row => row.Cells), cell => Assert.True(((ParagraphBlock)cell.Content[0]).Content.Runs.Single().Bold));
        Assert.True(_f.Paragraph(2).Content.Runs.Single().Bold);
    }

    [Fact]
    public void IlFormatoDalCorpoDentroUnaCellaSiFermaAllaCella()
    {
        var table = TableAfterFirst();
        var cell = (ParagraphBlock)table.Cell(0, 0).Content[0];
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(0, 2), new DocumentPosition(cell.Id, 1)), new CharacterProperties { Bold = true });
        Assert.Equal(["Pr", "ima"], _f.First.Content.Runs.Select(run => run.Text));
        Assert.True(cell.Content.Runs[0].Bold);
        Assert.False(cell.Content.Runs[1].Bold);
        Assert.False(((ParagraphBlock)table.Cell(0, 1).Content[0]).Content.Runs.Single().Bold);
        Assert.False(_f.Paragraph(2).Content.Runs.Single().Bold);
    }

    [Fact]
    public void AndareACapoDopoUnOggettoComeCarattereLoPortaConSe()
    {
        _f.Type("abcd");
        var item = new AnchoredObject("o1", new AsCharacterAnchor(_f.First.Id),
            new EmbeddedContent(new PulseOffice.Document.Embedding.PackagedObject { Id = "img", FileName = "a.png", MediaType = "image/png", Data = new byte[] { 1 } }),
            new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1)));
        _f.Editor.InsertObject(_f.Document, _f.History, item, _f.At(0, 3));
        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 1));
        Assert.Equal(new AsCharacterAnchor(_f.Paragraph(1).Id), _f.Document.Objects.Find("o1")!.Anchor);
        _f.History.Undo();
        Assert.Equal(new AsCharacterAnchor(_f.First.Id), _f.Document.Objects.Find("o1")!.Anchor);
    }

    [Fact]
    public void UnIdentificativoTroppoLungoNonFaCadere()
    {
        _f.Document.Ids.Reserve("p99999999999999999999");
        Assert.StartsWith("p", _f.Document.NextId("p"));
    }

    [Fact]
    public void FormattareMezzoCampoNonLoRaddoppia()
    {
        _f.Type("ab");
        _f.Editor.InsertField(_f.Document, _f.History, _f.At(0, 1), TextFieldKind.SlideNumber, "12");
        _f.Editor.SetCharacter(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 2), new CharacterProperties { Bold = true });
        Assert.Single(_f.First.Content.Runs, run => run.Field != TextFieldKind.None);
    }

    private DocumentClipboard Clipboard()
    {
        var scanner = new FragmentScanner(_f.Tree);
        var extractor = new FragmentExtractor(_f.Trails, _f.Text, _f.Tree, scanner, new StyleClosure(scanner));
        return new DocumentClipboard(extractor, _f.Editor);
    }

    [Fact]
    public void AnnullareTuttoRiportaEsattamenteAllInizioERipristinareAllaFine()
    {
        var table = TableAfterFirst();
        var cell = (ParagraphBlock)table.Cell(0, 1).Content[0];
        _f.Editor.InsertNote(_f.Document, _f.History, new DocumentPosition(cell.Id, 1), NoteKind.Footnote, "n");
        _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 1, 3), "Ada", DateTimeOffset.UnixEpoch, "c");
        _f.Editor.AddBookmark(_f.Document, _f.History, new DocumentRange(_f.At(0, 2), _f.At(2, 1)), "segno");
        var start = _f.Snapshot();
        var mark = _f.History.StatePoint;
        var clipboard = Clipboard();
        var fragment = clipboard.Copy(_f.Document, new DocumentRange(_f.At(0, 1), _f.At(2, 2)));
        clipboard.Cut(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), new DocumentPosition(cell.Id, 2)));
        clipboard.Paste(_f.Document, _f.History, _f.At(0, 0), fragment);
        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 0));
        _f.Editor.MergeWithNext(_f.Document, _f.History, _f.First.Id);
        var end = _f.Snapshot();
        while (!ReferenceEquals(_f.History.StatePoint, mark)) _f.History.Undo();
        Assert.Equal(start, _f.Snapshot());
        while (_f.History.CanRedo) _f.History.Redo();
        Assert.Equal(end, _f.Snapshot());
    }
}
