using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFindProtectionTests
{
    [Fact]
    public Task SostituisciTuttoNonToccaIlDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("alfa beta alfa");
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);
        var services = scene.Services;
        var commands = new FolioFindCommands(services.GetRequiredService<IActiveFolioContext>(), services.GetRequiredService<IDocumentSearch>(),
            services.GetRequiredService<IDocumentStatisticsCounter>(), services.GetRequiredService<IStatusBarService>(), null!, services.GetRequiredService<ITextCatalog>());

        var replaced = commands.ReplaceAll(scene.Editor, new SearchQuery("alfa"), "gamma");

        Assert.Equal(0, replaced);
        Assert.Equal(["alfa beta alfa"], scene.Paragraphs);
    }, CancellationToken.None);
}

public sealed class FolioListKeyProtectionTests
{
    [Fact]
    public Task TabaInizioVoceNonCambiaLivelloNelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Voce");
        scene.Editor.Apply((writer, range) => writer.SetList(scene.Document, scene.History, range, PulseOffice.Documents.Styles.DefaultStyleNames.Numbering123, 0), scene.Editor.ParagraphRange());
        scene.Editor.PlaceCaret(scene.Editor.Caret with { Offset = 0 }, false);
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);

        scene.Press(Avalonia.Input.Key.Tab);

        Assert.Equal(0, scene.Editor.CurrentParagraph().BulletLevel);
    }, CancellationToken.None);
}

public sealed class FolioNavigatorProtectionTests
{
    [Fact]
    public Task IlNavigatoreNonSpostaICapitoliDelDocumentoProtetto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new PulseOffice.Documents.Blocks.ParagraphBlock("h1", "Primo", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1)),
            new PulseOffice.Documents.Blocks.ParagraphBlock("t1", "uno", PulseOffice.Documents.Styles.DefaultStyleNames.TextBody),
            new PulseOffice.Documents.Blocks.ParagraphBlock("h2", "Secondo", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1)),
            new PulseOffice.Documents.Blocks.ParagraphBlock("t2", "due", PulseOffice.Documents.Styles.DefaultStyleNames.TextBody),
        ]));
        scene.Document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);
        var source = scene.Services.GetRequiredService<FolioNavigatorSource>();

        Assert.False(source.CanMove("Headings", "h2", PulseOffice.UI.Controls.Navigation.NavigatorMove.Up));
        source.Drop("Headings", "h2", "h1");
        source.Move("Headings", "h2", PulseOffice.UI.Controls.Navigation.NavigatorMove.Demote);

        Assert.Equal(["h1", "t1", "h2", "t2"], scene.Document.Body.Blocks.Select(block => block.Id));
        Assert.Equal(PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1), ((PulseOffice.Documents.Blocks.ParagraphBlock)scene.Document.Body.Blocks[2]).StyleName);
    }, CancellationToken.None);
}
