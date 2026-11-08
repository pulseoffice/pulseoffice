using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioGroupLinePaintTests
{
    [Theory]
    [InlineData(ObjectAnchorKind.AsCharacter)]
    [InlineData(ObjectAnchorKind.Paragraph)]
    public Task IConnettoriDentroUnGruppoSiDisegnanoSullaPagina(ObjectAnchorKind anchor) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var line = new ShapeObject
        {
            Id = "l",
            Shape = ShapeKind.Line,
            Style = new ShapeStyle { Fill = ShapeFill.None, Line = new ShapeLine { ColorHex = "#000000", Width = Twip.FromPoints(2), EndArrow = ArrowEnd.Arrow } },
        };
        var box = new ShapeObject { Id = "r", Shape = ShapeKind.Rectangle, Style = new ShapeStyle { Fill = new ShapeFill { ColorHex = "#FFFF00" }, Line = new ShapeLine { ColorHex = "#000000" } } };
        var group = new ShapeGroupObject { Id = "g", Children = [new GroupChild(box, 0, 0, 0.4, 1), new GroupChild(line with { FlipVertical = true }, 0.5, 0.25, 0.4, 0.5) { RotationDegrees = 180 }] };
        Assert.True(scene.Services.GetRequiredService<IFolioObjectEditing>().Insert(scene.Editor, new ObjectInsertion(group,
            new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4)), anchor, WrapMode.Parallel)));
        Dispatcher.UIThread.RunJobs();
        var page = scene.Editor.Layout.Pages[0];
        var item = page.Objects.Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)page.Width, (int)page.Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, page.Width, page.Height));
            scene.Services.GetRequiredService<IPageRenderer>().Paint(scene.Services.GetRequiredService<IPageSurfaceFactory>().Screen(context), page,
                id => scene.Document.Objects.Find(id)?.Content is EmbeddedContent embedded ? embedded.Value : null);
        }
        var pixels = scene.Services.GetRequiredService<IBitmapPixels>().Read(bitmap);
        var x = (int)(item.Bounds.X + item.Bounds.Width * 0.7);
        var y = (int)(item.Bounds.Y + item.Bounds.Height * 0.5);
        var dark = Enumerable.Range(-2, 5).SelectMany(dx => Enumerable.Range(-2, 5).Select(dy => pixels.Pixels[((y + dy) * pixels.Width + x + dx) * 4])).Min();
        Assert.True(dark < 128, $"line pixel {dark} at {x},{y} in {item.Bounds}");
    }, CancellationToken.None);

    [Fact]
    public Task LaLineaConLaFrecciaDisegnaLaPunta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var line = new ShapeObject
        {
            Id = "l",
            Shape = ShapeKind.Line,
            Style = new ShapeStyle { Fill = ShapeFill.None, Line = new ShapeLine { ColorHex = "#000000", Width = Twip.FromPoints(2), EndArrow = ArrowEnd.Arrow } },
        };
        using var bitmap = new RenderTargetBitmap(new PixelSize(100, 100), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, 100, 100));
            scene.Services.GetRequiredService<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>().Render(context, line, new Rect(10, 10, 80, 80));
        }
        var pixels = scene.Services.GetRequiredService<IBitmapPixels>().Read(bitmap);
        Assert.True(pixels.Pixels[(84 * pixels.Width + 87) * 4] < 128);
        Assert.True(pixels.Pixels[(87 * pixels.Width + 84) * 4] < 128);
    }, CancellationToken.None);
}
