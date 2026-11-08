using Avalonia.Input;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioTypingAssistTests
{
    private static IReadOnlyList<ParagraphBlock> Blocks(FolioScene scene) => [.. scene.Document.Body.Blocks.OfType<ParagraphBlock>()];

    [Fact]
    public Task UnTrattinoEUnoSpazioDiventanoUnElencoPuntato() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("- primo");
        scene.Press(Key.Enter);
        scene.Type("secondo");

        Assert.Equal(["primo", "secondo"], scene.Paragraphs);
        Assert.All(Blocks(scene), block => Assert.Equal(DefaultStyleNames.List1, block.Content.ListStyleName));

        scene.History.Undo();
        scene.History.Undo();

        Assert.Equal("- primo", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task InvioSuUnaVoceVuotaChiudeLElenco() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("- primo");
        scene.Press(Key.Enter);
        scene.Press(Key.Enter);
        scene.Type("fuori");

        Assert.Equal(["primo", "fuori"], scene.Paragraphs);
        Assert.Null(Blocks(scene)[1].Content.ListStyleName);
        return Task.CompletedTask;
    });

    [Fact]
    public Task UnNumeroConIlPuntoDiventaUnElencoNumeratoDalSuoInizio() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("3. terzo");
        scene.Press(Key.Enter);

        var first = Blocks(scene)[0];
        Assert.Equal("terzo", first.Text);
        Assert.Equal(DefaultStyleNames.Numbering123, first.Content.ListStyleName);
        Assert.Equal(3, first.Content.ListRestart);
        return Task.CompletedTask;
    });

    [Fact]
    public Task TreTrattiniDiventanoUnBordoSottoIlParagrafo() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("===");
        scene.Press(Key.Enter);
        scene.Type("dopo");

        var blocks = Blocks(scene);
        Assert.Equal(string.Empty, blocks[0].Text);
        Assert.Equal(BorderLineStyle.Double, blocks[0].Content.Borders?.Bottom?.Style);
        Assert.Null(blocks[1].Content.Borders?.Bottom);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LeParoleLungheSiCompletanoEInvioAccetta() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock(document.NextId("p"), "Un risultato straordinario ")]));
        scene.Editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(Blocks(scene)[0].Id, 27), new PulseOffice.Documents.Editing.DocumentPosition(Blocks(scene)[0].Id, 27));
        scene.Type("s");
        scene.Type("t");
        scene.Type("r");

        Assert.Equal("Un risultato straordinario straordinario", scene.Paragraphs[0]);
        Assert.True(scene.Editor.HasSelection);

        scene.Press(Key.Enter);

        Assert.Single(scene.Paragraphs);
        Assert.False(scene.Editor.HasSelection);
        Assert.Equal(40, scene.Editor.Caret.Offset);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ContinuareAScrivereScartaIlSuggerimento() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock(document.NextId("p"), "straordinario ")]));
        scene.Editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(Blocks(scene)[0].Id, 14), new PulseOffice.Documents.Editing.DocumentPosition(Blocks(scene)[0].Id, 14));
        scene.Type("s");
        scene.Type("t");
        scene.Type("r");
        scene.Type("x");

        Assert.Equal("straordinario strx", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });
}
