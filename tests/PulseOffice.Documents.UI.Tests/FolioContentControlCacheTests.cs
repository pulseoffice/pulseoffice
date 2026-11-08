using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioContentControlCacheTests
{
    [Fact]
    public Task IlCatalogoNonRipercorreIlDocumentoAParitaDiRevisione() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome: Ada");
        var catalog = scene.Services.GetRequiredService<IContentControlCatalog>();
        var editing = scene.Services.GetRequiredService<IContentControlEditing>();
        var paragraph = scene.Editor.Caret.ParagraphId;
        editing.Insert(scene.Document, scene.History, new DocumentRange(new DocumentPosition(paragraph, 6), new DocumentPosition(paragraph, 9)), new ContentControl { Kind = ContentControlKind.PlainText });

        var first = catalog.All(scene.Document);
        Assert.Same(first, catalog.All(scene.Document));
        Assert.Single(first);
    }, CancellationToken.None);

    [Fact]
    public Task AnnullaERipetiInvalidanoIlCatalogo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome: Ada");
        var catalog = scene.Services.GetRequiredService<IContentControlCatalog>();
        var editing = scene.Services.GetRequiredService<IContentControlEditing>();
        var paragraph = scene.Editor.Caret.ParagraphId;
        editing.Insert(scene.Document, scene.History, new DocumentRange(new DocumentPosition(paragraph, 6), new DocumentPosition(paragraph, 9)), new ContentControl { Kind = ContentControlKind.PlainText });
        Assert.Single(catalog.All(scene.Document));

        scene.History.Undo();
        Assert.Empty(catalog.All(scene.Document));
        Assert.Null(catalog.At(scene.Document, new DocumentPosition(paragraph, 7)));

        scene.History.Redo();
        Assert.Equal("Ada", scene.Paragraphs[0][6..9]);
        Assert.NotNull(catalog.At(scene.Document, new DocumentPosition(paragraph, 7)));
    }, CancellationToken.None);

    [Fact]
    public Task RiempireUnControlloSuPiuParagrafiLeggeLoStatoVivo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("uno");
        scene.Press(Avalonia.Input.Key.Enter);
        scene.Type("due");
        var catalog = scene.Services.GetRequiredService<IContentControlCatalog>();
        var editing = scene.Services.GetRequiredService<IContentControlEditing>();
        var paragraphs = scene.Document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        var inserted = editing.Insert(scene.Document, scene.History, new DocumentRange(new DocumentPosition(paragraphs[0].Id, 0), new DocumentPosition(paragraphs[1].Id, 3)), new ContentControl { Kind = ContentControlKind.RichText });
        var span = Assert.Single(catalog.All(scene.Document));

        editing.Fill(scene.Document, scene.History, span.Id, "tre");

        Assert.Equal("tre", scene.Paragraphs[0]);
        var filled = Assert.Single(catalog.All(scene.Document));
        Assert.Equal(("tre", 0, 3), (scene.Paragraphs[0], filled.Start.Offset, filled.End.Offset));
        Assert.NotNull(inserted);
    }, CancellationToken.None);
}
