using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioObjectTests
{
    private static readonly DocumentSize Size = new(Twip.FromCentimeters(4), Twip.FromCentimeters(3));

    private static FolioScene Scene(Func<string, ObjectAnchor> anchor)
    {
        var scene = new FolioScene();
        scene.Type("Primo paragrafo");
        scene.Press(Key.Enter);
        scene.Type("Secondo paragrafo");
        var first = scene.Document.Body.Blocks.OfType<ParagraphBlock>().First();
        var image = new RasterImageObject { Id = "img", Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = Size };
        var item = new AnchoredObject("o1", anchor(first.Id), new EmbeddedContent(image), Size);
        scene.Editor.Insert((documents, _) => documents.InsertObject(scene.Document, scene.History, item, new DocumentPosition(first.Id, 0)), "test");
        scene.Editor.SelectObject("o1");
        return scene;
    }

    private static IFolioObjectMover Mover(FolioScene scene) => scene.Services.GetRequiredService<IFolioObjectMover>();

    private static AnchoredObject Item(FolioScene scene) => scene.Document.Objects.Find("o1")!;

    [Fact]
    public Task NelDocumentoProtettoGliOggettiNonSiSpostano() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(_ => new PageAnchor(1));
        var before = Item(scene).Placement;
        scene.Document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(PulseOffice.Documents.Protection.DocumentRestriction.Comments, null);

        Assert.False(Mover(scene).Move(scene.Editor, "o1", new ObjectDrop(0, 120, 300, 130, 310)));
        Assert.False(Mover(scene).Nudge(scene.Editor, "o1", 10, 10));
        Assert.False(Mover(scene).Reanchor(scene.Editor, "o1", ObjectAnchorKind.Paragraph));
        Assert.False(scene.Editor.RotateSelectedObject(90));
        Assert.False(scene.Editor.DeleteSelectedObject());
        Assert.NotNull(scene.Document.Objects.Find("o1"));

        Assert.Equal(before, Item(scene).Placement);
        Assert.Equal(new PageAnchor(1), Item(scene).Anchor);
    }, CancellationToken.None);

    [Fact]
    public Task SpostareUnOggettoDiPaginaAggiornaLaPosizioneInUnSoloAnnullamento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(_ => new PageAnchor(1));
        Assert.True(Mover(scene).Move(scene.Editor, "o1", new ObjectDrop(0, 120, 300, 130, 310)));

        Assert.Equal(new PageAnchor(1), Item(scene).Anchor);
        Assert.Equal(120, Item(scene).Placement.X.ToPoints(), 0);
        Assert.Equal(300, Item(scene).Placement.Y.ToPoints(), 0);
        scene.History.Undo();
        Assert.Equal(0, Item(scene).Placement.X.ToPoints(), 0);
        Assert.Equal(0, Item(scene).Placement.Y.ToPoints(), 0);
    }, CancellationToken.None);

    [Fact]
    public Task SpostareUnOggettoDiParagrafoLoRiancoraAlParagrafoSottoIlPuntatore() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(id => new ParagraphAnchor(id));
        var second = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        var caret = scene.Editor.Services.Queries.Caret(scene.Editor.Layout, new DocumentPosition(second.Id, 3))!.Value;

        Assert.True(Mover(scene).Move(scene.Editor, "o1", new ObjectDrop(caret.Page, 200, caret.Y + 10, caret.X, caret.Y + caret.Height / 2)));

        Assert.Equal(new ParagraphAnchor(second.Id), Item(scene).Anchor);
        Assert.Equal(VerticalRelation.Paragraph, Item(scene).Placement.Vertical);
        Assert.Equal(10, Item(scene).Placement.Y.ToPoints(), 0);
    }, CancellationToken.None);

    [Fact]
    public Task SpostareUnOggettoComeCarattereSpostaIlSegnoNelTesto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(id => new AsCharacterAnchor(id));
        var second = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        var caret = scene.Editor.Services.Queries.Caret(scene.Editor.Layout, new DocumentPosition(second.Id, 7))!.Value;

        Assert.True(Mover(scene).Move(scene.Editor, "o1", new ObjectDrop(caret.Page, caret.X, caret.Y, caret.X, caret.Y + caret.Height / 2)));

        Assert.Equal(new AsCharacterAnchor(second.Id), Item(scene).Anchor);
        Assert.Contains(second.Marks, mark => mark is ObjectMark { ObjectId: "o1" });
        Assert.DoesNotContain(scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().Marks, mark => mark is ObjectMark);
        scene.History.Undo();
        Assert.Contains(scene.Document.Body.Blocks.OfType<ParagraphBlock>().First().Marks, mark => mark is ObjectMark);
    }, CancellationToken.None);

    [Fact]
    public Task LeFrecceSpostanoDiUnPunto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(_ => new PageAnchor(1));

        scene.Press(Key.Right);
        scene.Press(Key.Down, RawInputModifiers.Shift);

        Assert.Equal(1, Item(scene).Placement.X.ToPoints(), 1);
        Assert.Equal(10, Item(scene).Placement.Y.ToPoints(), 1);
    }, CancellationToken.None);

    [Fact]
    public Task LaRotazioneArrivaAlLayoutESiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(id => new ParagraphAnchor(id));

        Assert.True(scene.Editor.RotateSelectedObject(390));

        Assert.Equal(30, Item(scene).RotationDegrees, 3);
        var box = scene.Services.GetRequiredService<IFolioObjectHits>().Find(scene.Editor.Layout, "o1", out _)!;
        Assert.Equal(30, box.RotationDegrees, 3);
        Assert.True(box.RotatedBounds.Width > box.Bounds.Width);
        scene.History.Undo();
        Assert.Equal(0, Item(scene).RotationDegrees, 3);
    }, CancellationToken.None);

    [Fact]
    public Task CopiaTagliaEIncollaUnOggetto() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = Scene(id => new AsCharacterAnchor(id));

        await scene.View.CopyAsync();
        scene.Editor.PlaceCaret(new DocumentPosition(scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last().Id, 3), false);
        await scene.View.PasteAsync();
        Assert.Equal(2, scene.Document.Objects.All.Count);
        Assert.Equal(2, scene.Document.Body.Blocks.OfType<ParagraphBlock>().Count());

        scene.Editor.SelectObject("o1");
        await scene.View.CutAsync();
        Assert.Single(scene.Document.Objects.All);
        Assert.Null(scene.Document.Objects.Find("o1"));
    }, CancellationToken.None);
}
