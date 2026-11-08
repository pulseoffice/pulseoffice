using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioShapeAdjustTests
{
    private static Point On(FolioScene scene, double x, double y)
    {
        var slot = scene.View.Canvas.Slots[0];
        return scene.View.Canvas.TranslatePoint(new Point(slot.X + x * scene.View.Canvas.Scale, slot.Y + y * scene.View.Canvas.Scale), scene.Window)!.Value;
    }

    private static string Rounded(FolioScene scene, bool flipped)
    {
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        objects.Insert(scene.Editor, new ObjectInsertion(new ShapeObject { Id = "s", Shape = ShapeKind.RoundedRectangle }, new DocumentSize(Twip.FromPoints(200), Twip.FromPoints(100)), ObjectAnchorKind.Paragraph, WrapMode.Through));
        var id = scene.Editor.SelectedObjectId!;
        objects.Change(scene.Editor, item =>
        {
            item.Anchor = new PageAnchor(1);
            item.Placement = new ObjectPlacement(Twip.FromPoints(100), Twip.FromPoints(100)) { Horizontal = HorizontalRelation.Page, Vertical = VerticalRelation.Page };
            item.FlipHorizontal = flipped;
        });
        scene.Editor.SelectObject(id);
        Dispatcher.UIThread.RunJobs();
        return id;
    }

    private static double Adjustment(FolioScene scene, string id) =>
        Assert.IsType<PresetGeometry>(((ShapeObject)((EmbeddedContent)scene.Document.Objects.Find(id)!.Content).Value).Computed).Adjustments.Single().Value;

    private static void Drag(FolioScene scene, double fromX, double toX)
    {
        scene.Window.MouseDown(On(scene, fromX, 100), MouseButton.Left);
        scene.Window.MouseMove(On(scene, (fromX + toX) / 2, 100), RawInputModifiers.LeftMouseButton);
        scene.Window.MouseMove(On(scene, toX, 100), RawInputModifiers.LeftMouseButton);
        scene.Window.MouseUp(On(scene, toX, 100), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task La_maniglia_di_regolazione_segue_il_puntatore_e_si_annulla() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var id = Rounded(scene, flipped: false);

        Drag(scene, 100 + 16.667, 140);

        Assert.InRange(Adjustment(scene, id), 39500, 40500);
        scene.History.Undo();
        Assert.Null(((ShapeObject)((EmbeddedContent)scene.Document.Objects.Find(id)!.Content).Value).Computed);
        return Task.CompletedTask;
    });

    [Fact]
    public Task La_maniglia_di_una_forma_ribaltata_sta_sul_lato_specchiato() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var id = Rounded(scene, flipped: true);

        Drag(scene, 300 - 16.667, 260);

        Assert.InRange(Adjustment(scene, id), 39500, 40500);
        var item = scene.Document.Objects.Find(id)!;
        Assert.Equal(100, item.Placement.X.ToPoints(), 0);
        return Task.CompletedTask;
    });
}
