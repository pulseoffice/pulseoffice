using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioRichClipboardTests
{
    [Fact]
    public Task CopiareMetteNegliAppuntiHtmlERtfConLaFormattazione() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        scene.Type("Grassetto");
        scene.Editor.SelectAll();
        scene.Editor.Toggle(CharacterToggle.Bold);

        await scene.View.CopyAsync();

        var rich = scene.Clipboard.Rich;
        Assert.Contains("Grassetto", rich.Html, StringComparison.Ordinal);
        Assert.Matches("<(b|strong)[ >]|font-weight: ?(bold|700)", rich.Html!);
        Assert.Contains("\\b", rich.Rtf, StringComparison.Ordinal);
        Assert.Contains("Grassetto", rich.Rtf, StringComparison.Ordinal);
    });

    [Fact]
    public Task IncollareRtfDiUnAltraAppConservaIlGrassetto() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        await scene.Clipboard.SetAsync(new RichClipboardContent
        {
            Text = "Esterno",
            Rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Helvetica;}}\f0\fs24 {\b Esterno}\par}",
        });

        await scene.View.PasteAsync();
        Dispatcher.UIThread.RunJobs();

        scene.Editor.SelectAll();
        Assert.Contains("Esterno", scene.Editor.SelectedText(), StringComparison.Ordinal);
        Assert.True(scene.Editor.IsOn(CharacterToggle.Bold));
    });

    [Fact]
    public Task IncollareHtmlDiUnBrowserConservaIlCorsivo() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        await scene.Clipboard.SetAsync(new RichClipboardContent
        {
            Text = "Dal web",
            Html = "<html><body><p><i>Dal web</i></p></body></html>",
        });

        await scene.View.PasteAsync();
        Dispatcher.UIThread.RunJobs();

        scene.Editor.SelectAll();
        Assert.Contains("Dal web", scene.Editor.SelectedText(), StringComparison.Ordinal);
        Assert.True(scene.Editor.IsOn(CharacterToggle.Italic));
    });
}
