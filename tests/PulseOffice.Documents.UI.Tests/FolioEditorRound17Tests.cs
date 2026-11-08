using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioEditorRound17Tests
{
    private static FolioScene Sandwich() => new(document => document.Body.Replace(
    [
        new ParagraphBlock("primo", "primo"),
        new SectionBlock("s1", "Chiusa", new BlockContainer([new ParagraphBlock("chiuso", "chiuso")])) { Protected = true },
        new ParagraphBlock("ultimo", "ultimo"),
    ]));

    private static AnchoredObject Picture(string id, ObjectAnchor anchor)
    {
        var size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3));
        var image = new RasterImageObject { Id = "img" + id, Data = new byte[] { 1 }, MediaType = "image/png", NaturalSize = size };
        return new AnchoredObject(id, anchor, new EmbeddedContent(image), size);
    }

    private static FolioScene WithProtectedPicture()
    {
        var scene = Sandwich();
        scene.Document.Objects.Add(Picture("o1", new ParagraphAnchor("chiuso")));
        scene.Editor.Reflow();
        scene.Editor.SelectObject("o1");
        return scene;
    }

    [Fact]
    public Task ReplacingWithALineBreakPutsTheCaretAfterTheLastLine() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("uno", "alfa beta")]));

        Assert.True(scene.Editor.Replace(DocumentRange.In("uno", 0, 4), "x\nyz"));

        var paragraphs = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["x", "yz beta"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal(new DocumentPosition(paragraphs[1].Id, 2), scene.Editor.Caret);
    }, CancellationToken.None);

    [Fact]
    public Task AnObjectAnchoredInAProtectedSectionDoesNotMove() => FolioScene.Session.Dispatch(() =>
    {
        var scene = WithProtectedPicture();
        var mover = scene.Services.GetRequiredService<IFolioObjectMover>();
        var before = scene.Document.Objects.Find("o1")!.Placement;

        Assert.False(mover.Nudge(scene.Editor, "o1", 10, 10));
        Assert.False(mover.Reanchor(scene.Editor, "o1", ObjectAnchorKind.Page));

        Assert.Equal(before, scene.Document.Objects.Find("o1")!.Placement);
        Assert.IsType<ParagraphAnchor>(scene.Document.Objects.Find("o1")!.Anchor);
        Assert.False(scene.History.CanUndo);
    }, CancellationToken.None);

    [Fact]
    public Task AnObjectAnchoredInAProtectedSectionKeepsItsOrderAndCaption() => FolioScene.Session.Dispatch(() =>
    {
        var scene = WithProtectedPicture();
        scene.Document.Objects.Add(Picture("o2", new ParagraphAnchor("primo")));
        var editing = scene.Services.GetRequiredService<IFolioObjectEditing>();

        Assert.False(editing.Order(scene.Editor, ObjectOrder.Front));

        Assert.False(scene.History.CanUndo);
    }, CancellationToken.None);

    [Fact]
    public Task ANonFiniteRotationIsRefused() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("uno", "uno")]));
        scene.Document.Objects.Add(Picture("o1", new ParagraphAnchor("uno")));
        scene.Editor.Reflow();
        scene.Editor.SelectObject("o1");
        var editing = scene.Services.GetRequiredService<IFolioObjectEditing>();

        Assert.False(editing.Rotate(scene.Editor, double.NaN, relative: true));
        Assert.False(editing.Rotate(scene.Editor, double.PositiveInfinity, relative: false));

        Assert.Equal(0, scene.Document.Objects.Find("o1")!.RotationDegrees);
    }, CancellationToken.None);
}
