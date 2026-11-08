using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class DeepAuditRoundTwentyTwoPasteTests
{
    private static async Task Paste(FolioScene scene, string text)
    {
        await scene.Clipboard.SetAsync(new RichClipboardContent { Text = text });
        await scene.View.PasteAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task IlTestoSempliceIncollatoNonDiventaUnElencoAutomatico() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();

        await Paste(scene, "- uno\n- due\n* tre");

        Assert.Equal(["- uno", "- due", "* tre"], scene.Paragraphs);
        Assert.All(scene.Document.Body.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Null(paragraph.Content.ListStyleName));
    }, CancellationToken.None);

    [Fact]
    public Task IlTestoSempliceConRigheVuoteInUnElencoNonChiudeLElenco() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        scene.Type("- voce");
        scene.Press(Avalonia.Input.Key.Enter);
        var list = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last().Content.ListStyleName;
        Assert.NotNull(list);

        await Paste(scene, "a\n\nb");

        var paragraphs = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(["voce", "a", "", "b"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.All(paragraphs, paragraph => Assert.Equal(list, paragraph.Content.ListStyleName));
    }, CancellationToken.None);

    [Fact]
    public Task IncollareTestoSempliceSiAnnullaInUnSoloPasso() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        scene.Type("x");

        await Paste(scene, "uno\ndue\ntre");
        Assert.Equal(["xuno", "due", "tre"], scene.Paragraphs);

        scene.History.Undo();
        Assert.Equal(["x"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task MoltissimeRigheDiTestoSempliceSiIncollanoInFrettaInUnSoloPasso() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        var text = string.Join('\n', Enumerable.Range(0, 3000).Select(index => "riga " + index));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await Paste(scene, text);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), clock.Elapsed.ToString());
        Assert.Equal(3000, scene.Paragraphs.Count);
        scene.History.Undo();
        Assert.Equal([""], scene.Paragraphs);
    }, CancellationToken.None);

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(Brushes.Teal, new Rect(0, 0, width, height));
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    [Fact]
    public Task UnImmagineAltissimaIncollataStaNellaPagina() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();

        Assert.True(scene.Services.GetRequiredService<IFolioImages>().Insert(scene.Editor, Png(200, 9000), "image/png", "lunga.png"));

        var size = scene.Document.Objects.All[0].Size;
        Assert.True(size.Height.Value <= Twip.FromCentimeters(24).Value, size.ToString());
        Assert.True(size.Width.Value > 0);
        Assert.InRange((double)size.Height.Value / size.Width.Value, 44, 46);
    }, CancellationToken.None);
}
