using Avalonia.Input;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioEditorProbeTests
{
    private static string Text(FolioScene scene) => string.Join("|", scene.Paragraphs);

    private static TableBlock AddTable(FolioScene scene)
    {
        scene.Editor.Insert((editor, at) => editor.InsertTable(scene.Document, scene.History, at, 2, 2), "table");
        return scene.Document.Body.Blocks.OfType<TableBlock>().Single();
    }

    [Fact]
    public Task SelezionaTuttoEScriviSostituisceAncheLeTabelle() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("prima");
        AddTable(scene);
        scene.Editor.SelectAll();
        scene.Type("x");
        Assert.Empty(scene.Document.Body.Blocks.OfType<TableBlock>());
        Assert.Equal("x", Text(scene));
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Single(scene.Document.Body.Blocks.OfType<TableBlock>());
    }, CancellationToken.None);

    [Fact]
    public Task IndietroDopoUnaTabellaNonLaRompe() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("prima");
        var table = AddTable(scene);
        var after = scene.Document.Body.Blocks.SkipWhile(block => block != table).Skip(1).OfType<ParagraphBlock>().FirstOrDefault();
        Assert.True(after is not null, string.Join(",", scene.Document.Body.Blocks.Select(block => block.GetType().Name)));
        scene.Editor.Select(new DocumentPosition(after.Id, 0), new DocumentPosition(after.Id, 0));
        scene.Press(Key.Back);
        var still = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(2, still.Rows.Count);
        Assert.All(still.Rows, row => Assert.Equal(2, row.Cells.Count));
    }, CancellationToken.None);

    [Fact]
    public Task UnaTabellaInFondoAlDocumentoLasciaUnParagrafoDopoESiAnnullaInUnColpo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("prima");
        AddTable(scene);
        Assert.IsType<ParagraphBlock>(scene.Document.Body.Blocks[^1]);
        scene.History.Undo();
        Assert.Equal("prima", Text(scene));
        Assert.Single(scene.Document.Body.Blocks);
    }, CancellationToken.None);

    [Fact]
    public Task CancPrimaDiUnaTabellaNonLaRompe() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("prima");
        var table = AddTable(scene);
        var before = scene.Document.Body.Blocks.TakeWhile(block => block != table).OfType<ParagraphBlock>().Last();
        scene.Editor.Select(new DocumentPosition(before.Id, before.Text.Length), new DocumentPosition(before.Id, before.Text.Length));
        scene.Press(Key.Delete);
        var still = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(2, still.Rows.Count);
        Assert.All(still.Rows, row => Assert.Equal(2, row.Cells.Count));
    }, CancellationToken.None);

    [Fact]
    public Task TagliaEIncollaFraParagrafiSiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno"); scene.Press(Key.Enter); scene.Type("due"); scene.Press(Key.Enter); scene.Type("tre");
        var ids = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Select(block => block.Id).ToList();
        scene.Editor.Select(new DocumentPosition(ids[0], 1), new DocumentPosition(ids[1], 2));
        var cut = scene.Editor.Cut();
        Assert.NotNull(cut);
        Assert.Equal("ue|tre", Text(scene));
        var last = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last();
        scene.Editor.Select(new DocumentPosition(last.Id, 3), new DocumentPosition(last.Id, 3));
        Assert.True(scene.Editor.Paste(cut!));
        Assert.Equal("ue|treno|du", Text(scene));
        scene.History.Undo();
        scene.History.Undo();
        Assert.Equal("uno|due|tre", Text(scene));
    }, CancellationToken.None);

    [Fact]
    public Task IndietroSuUnCampoLoTogliePerIntero() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("Pag 12").WithRuns([new TextRun("Pag "), new TextRun("12") { Field = TextFieldKind.PageNumber }])),
        ]));
        scene.Editor.Select(new DocumentPosition("p1", 6), new DocumentPosition("p1", 6));
        scene.Press(Key.Back);
        Assert.Equal("Pag ", Text(scene));
        Assert.DoesNotContain(scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field != TextFieldKind.None);
    }, CancellationToken.None);

    [Fact]
    public Task RegistrandoIndietroSulProprioInserimentoLoAccorcia() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("base ");
        scene.Document.Changes.IsRecording = true;
        scene.Type("nuovo");
        for (var i = 0; i < 5; i++) scene.Press(Key.Back);
        Assert.Equal("base ", Text(scene));
        Assert.Empty(scene.Document.Changes.All);
    }, CancellationToken.None);

    [Fact]
    public Task RegistrandoUnaCancellazioneIlTestoResta() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("abc def");
        scene.Document.Changes.IsRecording = true;
        scene.Press(Key.Back); scene.Press(Key.Back);
        Assert.Equal("abc d", Text(scene));
        Assert.Single(scene.Document.Changes.All);
        scene.Type("X");
        Assert.Contains("X", Text(scene));
    }, CancellationToken.None);
}
