using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;
using LayoutRect = PulseOffice.Documents.Layout.Tree.LayoutRect;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioBulletsAndObjectLinksTests
{
    private readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public Task APictureBulletIsPaintedAsAnImageInsteadOfItsCharacter() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Document.Styles.List.Add(new ListStyle("Pictured", [new ListLevel(NumberFormat.Bullet) { Bullet = "§", Hanging = 24, Picture = new ListBulletPicture(_png, "image/png", 10, 8) }]));
        scene.Type("Voce");
        scene.Editor.Services.Editor.SetList(scene.Document, scene.History, scene.Editor.ParagraphRange(), "Pictured", 0);
        scene.Editor.Change(() => { });
        Dispatcher.UIThread.RunJobs();
        var surface = new RecordingSurface();

        scene.Services.GetRequiredService<IPageRenderer>().Paint(surface, scene.Editor.Layout.Pages[0]);

        var (content, bounds) = Assert.Single(surface.Objects);
        var picture = Assert.IsType<RasterImageObject>(content);
        Assert.Equal(_png, picture.Data.ToArray());
        Assert.Equal(10, bounds.Width, 3);
        Assert.Equal(8, bounds.Height, 3);
        Assert.DoesNotContain("§", surface.Texts);
        Assert.Contains("Voce", string.Concat(surface.Texts));
    }, CancellationToken.None);

    [Fact]
    public Task AnObjectHyperlinkIsSetAndRemovedAsUndoableSteps() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle });
        var links = scene.Services.GetRequiredService<IFolioObjectLinks>();
        var target = Assert.IsType<FolioObjectLinkTarget>(links.Target(scene.Editor));

        Assert.True(links.Set(scene.Editor, target, "https://example.com/"));
        Assert.Equal("https://example.com/", scene.Document.Objects.All.Single().Hyperlink);
        Assert.True(links.Set(scene.Editor, target, null));
        Assert.Null(scene.Document.Objects.All.Single().Hyperlink);

        scene.History.Undo();
        Assert.Equal("https://example.com/", scene.Document.Objects.All.Single().Hyperlink);
        scene.History.Undo();
        Assert.Null(scene.Document.Objects.All.Single().Hyperlink);
    }, CancellationToken.None);

    [Fact]
    public Task TheGroupMemberUnderThePointerGetsItsOwnLinkAndIsFollowed() => FolioScene.Session.Dispatch(() =>
    {
        var group = new ShapeGroupObject
        {
            Id = "g",
            Children =
            [
                new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 1),
                new GroupChild(new ShapeObject { Id = "b", Shape = ShapeKind.Ellipse }, 0.5, 0, 0.5, 1) { Hyperlink = "https://right.example/" },
            ],
        };
        var scene = Scene(group);
        var links = scene.Services.GetRequiredService<IFolioObjectLinks>();
        var box = scene.Editor.Layout.Pages[0].Objects.Single();

        var left = links.Point(scene.Editor, box, box.Bounds.X + box.Bounds.Width * 0.25, box.Bounds.Y + box.Bounds.Height / 2)!;
        var right = links.Point(scene.Editor, box, box.Bounds.X + box.Bounds.Width * 0.75, box.Bounds.Y + box.Bounds.Height / 2)!;

        Assert.Equal([0], left.Member!.Indices);
        Assert.Null(links.EffectiveLink(scene.Editor, left));
        Assert.True(links.Follow(scene.Editor, right));
        Assert.Equal(["https://right.example/"], scene.Opener.Opened);

        Assert.True(links.Set(scene.Editor, left, "https://left.example/"));
        var children = ((ShapeGroupObject)((EmbeddedContent)scene.Document.Objects.All.Single().Content).Value).Children;
        Assert.Equal("https://left.example/", children[0].Hyperlink);
        Assert.Equal("https://right.example/", children[1].Hyperlink);
        Assert.Null(scene.Document.Objects.All.Single().Hyperlink);
        scene.History.Undo();
        Assert.Null(((ShapeGroupObject)((EmbeddedContent)scene.Document.Objects.All.Single().Content).Value).Children[0].Hyperlink);
    }, CancellationToken.None);

    [Fact]
    public Task AnUnsafeObjectLinkIsNotOpened() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle });
        var links = scene.Services.GetRequiredService<IFolioObjectLinks>();
        var target = links.Target(scene.Editor)!;
        links.Set(scene.Editor, target, "javascript:alert(1)");

        Assert.False(links.Follow(scene.Editor, target));
        Assert.Empty(scene.Opener.Opened);
    }, CancellationToken.None);

    private FolioScene Scene(EmbeddedObject content)
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        Assert.True(objects.Insert(scene.Editor, new ObjectInsertion(content,
            new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)), ObjectAnchorKind.Paragraph, WrapMode.Parallel)));
        Dispatcher.UIThread.RunJobs();
        scene.Editor.SelectObject(scene.Document.Objects.All.Single().Id);
        return scene;
    }

    private sealed class RecordingSurface : IPageSurface
    {
        public List<string> Texts { get; } = [];

        public List<(EmbeddedObject Content, LayoutRect Bounds)> Objects { get; } = [];

        public void Fill(LayoutRect rect, uint argb)
        {
        }

        public void Line(double x1, double y1, double x2, double y2, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Frame(LayoutRect rect, uint argb, double thickness, PageStroke stroke = PageStroke.Solid)
        {
        }

        public void Glyphs(LayoutGlyphRun run, double x, double baseline, uint argb) => Texts.Add(run.Text);

        public void RotatedGlyphs(LayoutGlyphRun run, double x, double baseline, uint argb, double degrees) => Texts.Add(run.Text);

        public void Embedded(EmbeddedObject content, LayoutRect bounds, ObjectTransform transform) => Objects.Add((content, bounds));
    }
}
