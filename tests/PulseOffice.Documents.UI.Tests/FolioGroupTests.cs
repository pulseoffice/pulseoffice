using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioGroupTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static void Run(FolioScene scene, string id) =>
        new FolioShapeCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), new FolioShapeServices(
                scene.Services.GetRequiredService<IFolioObjectEditing>(), scene.Services.GetRequiredService<IObjectGrouping>(), scene.Services.GetRequiredService<IFolioObjectHits>(),
                null!, null!, null!, null!, null!, null!, null!))
            .GetCommands().Single(command => command.Id.Value == id).Execute(Context, CancellationToken.None).GetAwaiter().GetResult();

    private static string Shape(FolioScene scene, double x, double y)
    {
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        objects.Insert(scene.Editor, new ObjectInsertion(new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }, new DocumentSize(Twip.FromPoints(50), Twip.FromPoints(40)), ObjectAnchorKind.Paragraph, WrapMode.Through));
        var id = scene.Editor.SelectedObjectId!;
        objects.Change(scene.Editor, item =>
        {
            item.Anchor = new PageAnchor(1);
            item.Placement = new ObjectPlacement(Twip.FromPoints(x), Twip.FromPoints(y)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page };
        });
        return id;
    }

    [Fact]
    public Task DueFormeSiRaggruppanoESiSeparanoAlloStessoPosto() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var first = Shape(scene, 100, 100);
        var second = Shape(scene, 200, 180);
        scene.Editor.SelectObject(first);
        scene.Editor.ToggleObject(second);

        Run(scene, "folio.object.group");

        var group = Assert.Single(scene.Document.Objects.All);
        Assert.IsType<ShapeGroupObject>(((EmbeddedContent)group.Content).Value);
        Assert.Equal(150, group.Size.Width.ToPoints(), 0);
        Assert.Equal(100, group.Placement.X.ToPoints(), 0);

        Run(scene, "folio.object.ungroup");

        Assert.Equal(2, scene.Document.Objects.All.Count);
        Assert.Contains(scene.Document.Objects.All, item => Math.Abs(item.Placement.X.ToPoints() - 200) < 0.5 && Math.Abs(item.Placement.Y.ToPoints() - 180) < 0.5);
        Assert.Equal(2, scene.Editor.SelectedObjectIds.Count);

        scene.History.Undo();
        scene.History.Undo();
        Assert.Equal(2, scene.Document.Objects.All.Count);
        Assert.All(scene.Document.Objects.All, item => Assert.IsType<ShapeObject>(((EmbeddedContent)item.Content).Value));
        return Task.CompletedTask;
    });

    [Fact]
    public Task RaggruppareConIlCursoreNelTestoDiUnaFormaNonPerdeIlCursore() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var first = Shape(scene, 100, 100);
        var second = Shape(scene, 200, 180);
        var story = scene.Document.Objects.Find(first)!.Content.Story!;
        var inside = (PulseOffice.Documents.Blocks.ParagraphBlock)story[0];
        scene.Editor.PlaceCaret(new PulseOffice.Documents.Editing.DocumentPosition(inside.Id, 0), false);
        scene.Editor.SelectObject(first);
        Assert.NotEqual(inside.Id, scene.Editor.Caret.ParagraphId);
        scene.Editor.ToggleObject(second);
        var seen = new List<string>();
        scene.History.Changed += (_, _) => seen.Add(scene.Editor.EffectiveRun().Text + scene.Editor.EffectiveParagraph().Text);

        Run(scene, "folio.object.group");
        Assert.NotEmpty(seen);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(inside.Id, scene.Editor.Caret.ParagraphId);
        Assert.NotNull(scene.Editor.EffectiveParagraph());
        Run(scene, "folio.object.ungroup");
        Assert.NotNull(scene.Editor.EffectiveParagraph());
        return Task.CompletedTask;
    });
}
