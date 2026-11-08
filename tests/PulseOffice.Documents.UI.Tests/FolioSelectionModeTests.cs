using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioSelectionModeTests
{
    [Fact]
    public Task LaSelezioneABlocchiCopiaECancellaUnaColonnaDiTesto() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("abcdef");
        scene.Press(Key.Enter);
        scene.Type("ghijkl");
        var ids = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Id).ToList();
        scene.Editor.SelectBlock(
        [
            new DocumentRange(new DocumentPosition(ids[0], 1), new DocumentPosition(ids[0], 3)),
            new DocumentRange(new DocumentPosition(ids[1], 1), new DocumentPosition(ids[1], 3)),
        ]);

        Assert.Equal("bc\nhi", scene.Editor.SelectedText());
        Assert.True(scene.Editor.Backspace());
        Assert.Equal(["adef", "gjkl"], scene.Paragraphs);
        Assert.Empty(scene.Editor.BlockRanges);

        scene.History.Undo();
        Assert.Equal(["abcdef", "ghijkl"], scene.Paragraphs);
        return Task.CompletedTask;
    });

    [Fact]
    public Task IlCursoreDirettoAggiungeRigheVuoteERientraInUnSoloPasso() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Titolo");
        var first = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single();
        var cursor = scene.Services.GetRequiredService<IDirectCursor>();

        var position = cursor.Place(scene.Document, scene.History, new DirectCursorTarget(first.Id, 3, 72));

        var paragraphs = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(4, paragraphs.Count);
        Assert.Equal(paragraphs[^1].Id, position.ParagraphId);
        Assert.Equal(72, paragraphs[^1].Content.MarginLeft);
        Assert.Null(paragraphs[1].Content.MarginLeft);

        scene.History.Undo();
        Assert.Equal(["Titolo"], scene.Paragraphs);
        return Task.CompletedTask;
    });
}
