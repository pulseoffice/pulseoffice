using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ObjectEditingTests
{
    private readonly Fixture _f = new();
    private readonly ObjectArrangement _arrangement;
    private readonly Captions _captions;
    private readonly FrameLinking _linking = new(new FrameChains());

    public ObjectEditingTests()
    {
        _arrangement = new ObjectArrangement(_f.Stories, _f.Text);
        _captions = new Captions(_f.Stories, _f.Tree, new StyleEditing(new StyleReferences(_f.Stories, _f.Tree)), _f.Sequences);
    }

    private AnchoredObject Picture(string id, ObjectAnchor anchor) =>
        new(id, anchor, new EmbeddedContent(new PulseOffice.Drawing.Assets.RasterImageObject { Id = id + "-c", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)));

    private AnchoredObject Frame(string id, string paragraphId, string text) =>
        new(id, new ParagraphAnchor(paragraphId), new TextBoxContent(new BlockContainer([new ParagraphBlock(id + "-p", text)])), new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(2)));

    private AnchoredObject Inline(string id, int paragraph)
    {
        var item = Picture(id, new AsCharacterAnchor(_f.Paragraph(paragraph).Id));
        _f.Editor.InsertObject(_f.Document, _f.History, item, _f.At(paragraph, 0));
        return item;
    }

    [Fact]
    public void An_object_already_on_top_cannot_move_forward_and_one_at_the_bottom_cannot_move_back()
    {
        _f.Type("uno");
        foreach (var id in new[] { "a", "b" })
        {
            var item = Picture(id, new ParagraphAnchor(_f.First.Id));
            item.ZOrder = _f.Document.Objects.All.Count;
            _f.Document.Objects.Add(item);
        }

        Assert.False(_arrangement.Moves(_f.Document, "b", ObjectOrder.Front));
        Assert.False(_arrangement.Moves(_f.Document, "b", ObjectOrder.Forward));
        Assert.True(_arrangement.Moves(_f.Document, "b", ObjectOrder.Backward));
        Assert.False(_arrangement.Moves(_f.Document, "a", ObjectOrder.Back));
        Assert.True(_arrangement.Moves(_f.Document, "a", ObjectOrder.Front));
    }

    [Fact]
    public void LOrdineMetteDavantiEDietroESiAnnulla()
    {
        _f.Type("uno");
        foreach (var id in new[] { "a", "b", "c" })
        {
            var item = Picture(id, new ParagraphAnchor(_f.First.Id));
            item.ZOrder = _f.Document.Objects.All.Count;
            _f.Document.Objects.Add(item);
        }

        _arrangement.Order(_f.Document, _f.History, "a", ObjectOrder.Front);
        Assert.Equal(["b", "c", "a"], _f.Document.Objects.All.OrderBy(item => item.ZOrder).Select(item => item.Id));
        _arrangement.Order(_f.Document, _f.History, "a", ObjectOrder.Backward);
        Assert.Equal(["b", "a", "c"], _f.Document.Objects.All.OrderBy(item => item.ZOrder).Select(item => item.Id));
        _arrangement.Order(_f.Document, _f.History, "c", ObjectOrder.Back);
        Assert.Equal(["c", "b", "a"], _f.Document.Objects.All.OrderBy(item => item.ZOrder).Select(item => item.Id));

        _f.History.Undo();
        _f.History.Undo();
        _f.History.Undo();
        Assert.Equal(["a", "b", "c"], _f.Document.Objects.All.OrderBy(item => item.ZOrder).Select(item => item.Id));
    }

    [Fact]
    public void LAncoraCambiaETogliERimetteIlSegno()
    {
        _f.Type("testo qui");
        var item = Inline("img", 0);
        Assert.Contains(_f.First.Marks, mark => mark is ObjectMark { ObjectId: "img" });

        _arrangement.Reanchor(_f.Document, _f.History, "img", new PageAnchor(1), new ObjectPlacement(Twip.FromCentimeters(2), Twip.FromCentimeters(3)), null);
        Assert.IsType<PageAnchor>(item.Anchor);
        Assert.DoesNotContain(_f.First.Marks, mark => mark is ObjectMark);

        _arrangement.Reanchor(_f.Document, _f.History, "img", new CharacterAnchor(_f.First.Id), new ObjectPlacement(Twip.Zero, Twip.Zero), _f.At(0, 5));
        Assert.Contains(_f.First.Marks, mark => mark is ObjectMark { ObjectId: "img", Offset: 5 });

        _f.History.Undo();
        _f.History.Undo();
        Assert.IsType<AsCharacterAnchor>(item.Anchor);
        Assert.Contains(_f.First.Marks, mark => mark is ObjectMark { ObjectId: "img", Offset: 0 });
    }

    [Fact]
    public void LeDidascalieSiNumeranoPerCategoriaESiRinumeranoCancellando()
    {
        _f.Type("prima", "seconda");
        Inline("a", 0);
        Inline("b", 1);

        _captions.CaptionObject(_f.Document, _f.History, "a", new CaptionRequest("Figure", "Figura", "il primo", false));
        _captions.CaptionObject(_f.Document, _f.History, "b", new CaptionRequest("Figure", "Figura", "il secondo", false));

        var captions = _f.Document.Body.Blocks.OfType<ParagraphBlock>().Where(paragraph => paragraph.StyleName == "Figure").ToList();
        Assert.Equal(["Figura 1: il primo", "Figura 2: il secondo"], captions.Select(paragraph => paragraph.Text));
        Assert.Equal("Caption", _f.Document.Styles.Paragraph.Find("Figure")!.ParentName);
        Assert.Equal(TextFieldKind.Sequence, captions[0].Content.Runs[1].Field);
        Assert.Equal("Figure", captions[0].Content.Runs[1].FieldArgument);

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(new DocumentPosition(_f.Paragraph(0).Id, 0), new DocumentPosition(captions[0].Id, captions[0].Length)));
        Assert.Equal("Figura 1: il secondo", captions[1].Text);

        _f.History.Undo();
        Assert.Equal("Figura 2: il secondo", captions[1].Text);
    }

    [Fact]
    public void LaDidascaliaDiUnOggettoAncoratoLoMetteInUnaCornice()
    {
        _f.Type("testo");
        var item = Picture("img", new ParagraphAnchor(_f.First.Id));
        item.Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(1));
        item.Wrap = WrapMode.Parallel;
        item.WrapContour = true;
        _f.Document.Objects.Add(item);

        _captions.CaptionObject(_f.Document, _f.History, "img", new CaptionRequest("Figure", "Figura", "foto", false));

        var frame = _f.Document.Objects.All.Single(other => other.Content is TextBoxContent);
        var box = (TextBoxContent)frame.Content;
        Assert.Equal(item.Placement, new ObjectPlacement(Twip.Zero, Twip.Zero));
        Assert.Equal(Twip.FromCentimeters(1), frame.Placement.X);
        Assert.Equal(WrapMode.Parallel, frame.Wrap);
        Assert.IsType<AsCharacterAnchor>(item.Anchor);
        Assert.False(item.WrapContour);
        Assert.Equal(((ParagraphBlock)box.Content[0]).Id, ((AsCharacterAnchor)item.Anchor).ParagraphId);
        Assert.Equal("Figura 1: foto", ((ParagraphBlock)box.Content[1]).Text);

        _f.History.Undo();
        Assert.Single(_f.Document.Objects.All);
        Assert.IsType<ParagraphAnchor>(_f.Document.Objects.All[0].Anchor);
    }

    [Fact]
    public void LaDidascaliaDellaTabellaVaSopra()
    {
        _f.Type("testo");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(0, 5), 2, 2);
        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();

        _captions.CaptionTable(_f.Document, _f.History, table.Id, new CaptionRequest("Table", "Tabella", "prezzi", true));

        var index = _f.Document.Body.Blocks.ToList().IndexOf(table);
        Assert.Equal("Tabella 1: prezzi", ((ParagraphBlock)_f.Document.Body[index - 1]).Text);
    }

    [Fact]
    public void LeCorniciSiCollegano()
    {
        _f.Type("testo");
        var first = Frame("f1", _f.First.Id, "contenuto");
        var second = Frame("f2", _f.First.Id, string.Empty);
        var full = Frame("f3", _f.First.Id, "pieno");
        _f.Document.Objects.Add(first);
        _f.Document.Objects.Add(second);
        _f.Document.Objects.Add(full);
        var chains = new FrameChains();

        Assert.False(chains.CanLink(_f.Document, first, full));
        _linking.Link(_f.Document, _f.History, "f1", "f2");

        Assert.Equal("f2", ((TextBoxContent)_f.Document.Objects.Find("f1")!.Content).NextFrameName);
        Assert.Equal(["f1", "f2"], chains.Chain(_f.Document, _f.Document.Objects.Find("f2")!).Select(item => item.Id));
        Assert.False(chains.CanLink(_f.Document, _f.Document.Objects.Find("f2")!, _f.Document.Objects.Find("f1")!));

        _linking.Unlink(_f.Document, _f.History, "f2");
        Assert.Null(((TextBoxContent)_f.Document.Objects.Find("f1")!.Content).NextFrameName);

        _f.History.Undo();
        Assert.Equal("f2", ((TextBoxContent)_f.Document.Objects.Find("f1")!.Content).NextFrameName);
    }
}
