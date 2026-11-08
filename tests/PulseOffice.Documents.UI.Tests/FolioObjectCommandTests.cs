using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioObjectCommandTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, FolioObjectCommands.ObjectSelection, DocumentState.DocumentOpen);

    private static IReadOnlyList<CommandDefinition> Commands(FolioScene scene) =>
        [.. new FolioObjectCommands(
            scene.Services.GetRequiredService<IActiveFolioContext>(),
            scene.Services.GetRequiredService<IFolioObjectEditing>(),
            scene.Services.GetRequiredService<IFolioObjectMover>(),
            null!,
            scene.Services.GetRequiredService<ICaptions>(),
            scene.Services.GetRequiredService<IFrameChains>(),
            scene.Services.GetRequiredService<IFolioObjectCycle>()).GetCommands()];

    private static void Run(FolioScene scene, string id)
    {
        Commands(scene).Single(command => command.Id.Value == id).Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Enabled(FolioScene scene, string id) =>
        Commands(scene).Single(command => command.Id.Value == id).IsEnabled(Context);

    private static bool Checked(FolioScene scene, string id) =>
        Commands(scene).Single(command => command.Id.Value == id).CheckedWhen?.Invoke(Context) == true;

    private static AnchoredObject Shape(FolioScene scene)
    {
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        Assert.True(objects.Insert(scene.Editor, new ObjectInsertion(new ShapeObject { Id = "s", Shape = ShapeKind.Ellipse }, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)), ObjectAnchorKind.Paragraph, WrapMode.Parallel)));
        Dispatcher.UIThread.RunJobs();
        return scene.Document.Objects.Find(scene.Editor.SelectedObjectId!)!;
    }

    [Fact]
    public Task ScorrimentoAncoraEOrdineSiCambianoESiAnnullano() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Un paragrafo con abbastanza testo per girare intorno alla forma");
        var item = Shape(scene);

        Assert.True(Checked(scene, "folio.object.wrap.parallel"));
        Run(scene, "folio.object.wrap.optimal");
        Assert.Equal(WrapMode.Optimal, item.Wrap);
        Run(scene, "folio.object.wrap.contour");
        Assert.True(item.WrapContour);
        Run(scene, "folio.object.anchor.page");
        Assert.IsType<PageAnchor>(item.Anchor);
        Assert.True(Checked(scene, "folio.object.anchor.page"));
        Run(scene, "folio.object.rotateRight");
        Assert.Equal(90, item.RotationDegrees, 3);
        Run(scene, "folio.object.flipHorizontal");
        Assert.True(((ShapeObject)((EmbeddedContent)scene.Document.Objects.Find(item.Id)!.Content).Value).FlipHorizontal);

        for (var i = 0; i < 5; i++) scene.History.Undo();
        Dispatcher.UIThread.RunJobs();
        var restored = scene.Document.Objects.Find(item.Id)!;
        Assert.Equal(WrapMode.Parallel, restored.Wrap);
        Assert.False(restored.WrapContour);
        Assert.IsType<ParagraphAnchor>(restored.Anchor);
        Assert.Equal(0, restored.RotationDegrees, 3);
    }, CancellationToken.None);

    [Fact]
    public Task LeImmaginiComeCarattereNonHannoScorrimento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        Shape(scene);

        Run(scene, "folio.object.anchor.asCharacter");

        Assert.False(Enabled(scene, "folio.object.wrap.parallel"));
        Assert.False(Enabled(scene, "folio.object.order.front"));
        Assert.True(Enabled(scene, "folio.object.rotateLeft"));
    }, CancellationToken.None);

    [Fact]
    public Task InSolaLetturaIComandiDegliOggettiSiSpengono() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        Shape(scene);
        scene.ReadOnly = true;

        Assert.False(Enabled(scene, "folio.object.wrap.optimal"));
        Assert.False(Enabled(scene, "folio.object.anchor.page"));
        Assert.False(Enabled(scene, "folio.object.properties"));
    }, CancellationToken.None);

    [Fact]
    public Task CornicedITestoSiInserisceESiCollega() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        objects.InsertTextFrame(scene.Editor, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)));
        var first = scene.Editor.SelectedObjectId!;
        objects.InsertTextFrame(scene.Editor, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)));
        var second = scene.Editor.SelectedObjectId!;
        scene.Editor.SelectObject(first);

        Assert.True(Enabled(scene, "folio.object.link"));
        Assert.True(objects.Link(scene.Editor, second));
        Assert.True(Enabled(scene, "folio.object.unlink"));
        Assert.NotNull(((TextBoxContent)scene.Document.Objects.Find(first)!.Content).NextFrameName);

        Run(scene, "folio.object.unlink");
        Assert.Null(((TextBoxContent)scene.Document.Objects.Find(first)!.Content).NextFrameName);
    }, CancellationToken.None);

    [Fact]
    public Task ScrivendoConUnaCorniceSelezionataSiScriveDentroLaCornice() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Corpo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        objects.InsertTextFrame(scene.Editor, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)));
        var frame = scene.Editor.SelectedObjectId!;

        Assert.True(scene.Editor.EnterFrame(frame, null));
        scene.Editor.Type("dentro");

        var box = (TextBoxContent)scene.Document.Objects.Find(frame)!.Content;
        Assert.Equal("dentro", ((PulseOffice.Documents.Blocks.ParagraphBlock)box.Content[0]).Text);
        Assert.Equal(["Corpo"], scene.Paragraphs);
        Assert.Null(scene.Editor.SelectedObjectId);
        Shape(scene);
        var shape = scene.Editor.SelectedObjectId!;
        Assert.True(scene.Editor.EnterFrame(shape, null));
        scene.Editor.Type("forma");

        var story = scene.Document.Objects.Find(shape)!.Content.Story!;
        Assert.Equal("forma", ((PulseOffice.Documents.Blocks.ParagraphBlock)story[0]).Text);
        Assert.Equal(["Corpo"], scene.Paragraphs);
    }, CancellationToken.None);
}
