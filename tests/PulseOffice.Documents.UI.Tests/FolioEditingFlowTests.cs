using Avalonia.Input;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioEditingFlowTests
{
    private static string Text(FolioScene scene) => string.Join("|", scene.Paragraphs);

    [Fact]
    public Task EnterInMezzoEAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("abcdef");
        scene.Press(Key.Left); scene.Press(Key.Left); scene.Press(Key.Left);
        scene.Press(Key.Enter);
        Assert.Equal("abc|def", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal("abcdef", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Assert.Equal("abc|def", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task ScrivereSuSelezioneDiDueParagrafiEAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno"); scene.Press(Key.Enter); scene.Type("due");
        scene.Press(Key.Left);
        scene.Press(Key.Up, RawInputModifiers.Shift);
        scene.Type("X");
        Assert.Equal("unXe", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal("uno|due", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task MaiuscSinistraAttraversaIlParagrafo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno"); scene.Press(Key.Enter); scene.Type("d");
        scene.Press(Key.Left, RawInputModifiers.Shift);
        scene.Press(Key.Left, RawInputModifiers.Shift);
        scene.Press(Key.Back);
        Assert.Equal("uno", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task TabNellUltimaCellaAggiungeUnaRiga() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Editor.Insert((editor, at) => editor.InsertTable(scene.Document, scene.History, at, 1, 2), "table");
        var first = (ParagraphBlock)scene.Document.Body.Blocks.OfType<TableBlock>().Single().Rows[0].Cells[0].Content.Blocks[0];
        scene.Editor.Select(new DocumentPosition(first.Id, 0), new DocumentPosition(first.Id, 0));
        scene.Press(Key.Tab);
        scene.Press(Key.Tab);
        Assert.Equal(2, scene.Document.Body.Blocks.OfType<TableBlock>().Single().Rows.Count);
    }, CancellationToken.None);

    [Fact]
    public Task TabNellUltimaCellaNonAggiungeRigheNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Editor.Insert((editor, at) => editor.InsertTable(scene.Document, scene.History, at, 1, 2), "table");
        var first = (ParagraphBlock)scene.Document.Body.Blocks.OfType<TableBlock>().Single().Rows[0].Cells[0].Content.Blocks[0];
        scene.Editor.Select(new DocumentPosition(first.Id, 0), new DocumentPosition(first.Id, 0));
        scene.Document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(PulseOffice.Documents.Protection.DocumentRestriction.Comments, null);
        scene.Press(Key.Tab);
        scene.Press(Key.Tab);
        Assert.Single(scene.Document.Body.Blocks.OfType<TableBlock>().Single().Rows);
    }, CancellationToken.None);

    [Fact]
    public Task CancAFineParagrafoUnisce() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno"); scene.Press(Key.Enter); scene.Type("due");
        scene.Press(Key.Up); scene.Press(Key.End);
        scene.Press(Key.Delete);
        Assert.Equal("unodue", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal("uno|due", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task DoppioClicSelezionaParolaConApostrofo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("dell'acqua fresca");
        scene.Editor.SelectWord(scene.Editor.Caret with { Offset = 2 });
        Assert.Equal("dell'acqua", scene.Editor.SelectedText());
    }, CancellationToken.None);
}
