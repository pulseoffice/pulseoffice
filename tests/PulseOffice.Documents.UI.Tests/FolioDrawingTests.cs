using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Drawing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioDrawingTests
{
    private static Point On(FolioScene scene, double x, double y)
    {
        var slot = scene.View.Canvas.Slots[0];
        return scene.View.Canvas.TranslatePoint(new Point(slot.X + x * scene.View.Canvas.Scale, slot.Y + y * scene.View.Canvas.Scale), scene.Window)!.Value;
    }

    [Fact]
    public Task UnPoligonoDisegnatoDiventaUnOggettoSullaPagina() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Services.GetRequiredService<IFolioDrawingTools>().Choose(DrawingTool.Polygon, false);

        foreach (var (x, y) in new[] { (100.0, 100.0), (200.0, 120.0), (150.0, 200.0) })
        {
            scene.Window.MouseDown(On(scene, x, y), MouseButton.Left);
            scene.Window.MouseUp(On(scene, x, y), MouseButton.Left);
        }
        scene.Press(Key.Enter);
        Dispatcher.UIThread.RunJobs();

        var item = Assert.Single(scene.Document.Objects.All);
        Assert.IsType<FreeformObject>(((EmbeddedContent)item.Content).Value);
        Assert.Equal(new PageAnchor(1), item.Anchor);
        Assert.Equal(100, item.Placement.X.ToPoints(), 0);
        Assert.Equal(100, item.Size.Width.ToPoints(), 0);
        Assert.Equal(DrawingTool.None, scene.Services.GetRequiredService<IFolioDrawingTools>().Tool);

        scene.History.Undo();
        Assert.Empty(scene.Document.Objects.All);
        return Task.CompletedTask;
    });

    [Fact]
    public Task UnaCorniceSiDisegnaTrascinando() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Services.GetRequiredService<IFolioDrawingTools>().Choose(DrawingTool.Frame, false);

        scene.Window.MouseDown(On(scene, 120, 150), MouseButton.Left);
        scene.Window.MouseMove(On(scene, 320, 250), RawInputModifiers.LeftMouseButton);
        scene.Window.MouseUp(On(scene, 320, 250), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var item = Assert.Single(scene.Document.Objects.All);
        Assert.IsType<TextBoxContent>(item.Content);
        Assert.Equal(200, item.Size.Width.ToPoints(), 0);
        Assert.Equal(150, item.Placement.Y.ToPoints(), 0);
        return Task.CompletedTask;
    });
}
