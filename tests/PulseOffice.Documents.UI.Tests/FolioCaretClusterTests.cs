using Avalonia.Input;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioCaretClusterTests
{
    private static string Text(FolioScene scene) => string.Join("|", scene.Paragraphs);

    [Fact]
    public Task IndietroCancellaUnEmojiConIlSuoModificatoreIntero() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("a👍🏽b");
        scene.Press(Key.Back);
        scene.Press(Key.Back);
        Assert.Equal("a", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task LaFrecciaSaltaUnaFamigliaDiEmojiUnitaDaZwjInUnPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("a👨‍👩‍👧b");
        scene.Press(Key.Left);
        scene.Press(Key.Left);
        Assert.Equal(1, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task CancCancellaUnaBandieraIntera() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("a🇮🇹b");
        scene.Press(Key.Home);
        scene.Press(Key.Right);
        scene.Press(Key.Delete);
        Assert.Equal("ab", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task LaFrecciaNonSiFermaFraLetteraEAccentoCombinato() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("éx");
        scene.Press(Key.Left);
        scene.Press(Key.Left);
        Assert.Equal(0, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task AltDestraVaAllInizioDellaParolaSeguente() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno due tre");
        scene.Press(Key.Home);
        scene.Press(Key.Right, RawInputModifiers.Alt);
        Assert.Equal(4, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task AltSinistraScavalcaLaPunteggiatura() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno, due");
        scene.Press(Key.Left, RawInputModifiers.Alt);
        Assert.Equal(5, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task AltDestraSiFermaAlCambioDiScritturaNelGiapponese() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("日本語のテキスト");
        scene.Press(Key.Home);
        scene.Press(Key.Right, RawInputModifiers.Alt);
        Assert.Equal(3, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task AnnullaERipetiDellaScrittura() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("ciao");
        scene.Type(" ");
        scene.Type("mondo");
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.NotEqual("", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        scene.Press(Key.Z, RawInputModifiers.Meta);
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal("", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Assert.NotEqual("", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task SelezionaTuttoCancellaEAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno");
        scene.Press(Key.Enter);
        scene.Type("due");
        scene.Press(Key.A, RawInputModifiers.Meta);
        scene.Press(Key.Back);
        Assert.Equal("", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal("uno|due", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task InizioEFineRigaInUnTestoEbraico() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("שלום");
        scene.Press(Key.Home);
        var start = scene.Editor.Caret.Offset;
        scene.Press(Key.End);
        Assert.NotEqual(start, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task MaiuscFineSelezionaFinoAFineRiga() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("abc def");
        scene.Press(Key.Home);
        scene.Press(Key.End, RawInputModifiers.Shift);
        scene.Type("X");
        Assert.Equal("X", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task PaginaGiuEPaginaSu() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        for (var i = 0; i < 80; i++) { scene.Type("riga " + i); scene.Press(Key.Enter); }
        scene.Press(Key.Up, RawInputModifiers.Meta);
        scene.Press(Key.PageDown);
        Assert.NotEqual(scene.Document.Body.Blocks[0].Id, scene.Editor.Caret.ParagraphId);
        scene.Press(Key.PageUp);
        Assert.Equal(scene.Document.Body.Blocks[0].Id, scene.Editor.Caret.ParagraphId);
    }, CancellationToken.None);
}
