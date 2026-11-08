using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioViewTests
{
    [Fact]
    public Task LaDigitazioneEntraNelDocumento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();

        scene.Type("Ciao");

        Assert.Equal(["Ciao"], scene.Paragraphs);
        Assert.Equal(4, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task InvioDivideIlParagrafoEBackspaceLoRiunisce() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Alfa");
        scene.Press(Key.Left);
        scene.Press(Key.Left);

        scene.Press(Key.Enter);
        Assert.Equal(["Al", "fa"], scene.Paragraphs);
        Assert.Equal(0, scene.Editor.Caret.Offset);

        scene.Press(Key.Back);
        Assert.Equal(["Alfa"], scene.Paragraphs);
        Assert.Equal(2, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task CancUniscePerIlVersoOpposto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno");
        scene.Press(Key.Enter);
        scene.Type("Due");
        scene.Press(Key.Up);
        scene.Press(Key.End);

        scene.Press(Key.Delete);

        Assert.Equal(["UnoDue"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task MaiuscFrecceSelezionanoELaDigitazioneSostituisce() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Buongiorno");
        scene.Press(Key.Left, RawInputModifiers.Shift);
        scene.Press(Key.Left, RawInputModifiers.Shift);
        scene.Press(Key.Left, RawInputModifiers.Shift);

        Assert.True(scene.Editor.HasSelection);
        scene.Type("x");

        Assert.Equal(["Buongiox"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task AnnullaERipristinaPassanoDallaCronologia() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("A");
        scene.Type("B");
        scene.Press(Key.Enter);

        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal(["AB"], scene.Paragraphs);
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal([""], scene.Paragraphs);
        Assert.Equal(0, scene.Editor.Caret.Offset);

        scene.Press(Key.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Assert.Equal(["AB"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task IlClicPortaIlCursoreEIlDoppioClicSelezionaLaParola() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno due tre");
        scene.Press(Key.Home);
        var canvas = scene.View.Canvas;
        var caret = canvas.CaretRectangle;
        var point = canvas.TranslatePoint(new Point(caret.X + 2, caret.Center.Y), scene.Window)!.Value;

        scene.Press(Key.End);
        scene.Window.MouseDown(point, MouseButton.Left);
        scene.Window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, scene.Editor.Caret.Offset);
        Assert.False(scene.Editor.HasSelection);

        scene.Editor.SelectWord(new DocumentPosition(scene.Editor.Caret.ParagraphId, 5));
        Assert.Equal("due", scene.Editor.SelectedText());
    }, CancellationToken.None);

    [Fact]
    public Task LaCorrezioneAutomaticaAgisceAlConfineDellaParola() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("teh");
        scene.Type(" ");

        Assert.Equal(["the "], scene.Paragraphs);
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal(["teh "], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task CopiaEIncollaConservanoIlTesto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Rosso");
        scene.Press(Key.A, RawInputModifiers.Meta);
        scene.Press(Key.C, RawInputModifiers.Meta);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Rosso", scene.Clipboard.Text);

        scene.Press(Key.End);
        scene.Press(Key.V, RawInputModifiers.Meta);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("RossoRosso", string.Concat(scene.Paragraphs));
    }, CancellationToken.None);

    [Fact]
    public Task InSolaLetturaNonSiScrive() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene { ReadOnly = true };
        scene.Type("x");
        scene.Press(Key.Enter);

        Assert.Equal([""], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task IlTestoLungoVaSuPiuPagineEIlCursoreLeAttraversa() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        for (var i = 0; i < 80; i++)
        {
            scene.Type("Riga di prova numero " + i);
            scene.Press(Key.Enter);
        }

        Assert.True(scene.Editor.Layout.Pages.Count >= 2);
        scene.Press(Key.Up, RawInputModifiers.Meta);
        Assert.Equal(0, scene.Editor.Caret.Offset);
        for (var i = 0; i < 79; i++) scene.Press(Key.Down);
        Assert.Equal(scene.Document.Body.Blocks[79].Id, scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);
}
